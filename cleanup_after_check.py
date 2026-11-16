"""Убирает тестовые записи, оставшиеся после живой проверки."""

import io
import json
from pathlib import Path

data = Path.home() / "AppData" / "Roaming" / "Kapsula"

test_clip = {
    "обычная заметка для проверки буфера",
    "новая строка на глазах у панели",
    "таймер на 2 минуты",
    "Zx9Qw2Lp8Rt4Vb7N",
}
test_chat = {
    "таймер на 2 минуты",
    "Таймер на 2 мин поставлен.",
    "ответь одним словом: работает",
    "Да.",
}

clip_file = data / "clipboard.json"
if clip_file.exists():
    items = json.loads(clip_file.read_text(encoding="utf-8"))
    kept = [i for i in items if i.get("Text") not in test_clip]
    clip_file.write_text(json.dumps(kept, ensure_ascii=False, indent=2), encoding="utf-8")
    print("буфер: было", len(items), "стало", len(kept))

chat_file = data / "ai_chat.json"
if chat_file.exists():
    messages = json.loads(chat_file.read_text(encoding="utf-8"))
    kept = [m for m in messages if m.get("Content") not in test_chat]
    chat_file.write_text(json.dumps(kept, ensure_ascii=False, indent=2), encoding="utf-8")
    print("чат: было", len(messages), "стало", len(kept))

timers_file = data / "timers.json"
if timers_file.exists():
    timers_file.write_text("[]", encoding="utf-8")
    print("таймеры очищены")

settings_file = data / "settings.json"
if settings_file.exists():
    settings = json.loads(settings_file.read_text(encoding="utf-8"))
    settings["AiModel"] = "free-auto"
    settings["AiServerUrl"] = ""
    settings["AiServerKey"] = ""
    settings["AiServerModel"] = ""
    settings_file.write_text(json.dumps(settings, ensure_ascii=False, indent=2), encoding="utf-8")
    print("настройки: модель free-auto, поля своего сервера очищены")
