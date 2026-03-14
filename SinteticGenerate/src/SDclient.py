"""SD API клиент — только txt2img, без img2img."""
import asyncio
import base64
import json
import logging
import time
from typing import Optional, Tuple

import aiohttp

from Config import GENERATION, SAFE_NEGATIVE_PROMPT
from NodeManager import Node, node_manager

logger = logging.getLogger(__name__)

# HTTP-статусы, которые отправляют ноду в карантин
FATAL_HTTP_STATUSES = {502, 503, 504}


class SDClient:

    async def txt2img(
        self,
        prompt: str,
        negative_prompt: str = "",
        seed: int = -1,
        width: Optional[int] = None,
        height: Optional[int] = None,
    ) -> Tuple[Optional[bytes], Optional[int], Optional[str]]:
        """
        Генерирует изображение через txt2img.
        Возвращает (image_bytes, seed_used, error_str).
        При ошибке 502 / таймауте нода уходит в карантин.
        """
        node = await node_manager.acquire_node(wait_timeout=300)
        if node is None:
            return None, None, "Нет доступных нод"

        payload = self._build_payload(
            prompt=prompt,
            negative_prompt=negative_prompt,
            seed=seed,
            width=width or GENERATION["width"],
            height=height or GENERATION["height"],
        )

        try:
            result = await self._request(node, payload)
            node_manager.release_node(node, failed=not result["success"])
            if result["success"]:
                return result["image_bytes"], result["seed"], None
            else:
                return None, None, result["error"]

        except (aiohttp.ClientResponseError,) as e:
            fatal = e.status in FATAL_HTTP_STATUSES
            node_manager.release_node(node, failed=True, fatal=fatal)
            return None, None, f"HTTP {e.status}: {e.message}"

        except asyncio.TimeoutError:
            # Таймаут → карантин
            node_manager.release_node(node, failed=True, fatal=True)
            return None, None, f"Таймаут на ноде [{node.id}]"

        except aiohttp.ServerDisconnectedError:
            node_manager.release_node(node, failed=True, fatal=True)
            return None, None, f"Сервер [{node.id}] оборвал соединение (возможно PyTorch OOM)"

        except Exception as e:
            node_manager.release_node(node, failed=True)
            logger.exception(f"[{node.id}] Непредвиденная ошибка")
            return None, None, str(e)

    # ── internals ──────────────────────────────────────────

    def _build_payload(
        self,
        prompt: str,
        negative_prompt: str,
        seed: int,
        width: int,
        height: int,
    ) -> dict:
        # Объединяем пользовательский негатив с safety-негативом
        combined_negative = self._combine_negatives(negative_prompt, SAFE_NEGATIVE_PROMPT)

        payload = {
            "prompt": prompt,
            "negative_prompt": combined_negative,
            "steps": GENERATION["steps"],
            "cfg_scale": GENERATION["cfg_scale"],
            "sampler_name": GENERATION["sampler_name"],
            "scheduler": GENERATION["scheduler"],
            "width": width,
            "height": height,
            "clip_skip": GENERATION["clip_skip"],
            "enable_hr": False,
            "save_images": GENERATION["save_images"],
            "send_images": GENERATION["send_images"],
        }
        if seed != -1:
            payload["seed"] = seed
        return payload

    @staticmethod
    def _combine_negatives(user_neg: str, safe_neg: str) -> str:
        """Мержит два негативных промпта без дублирования."""
        if not user_neg.strip():
            return safe_neg
        # Добавляем safety-теги которых ещё нет в user_neg
        existing = {t.strip().lower() for t in user_neg.split(",")}
        extra = [t for t in safe_neg.split(",") if t.strip().lower() not in existing]
        if extra:
            return user_neg.rstrip(", ") + ", " + ", ".join(extra)
        return user_neg

    async def _request(self, node: Node, payload: dict) -> dict:
        url = f"{node.url.rstrip('/')}/sdapi/v1/txt2img"
        logger.info(f"[{node.id}] POST {url}")

        http_timeout = aiohttp.ClientTimeout(total=node.timeout + 10, connect=15)
        start = time.monotonic()

        async with aiohttp.ClientSession() as session:
            async with session.post(url, json=payload, timeout=http_timeout) as resp:
                elapsed = time.monotonic() - start
                logger.info(f"[{node.id}] Ответ {resp.status} за {elapsed:.1f}с")

                if resp.status in FATAL_HTTP_STATUSES:
                    # Поднимаем, чтобы поймать в вызывающем методе
                    resp.raise_for_status()

                resp.raise_for_status()
                data = await resp.json()

        if not data.get("images"):
            return {"success": False, "error": "SD не вернул изображение"}

        img_data = data["images"][0]
        if isinstance(img_data, str) and img_data.startswith("data:image"):
            img_data = img_data.split(",")[1]
        image_bytes = base64.b64decode(img_data)

        seed_used = None
        if info_raw := data.get("info"):
            try:
                seed_used = json.loads(info_raw).get("seed")
            except Exception:
                pass

        return {"success": True, "image_bytes": image_bytes, "seed": seed_used}


sd_client = SDClient()