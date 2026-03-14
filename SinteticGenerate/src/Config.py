"""Конфиг сервиса генерации синтетических данных."""
from typing import List

# ─────────────────────────────────────────────
#  НОДЫ
# ─────────────────────────────────────────────
NODES = [
    {
        "id": "node_0",
        "url": "http://IP:7860",
        "timeout": 180,
        "cooldown": 120,
    },
    {
        "id": "node_1",
        "url": "http://IP:7860",
        "timeout": 180,
        "cooldown": 120,
    },
]

# ─────────────────────────────────────────────
#  ПАРАМЕТРЫ ГЕНЕРАЦИИ
# ─────────────────────────────────────────────
GENERATION = {
    "steps": 20,
    "cfg_scale": 7.0,
    "sampler_name": "Euler a",
    "scheduler": "automatic",
    "width": 768,
    "height": 512,
    "clip_skip": 2,
    "save_images": False,
    "send_images": True,
}

# ─────────────────────────────────────────────
#  НЕГАТИВНЫЙ ПРОМПТ — без весов, чистые ключевые слова
# ─────────────────────────────────────────────
SAFE_NEGATIVE_PROMPT = (
    # Люди и части тела
    "people, person, human, man, woman, girl, boy, child, crowd, passenger, "
    "face, hands, fingers, arms, legs, feet, body, silhouette, character, figure, "
    # NSFW
    "nsfw, nude, naked, explicit, sexual, pornographic, "
    "breasts, nipples, genitalia, erotic, hentai, lewd, suggestive, "
    # Посторонние объекты
    "car, automobile, vehicle, truck, bus, road, street, outdoor, outside, "
    # Природа и погода
    "rain, sky, clouds, nature, "
    # Животные
    "animal, dog, cat, "
    # Качество
    "bad quality, worst quality, blurry, low resolution, artifacts, "
    "watermark, signature, logo, text"
)

# ─────────────────────────────────────────────
#  КАТЕГОРИИ ДЕФЕКТОВ
# ─────────────────────────────────────────────
CATEGORIES = {
    1: "Сиденье",
    2: "Поручень",
    3: "Стена",
    4: "Пол",
    5: "Граффити",
    6: "Стекло",
}

# ─────────────────────────────────────────────
#  СУФФИКС КАЧЕСТВА
#  Добавляется к каждому промпту
#  "3D" вместо "3d render" — лучше воспринимается моделью
# ─────────────────────────────────────────────
_Q  = "photorealistic, highly detailed"
_QW = "photorealistic, wide shot"
_QM = "photorealistic, highly detailed macro, close-up"

