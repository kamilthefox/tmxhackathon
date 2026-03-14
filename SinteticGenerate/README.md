# Synthetic Data Generator (SD WebUI)

Генератор синтетических данных для метро/урбан-деградации через Stable Diffusion.

## Структура

```
synthetic_gen/
├── config.py        ← конфиг: IP нод, промпты, параметры генерации
├── node_manager.py  ← балансировка нагрузки + карантин при 502/таймауте  
├── sd_client.py     ← только txt2img (img2img отключён намеренно)
├── generate.py      ← точка входа
└── output/          ← сюда сохраняются PNG
```

## Быстрый старт (Docker)

```bash
# 1. Поправь IP нод в config.py
#    NODES[0]["url"] = "http://192.168.X.X:7860"
#    NODES[1]["url"] = "http://192.168.X.X:7860"

# 2. Собери образ
docker compose build

# 3. Посмотреть что будет генерироваться (без запросов к API)
docker compose run --rm synthetic-gen --dry-run

# 4. Запустить генерацию (настройки по умолчанию из config.py)
docker compose up

# 5. Задать своё количество картинок на промпт
docker compose run --rm synthetic-gen --count 5

# 6. Свои промпты из файла (сначала положи файл в папку проекта)
docker compose run --rm synthetic-gen --prompts-file /app/my_prompts.txt
```

Результаты будут в `./output/`, логи в `./generation.log`.

## Быстрый старт (без Docker)

```bash
pip install -r requirements.txt

# 1. Поправь IP нод в config.py
# 2. Запусти
python generate.py --dry-run  # проверка
python generate.py            # генерация
```

## Логика нод

- Балансировка **round-robin** между нодами
- Если нода вернула **502 / 503 / 504** или **таймаут** или **оборвала соединение** (PyTorch OOM) — она уходит в **карантин на 120 секунд**
- Пока нода в карантине — все задачи идут на вторую
- Через 120с нода автоматически возвращается в ротацию

## Безопасность (NSFW)

`SAFE_NEGATIVE_PROMPT` в `config.py` автоматически добавляется к каждому запросу.
Содержит теги: nsfw, nude, sexual, pornographic, explicit, hentai, lewd и т.д.

Это не гарантия, финальная ручная проверка датасета обязательна.

## Настройка промптов

Промпты хранятся в `config.py → PROMPTS`. Можно дополнять прямо там
или передавать свой файл через `--prompts-file`.

Формат файла промптов:
```
# Это комментарий, строка игнорируется
subway station interior, dirty walls, realistic
metro tunnel, dark, wet concrete
```

## Параметры генерации

В `config.py → GENERATION`:
| Параметр     | Значение по умолчанию | Описание               |
|--------------|-----------------------|------------------------|
| steps        | 28                    | Шаги денойзинга        |
| cfg_scale    | 7.0                   | Следование промпту     |
| sampler_name | Euler a               | Сэмплер                |
| width        | 768                   | Ширина изображения     |
| height       | 512                   | Высота изображения     |
| clip_skip    | 2                     | Для аниме-моделей      |

## Очистка Docker

```bash
# Остановить контейнер
docker compose down

# Удалить образ
docker rmi synthetic_gen-synthetic-gen

# Или полная очистка всего Docker мусора (осторожно!)
docker system prune -a
```

## Troubleshooting

**Ноды недоступны при запуске в Docker:**
- Проверь что `network_mode: host` включен в `docker-compose.yml`
- Проверь что IP нод правильные и SD WebUI запущены
- Если ноды в той же машине что и Docker — используй `127.0.0.1:7860` или IP хоста

**Хочу поменять config.py без пересборки образа:**
- Раскомментируй строку `- ./config.py:/app/config.py:ro` в `docker-compose.yml`
- Изменения в конфиге будут применяться сразу