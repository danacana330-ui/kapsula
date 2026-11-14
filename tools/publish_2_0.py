"""Публикация релиза Kapsula 2.0 через REST API GitHub."""
import subprocess
import sys
import time

import requests

OWNER = "danacana330-ui"
REPO = "kapsula"
TAG = "v2.0.0"
API = "https://api.github.com"
ROOT = r"C:\Users\Asus\Desktop\Kapsula\release"

BODY = """# Kapsula 2.0

Маленький «островок» сверху экрана для Windows: полоска, которая разворачивается в панель
с буфером обмена, полкой файлов, заметками, таймером, погодой и ИИ.

![Kapsula 2.0](https://github.com/danacana330-ui/kapsula/releases/download/v2.0.0/capsule_2.0.png)

## Что нового

- **ИИ работает сам по себе.** Капсула скачивает небольшую модель Qwen2.5 (1,1 ГБ) и отвечает
  без интернета и без Ollama. Есть вариант поумнее — Qwen2.5 3B (2 ГБ). Кнопки «Скачать» и
  «Удалить» прямо в панели ИИ, прогресс виден в панели и в трее. Если на компьютере есть Ollama,
  капсула предложит отвечать на её моделях.
- **Сводка дня.** Утром рядом с капсулой появляется плашка «ваша сводка готова»: погода на день,
  планы из незакрытых заметок и курсы юаня, доллара и евро. Время настраивается в шестерёнке;
  если компьютер спал — сводка покажется при первом запуске. Закрывается кнопкой «я понял, спасибо».
- **Запись экрана теперь со звуком системы.**
- **Обновления приходят сами:** раз в сутки капсула проверяет эту страницу и предлагает новую версию.
- Таймер доживает до перезапуска, история чата с ИИ сохраняется, у ответа есть кнопка «Стоп».
- Мелочи: подсказки в полях больше не мешают каретке, кнопка «Создать ярлык на рабочем столе»,
  сводка и запись включаются плитками на главной.

## Как поставить

1. Скачайте `Kapsula-2.0.zip`, распакуйте в любую папку и запустите `Kapsula.exe`.
2. SmartScreen может спросить подтверждение — «Подробнее» → «Выполнить в любом случае».
   Так бывает с любым новым файлом без платной подписи, ничего страшного.

Горячие клавиши: Alt+Space — панель запуска, Alt+S — скриншот, Alt+R — запись экрана.

sha256: F1F2638045E79E330DDFED684304DA321FD750033FED43CAB9D1291E07A397B0
"""


def token():
    out = subprocess.run(
        ["git", "credential", "fill"],
        input="protocol=https\nhost=github.com\n\n",
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    for line in out.splitlines():
        if line.startswith("password="):
            return line[len("password="):]
    raise SystemExit("токен не найден в git credential")


def main():
    headers = {"Authorization": "Bearer " + token(), "Accept": "application/vnd.github+json"}

    rel = requests.get(f"{API}/repos/{OWNER}/{REPO}/releases/tags/{TAG}", headers=headers, timeout=60)
    if rel.status_code == 404:
        created = requests.post(
            f"{API}/repos/{OWNER}/{REPO}/releases",
            headers=headers,
            json={"tag_name": TAG, "name": "Kapsula 2.0", "body": BODY, "draft": False},
            timeout=120,
        )
        created.raise_for_status()
        rel = created
    rel.raise_for_status()
    data = rel.json()
    rid = data["id"]
    existing = {a["name"] for a in data.get("assets", [])}
    print(f"релиз {TAG} id={rid}, уже залито: {sorted(existing) or '—'}", flush=True)

    for path, name in [
        (ROOT + r"\Kapsula-2.0\Kapsula.exe", "Kapsula.exe"),
        (ROOT + r"\Kapsula-2.0.zip", "Kapsula-2.0.zip"),
        (ROOT + r"\capsule_2.0.png", "capsule_2.0.png"),
    ]:
        if name in existing:
            print(f"пропуск (уже есть): {name}", flush=True)
            continue
        print(f"заливаю {name}", flush=True)
        started = time.time()
        with open(path, "rb") as fh:
            r = requests.post(
                f"https://uploads.github.com/repos/{OWNER}/{REPO}/releases/{rid}/assets?name={name}",
                headers={**headers, "Content-Type": "application/octet-stream"},
                data=fh,
                timeout=(60, 1800),
            )
        if r.status_code >= 300:
            print(f"ОШИБКА {r.status_code}: {r.text[:300]}", flush=True)
            continue
        print(f"готово {name} за {time.time() - started:.0f} с", flush=True)

    print("готово", flush=True)


if __name__ == "__main__":
    main()