# ─────────────────────────────────────────────
#  ПРОМПТЫ
#  Формат: (prompt, [category_ids])
#  Лучшие рабочие промпты оставлены как есть,
#  только добавлен суффикс качества
# ─────────────────────────────────────────────
PROMPTS: List[tuple[str, list[int]]] = [

    # ════════════════════════════════════
    #  СИДЕНЬЯ (cat=1)
    # ════════════════════════════════════

    # ✅ лучшие рабочие
    (f"subway train car interior, torn fabric seat, ripped upholstery, visible padding, empty seat, fluorescent lighting, {_Q}", [1]),
    (f"metro train seat, dirty stained fabric, grime, worn surface, close-up view, {_QM}", [1]),
    (f"subway seat with deep cut, vandalism damage, torn material, interior lighting, {_QM}", [1]),
    (f"metro seat with graffiti marker stains, scribbles on fabric, vandalized, {_QM}", [1, 5]),

    # доп. вариации
    (f"subway train car interior, bench seat, cigarette burns, scorch marks, faded worn fabric, stains, {_QM}", [1]),
    (f"subway train car interior, dirty bench seat, grimy worn plastic surface, passenger stains, fluorescent light, {_Q}", [1]),
    (f"subway train car interior, row of seats, some torn some intact, varying damage levels, {_QW}", [1]),
    (f"subway train car interior, seat corner, worn stitching coming apart, fraying fabric, {_QM}", [1]),

    # ════════════════════════════════════
    #  ПОРУЧНИ (cat=2)
    # ════════════════════════════════════

    # ✅ лучшие рабочие
    (f"dirty yellow handrail in subway car, scratches, worn paint, realistic lighting, {_QM}", [2]),

    # доп. вариации
    (f"subway train car interior, vertical stainless steel pole handrail, grimy surface, fingerprint smudges, worn, fluorescent lighting, {_QM}", [2]),
    (f"subway train car interior, overhead hanging strap handle, yellow rubber grip, dirty sticky surface, {_QM}", [2]),
    (f"subway train car interior, stainless steel pole, greasy surface, dirt layer, oxidation spots, {_QM}", [2]),
    (f"subway train car interior, metal grab pole, rust spots, peeling chrome coating, corrosion, {_QM}", [2]),
    (f"subway train car interior, metal pole surface texture, grime layer, scratches, oxidation, {_QM}", [2]),

    # ════════════════════════════════════
    #  СТЕНЫ (cat=3)
    # ════════════════════════════════════

    # ✅ лучшие рабочие
    (f"dirty subway car wall, accumulated grime in corners, worn paint, realistic lighting, {_QM}", [3]),

    # доп. вариации
    (f"subway train car interior, plastic wall panel, scratches and scuff marks, dirty surface, fluorescent lighting, {_QM}", [3]),
    (f"subway train car interior, wall with informational poster, torn poster edges, marker graffiti around it, {_Q}", [3, 5]),
    (f"subway train car interior, wall panel, crack damage, broken plastic, exposed metal frame underneath, {_QM}", [3]),
    (f"subway train car interior, interior wall, water leak stains, discoloration streaks, moisture damage, {_QM}", [3]),
    (f"subway train car interior, wall, scratched etched graffiti tags, deep scratches in plastic surface, vandalism, {_QM}", [3, 5]),

    # ════════════════════════════════════
    #  ПОЛ (cat=4)
    # ════════════════════════════════════

    # ✅ лучший рабочий
    (f"metro train floor with spilled liquid stain, dried puddle, dirty, {_QM}", [4]),

    # доп. вариации
    (f"subway train car interior, rubber floor, non-slip texture, grime buildup in grooves, dirty, {_QM}", [4]),
    (f"subway train car interior, worn rubber floor surface, faded yellow safety edge markings, scuff marks, {_QM}", [4]),
    (f"subway train car interior, floor near sliding doors, accumulated dirt and debris, high traffic wear, {_Q}", [4]),
    (f"subway train car interior, floor surface, black hardened stains, adhesive residue spots, dark marks, {_QM}", [4]),
    (f"subway train car interior, wet floor, dirty water tracked in, damp rubber surface, footprint marks, {_QM}", [4]),
    (f"subway train car interior, floor texture close-up, anti-slip rubber mat, worn through in center path, {_QM}", [4]),

    # ════════════════════════════════════
    #  ГРАФФИТИ (cat=5)
    # ════════════════════════════════════

    # ✅ лучшие рабочие
    (f"metro seat with graffiti marker drawings, vandalized fabric, colorful tags, {_QM}", [1, 5]),
    (f"subway interior, graffiti on multiple surfaces, walls seats poles, urban decay, {_QW}", [1, 2, 3, 5]),

    # доп. вариации
    (f"subway train car interior, walls covered in spray paint graffiti tags, marker scribbles, vandalism, {_Q}", [5]),
    (f"subway train car interior, wall with layered graffiti, multiple spray paint colors overlapping, {_QM}", [3, 5]),
    (f"subway train car interior, seat fabric with permanent marker drawings and tags, vandalized upholstery, {_QM}", [1, 5]),

    # ════════════════════════════════════
    #  СТЕКЛО (cat=6)
    # ════════════════════════════════════

    # ✅ лучшие рабочие
    (f"subway car window, scratched glass surface, etched vandalism, realistic, {_QM}", [6]),
    (f"metro train window with fingerprint smudges, dirty glass, streaks, {_QM}", [6]),
    (f"subway car door window, cracked glass with spider web pattern, damage, {_QM}", [6]),
    (f"subway window, heavy scratching etched graffiti, damaged glass surface, {_QM}", [5, 6]),

    # доп. вариации
    (f"subway train car interior, window glass, deep scratches, etched vandalism, damaged surface, {_QM}", [6]),
    (f"subway train car interior, window inner surface, condensation water droplets, grimy smeared, {_QM}", [6]),
    (f"subway train car interior, window glass with etched graffiti scratches, vandalism tags, {_QM}", [5, 6]),

    # ════════════════════════════════════
    #  КОМБИНИРОВАННЫЕ СЦЕНЫ
    # ════════════════════════════════════

    (f"subway train car interior, wide view, dirty rubber floor, torn seats, grimy metal poles, fluorescent lighting, {_QW}", [1, 2, 4]),
    (f"subway train car interior, wide shot, graffiti on walls, dirty windows, worn torn seats, {_QW}", [1, 3, 5, 6]),
    (f"subway train car interior, floor corner meeting scratched wall, accumulated grime, {_QM}", [3, 4]),
    (f"subway train car interior, sliding door area, grimy metal pole, dirty floor, scuffed wall panels, {_QW}", [2, 3, 4]),
    (f"subway train car interior, vandalized seats with graffiti marker tags, dirty floor, {_Q}", [1, 4, 5]),
    (f"subway train car interior, aged worn interior, dirty windows, torn seats, grimy poles, worn floor, {_QW}", [1, 2, 4, 6]),

    # ════════════════════════════════════
    #  ДЕТАЛЬНЫЕ КРУПНЫЕ ПЛАНЫ
    # ════════════════════════════════════

    (f"subway train car interior, torn seat upholstery, ripped fabric edge, grey padding visible underneath, {_QM}", [1]),
    (f"subway train car interior, metal handrail pole close-up, grimy surface, fingerprint marks, {_QM}", [2]),
    (f"subway train car interior, plastic wall panel close-up, deep scratch marks, vandalism damage, {_QM}", [3]),
    (f"subway train car interior, rubber anti-slip floor tile, textured surface filled with dirt, grime, {_QM}", [4]),
    (f"subway train car interior, window glass close-up, heavy scratch etched graffiti tags, damaged surface, {_QM}", [5, 6]),
]

# ─────────────────────────────────────────────
#  НАСТРОЙКИ ВЫВОДА
#  48 промптов × 26 = 1248 ≈ 1200 изображений
# ─────────────────────────────────────────────
OUTPUT_DIR = "./output"
IMAGES_PER_PROMPT = 30
MAX_CONCURRENT = 2