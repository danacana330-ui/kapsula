"""Заливка файлов в релиз GitHub (gh CLI нет, работаем через REST API и токен из git).

Использование: python upload_release.py <tag> <file> [<file> ...]
Токен берётся из хранилища учётных данных Windows через `git credential fill`.
"""
import subprocess
import sys
import time

import requests

OWNER = "danacana330-ui"
REPO = "kapsula"
API = "https://api.github.com"


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
    tag = sys.argv[1]
    files = sys.argv[2:]
    headers = {"Authorization": "Bearer " + token(), "Accept": "application/vnd.github+json"}

    rel = requests.get(f"{API}/repos/{OWNER}/{REPO}/releases/tags/{tag}", headers=headers, timeout=60)
    if rel.status_code == 404:
        created = requests.post(
            f"{API}/repos/{OWNER}/{REPO}/releases",
            headers=headers,
            json={
                "tag_name": tag,
                "name": "Модели для Kapsula 2.0",
                "body": "Файлы мини-моделей Qwen2.5, которые капсула скачивает сама.\n"
                        "3B лежит двумя частями — приложение скачивает и склеивает их автоматически.",
            },
            timeout=120,
        )
        created.raise_for_status()
        rel = created
    rel.raise_for_status()
    data = rel.json()
    rid = data["id"]
    existing = {a["name"] for a in data.get("assets", [])}
    print(f"релиз {tag} id={rid}, уже залито: {sorted(existing) or '—'}", flush=True)

    for path in files:
        name = path.replace("\\", "/").rsplit("/", 1)[-1]
        if name in existing:
            print(f"пропуск (уже есть): {name}", flush=True)
            continue

        size = __import__("os").path.getsize(path)
        print(f"заливаю {name} ({size / 1024 / 1024:.0f} МБ)", flush=True)
        started = time.time()
        with open(path, "rb") as fh:
            r = requests.post(
                f"https://uploads.github.com/repos/{OWNER}/{REPO}/releases/{rid}/assets?name={name}",
                headers={**headers, "Content-Type": "application/octet-stream"},
                data=fh,
                timeout=(60, 3600),
            )
        if r.status_code >= 300:
            print(f"ОШИБКА {r.status_code}: {r.text[:400]}", flush=True)
            continue
        spent = time.time() - started
        print(f"готово {name} за {spent / 60:.1f} мин, "
              f"{size / 1024 / 1024 / max(spent, 1):.1f} МБ/с", flush=True)

    print("всё", flush=True)


if __name__ == "__main__":
    main()
