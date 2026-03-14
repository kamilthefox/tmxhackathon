"""Менеджер нод: распределение нагрузки, карантин, retry."""
import asyncio
import logging
import time
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, List

import aiohttp

from Config import NODES

logger = logging.getLogger(__name__)


class NodeStatus(Enum):
    HEALTHY   = "healthy"
    COOLDOWN  = "cooldown"   # временно выведена из ротации


@dataclass
class Node:
    id: str
    url: str
    timeout: int
    cooldown: int  # секунды карантина

    # runtime state
    status:      NodeStatus = NodeStatus.HEALTHY
    busy:        bool       = False
    cooldown_until: float   = 0.0   # timestamp, после которого нода снова доступна
    total_ok:    int        = 0
    total_fail:  int        = 0

    # ── helpers ──────────────────────────────────────────

    def is_available(self) -> bool:
        """Нода доступна, если не занята и не в карантине."""
        if self.busy:
            return False
        if self.status == NodeStatus.COOLDOWN:
            if time.monotonic() >= self.cooldown_until:
                self._recover()
            else:
                return False
        return True

    def enter_cooldown(self):
        self.status = NodeStatus.COOLDOWN
        self.cooldown_until = time.monotonic() + self.cooldown
        self.busy = False
        logger.warning(
            f"[{self.id}] 🔴 Ушёл в карантин на {self.cooldown}с "
            f"(до {time.strftime('%H:%M:%S', time.localtime(time.time() + self.cooldown))})"
        )

    def _recover(self):
        self.status = NodeStatus.HEALTHY
        logger.info(f"[{self.id}] 🟢 Вышел из карантина, снова доступен")

    def seconds_until_available(self) -> float:
        if self.status == NodeStatus.COOLDOWN:
            return max(0.0, self.cooldown_until - time.monotonic())
        return 0.0


class NodeManager:
    """
    Round-robin с учётом занятости и карантина.
    При 502 / TimeoutError нода уходит в cooldown на N секунд.
    """

    def __init__(self, nodes_cfg: list):
        self.nodes: List[Node] = [
            Node(
                id=cfg["id"],
                url=cfg["url"],
                timeout=cfg["timeout"],
                cooldown=cfg["cooldown"],
            )
            for cfg in nodes_cfg
        ]
        self._lock = asyncio.Lock()
        self._rr_index = 0  # round-robin курсор

    # ── выбор ноды ────────────────────────────────────────

    async def acquire_node(self, wait_timeout: float = 300.0) -> Optional[Node]:
        """
        Ждёт свободную здоровую ноду (до wait_timeout секунд).
        Возвращает ноду с флагом busy=True или None если не дождались.
        """
        deadline = time.monotonic() + wait_timeout
        while time.monotonic() < deadline:
            async with self._lock:
                node = self._pick_round_robin()
                if node:
                    node.busy = True
                    logger.debug(f"[{node.id}] Нода захвачена")
                    return node

            # Ждём чуть-чуть и повторяем
            await asyncio.sleep(1.0)

        logger.error("Нет доступных нод дольше wait_timeout секунд")
        return None

    def _pick_round_robin(self) -> Optional[Node]:
        n = len(self.nodes)
        for _ in range(n):
            idx = self._rr_index % n
            self._rr_index += 1
            node = self.nodes[idx]
            if node.is_available():
                return node
        return None

    def release_node(self, node: Node, *, failed: bool = False, fatal: bool = False):
        """Освобождает ноду. fatal=True → карантин."""
        if fatal:
            node.total_fail += 1
            node.enter_cooldown()
        else:
            if failed:
                node.total_fail += 1
            else:
                node.total_ok += 1
            node.busy = False
            logger.debug(f"[{node.id}] Нода освобождена")

    # ── статус ────────────────────────────────────────────

    def status_report(self) -> str:
        lines = []
        for n in self.nodes:
            if n.status == NodeStatus.COOLDOWN:
                secs = n.seconds_until_available()
                state = f"🔴 COOLDOWN ({secs:.0f}с)"
            elif n.busy:
                state = "🟡 BUSY"
            else:
                state = "🟢 OK"
            lines.append(
                f"  {n.id} [{n.url}] {state} | ok={n.total_ok} fail={n.total_fail}"
            )
        return "\n".join(lines)

    async def check_all_health(self):
        """Быстрая проверка /sdapi/v1/options на всех нодах (при старте)."""
        async def _check(node: Node):
            url = f"{node.url.rstrip('/')}/sdapi/v1/options"
            try:
                async with aiohttp.ClientSession() as s:
                    async with s.get(url, timeout=aiohttp.ClientTimeout(total=10)) as r:
                        r.raise_for_status()
                logger.info(f"[{node.id}] ✅ Healthcheck OK")
            except Exception as e:
                logger.warning(f"[{node.id}] ⚠️ Healthcheck FAILED: {e}")

        await asyncio.gather(*[_check(n) for n in self.nodes])


# Глобальный экземпляр
node_manager = NodeManager(NODES)