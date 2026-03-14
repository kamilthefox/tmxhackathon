"""
Сервис генерации синтетических данных.

Запуск:
    python generate.py
    python generate.py --prompts-file my_prompts.txt  # свои промпты из файла
    python generate.py --count 5                      # 5 картинок на промпт
    python generate.py --dry-run                      # покажет промпты, не генерируя
"""
import argparse
import asyncio
import json
import logging
import os
import time
from pathlib import Path

from Config import OUTPUT_DIR, PROMPTS, IMAGES_PER_PROMPT, MAX_CONCURRENT, SAFE_NEGATIVE_PROMPT, CATEGORIES
from NodeManager import node_manager
from SDclient import sd_client

# Создаём output директорию сразу (для логов)
Path(OUTPUT_DIR).mkdir(parents=True, exist_ok=True)

# ─────────────────────────────────────────────
#  Логирование
# ─────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler(f"{OUTPUT_DIR}/generation.log", encoding="utf-8"),
    ],
)
logger = logging.getLogger("generate")


# ─────────────────────────────────────────────
#  Сохранение
# ─────────────────────────────────────────────

def save_image(image_bytes: bytes, prompt: str, categories: list[int], index: int, seed: int) -> Path:
    out = Path(OUTPUT_DIR)
    out.mkdir(parents=True, exist_ok=True)

    # Имя файла: <порядковый>_<категории>_<первые слова промпта>_seed<seed>.png
    cat_str = "_".join(f"cat{c}" for c in sorted(categories))
    slug = "_".join(prompt.split()[:4]).lower()
    slug = "".join(c if c.isalnum() or c == "_" else "" for c in slug)
    filename = f"{index:04d}_{cat_str}_{slug}_seed{seed}.png"
    path = out / filename
    path.write_bytes(image_bytes)
    return path


def save_metadata(index: int, prompt: str, categories: list[int], seed: int, filename: str):
    """Сохраняет JSON метаданные рядом с картинкой."""
    out = Path(OUTPUT_DIR)
    meta_path = out / f"{Path(filename).stem}.json"
    
    metadata = {
        "index": index,
        "prompt": prompt,
        "categories": [
            {"id_cat": cat_id, "name": CATEGORIES[cat_id]}
            for cat_id in sorted(categories)
        ],
        "seed": seed,
        "filename": filename,
    }
    
    meta_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")


# ─────────────────────────────────────────────
#  Задача генерации одной картинки
# ─────────────────────────────────────────────

async def generate_one(
    task_id: int,
    prompt: str,
    categories: list[int],
    semaphore: asyncio.Semaphore,
    stats: dict,
) -> None:
    async with semaphore:
        cat_names = ", ".join(CATEGORIES[c] for c in categories)
        logger.info(f"[task {task_id}] Старт [{cat_names}] → {prompt[:60]}...")

        img_bytes, seed, error = await sd_client.txt2img(
            prompt=prompt,
            negative_prompt="",   # SAFE_NEGATIVE_PROMPT добавится автоматически в sd_client
        )

        if error:
            logger.error(f"[task {task_id}] ❌ Ошибка: {error}")
            stats["failed"] += 1
            return

        path = save_image(img_bytes, prompt, categories, task_id, seed or -1)
        save_metadata(task_id, prompt, categories, seed or -1, path.name)
        stats["done"] += 1
        logger.info(f"[task {task_id}] ✅ Сохранено → {path.name} (seed={seed})")


# ─────────────────────────────────────────────
#  Главная функция
# ─────────────────────────────────────────────

async def main(prompts_with_cats: list[tuple[str, list[int]]], count: int, dry_run: bool):
    # Разворачиваем промпты × count
    tasks_data = [(p, cats) for p, cats in prompts_with_cats for _ in range(count)]
    total = len(tasks_data)

    if dry_run:
        print(f"\n=== DRY RUN: {total} задач ===")
        for i, (p, cats) in enumerate(tasks_data, 1):
            cat_names = ", ".join(CATEGORIES[c] for c in cats)
            print(f"  {i:03d}. [{cat_names}] {p}")
        print(f"\nНегатив (применяется ко всем):\n  {SAFE_NEGATIVE_PROMPT}")
        
        # Статистика по категориям
        cat_counts = {}
        for _, cats in prompts_with_cats:
            for c in cats:
                cat_counts[c] = cat_counts.get(c, 0) + count
        print("\nСтатистика категорий:")
        for cat_id in sorted(cat_counts.keys()):
            print(f"  {CATEGORIES[cat_id]}: {cat_counts[cat_id]} изображений")
        return

    logger.info(f"=== Генерация {total} изображений ===")
    logger.info(f"Промптов: {len(prompts_with_cats)}, повторов: {count}, параллельно: {MAX_CONCURRENT}")
    logger.info(f"Выходная папка: {Path(OUTPUT_DIR).absolute()}")

    # Healthcheck нод при старте
    logger.info("Проверка нод...")
    await node_manager.check_all_health()
    logger.info(f"Состояние нод:\n{node_manager.status_report()}")

    semaphore = asyncio.Semaphore(MAX_CONCURRENT)
    stats = {"done": 0, "failed": 0}
    t0 = time.monotonic()

    tasks = [
        asyncio.create_task(generate_one(i, p, cats, semaphore, stats))
        for i, (p, cats) in enumerate(tasks_data, 1)
    ]

    # Ждём завершения всех задач
    await asyncio.gather(*tasks)

    elapsed = time.monotonic() - t0
    logger.info(
        f"\n=== Готово за {elapsed:.1f}с ===\n"
        f"  Успешно : {stats['done']}\n"
        f"  Ошибки  : {stats['failed']}\n"
        f"  Выход   : {Path(OUTPUT_DIR).absolute()}\n"
        f"Финальное состояние нод:\n{node_manager.status_report()}"
    )


# ─────────────────────────────────────────────
#  CLI
# ─────────────────────────────────────────────

def parse_args():
    parser = argparse.ArgumentParser(description="Генератор синтетических данных через SD")
    parser.add_argument(
        "--prompts-file", "-p",
        help="Путь к txt-файлу с промптами (один на строку). По умолчанию берутся из config.py",
    )
    parser.add_argument(
        "--count", "-c", type=int, default=IMAGES_PER_PROMPT,
        help=f"Картинок на промпт (default: {IMAGES_PER_PROMPT})",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Показать промпты без реальной генерации",
    )
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()

    if args.prompts_file:
        # Простой формат файла: один промпт на строку, категории не поддерживаются
        with open(args.prompts_file, encoding="utf-8") as f:
            lines = [line.strip() for line in f if line.strip() and not line.startswith("#")]
        prompts_with_cats = [(line, [7]) for line in lines]  # category 7 = "Другое"
        logger.info(f"Загружено {len(prompts_with_cats)} промптов из {args.prompts_file}")
    else:
        prompts_with_cats = PROMPTS
        logger.info(f"Используем {len(prompts_with_cats)} промптов из config.py")

    asyncio.run(main(prompts_with_cats, args.count, args.dry_run))