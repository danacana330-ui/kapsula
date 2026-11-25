"""Публикация релиза Kapsula 2.0.2 через REST API GitHub."""
import subprocess
import time

import requests

OWNER = "danacana330-ui"
REPO = "kapsula"
TAG = "v2.0.2"
API = "https://api.github.com"
ROOT = r"C:\Users\Asus\Desktop\Kapsula\release"

BODY = """# Kapsula 2.0.2

Настройки капсулы стали карточками с тумблерами — вместо списка с галочками.

![Kapsula 2.0](https://github.com/danacana330-ui/kapsula/releases/download/v2.0.0/capsule_2.0.png)

## Что нового

- **Настройки — карточками.** Каждая строка — карточка: подпись слева, янтарный тумблер справа.
  Поля, ползунок размера и горячие клавиши тоже лежат в карточках, разделы слева стали скруглёнными.
  Настройки не менялись — только вид.
- Тумблер включённого пункта подсвечен янтарным, как всё живое в капсуле.

## Как обновиться

- Капсула сама предложит новую версию (проверка раз в сутки) — согласитесь, и она скачает
  файл и перезапустится.
- Новая установка: скачайте `Kapsula-2.0.2.zip`, распакуйте в любую папку и запустите `Kapsula.exe`.
  SmartScreen может спросить подтверждение — «Подробнее» → «Выполнить в любом случае».

Горячие клавиши: Alt+Space — панель запуска, Alt+S — скриншот, Alt+R — запись экрана.

sha256: 61eeb4eeea518f0e3216fb9058c18c5f0ded181bf35d96bde057517beeab5904
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
            json={"tag_name": TAG, "name": "Kapsula 2.0.2", "body": BODY, "draft": False},
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
        (ROOT + r"\Kapsula-2.0.2\Kapsula.exe", "Kapsula.exe"),
        (ROOT + r"\Kapsula-2.0.2.zip", "Kapsula-2.0.2.zip"),
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
                timeout=(60, 3600),
            )
        if r.status_code >= 300:
            print(f"ОШИБКА {r.status_code}: {r.text[:300]}", flush=True)
            continue
        spent = time.time() - started
        print(f"готово {name} за {spent / 60:.1f} мин", flush=True)

    print("готово", flush=True)


if __name__ == "__main__":
    main()