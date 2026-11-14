"""Заменить файлы в существующем релизе и обновить sha256 в описании."""
import re
import subprocess
import sys
import time

import requests

OWNER = "danacana330-ui"
REPO = "kapsula"
API = "https://api.github.com"
ROOT = r"C:\Users\Asus\Desktop\Kapsula\release"


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
    files = [(ROOT + r"\Kapsula-2.0\Kapsula.exe", "Kapsula.exe"),
             (ROOT + r"\Kapsula-2.0.zip", "Kapsula-2.0.zip")]
    new_sha = sys.argv[2] if len(sys.argv) > 2 else ""

    headers = {"Authorization": "Bearer " + token(), "Accept": "application/vnd.github+json"}
    rel = requests.get(f"{API}/repos/{OWNER}/{REPO}/releases/tags/{tag}", headers=headers, timeout=60)
    rel.raise_for_status()
    data = rel.json()
    rid = data["id"]

    for asset in data.get("assets", []):
        if asset["name"] in [name for _, name in files]:
            r = requests.delete(f"{API}/repos/{OWNER}/{REPO}/releases/assets/{asset['id']}",
                                headers=headers, timeout=60)
            print(f"удалён старый {asset['name']}: {r.status_code}", flush=True)

    for path, name in files:
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

    if new_sha:
        body = data.get("body") or ""
        body = re.sub(r"sha256:\s*[0-9a-fA-F]{64}", "sha256: " + new_sha, body, count=1)
        r = requests.patch(f"{API}/repos/{OWNER}/{REPO}/releases/{rid}",
                           headers=headers, json={"body": body}, timeout=120)
        print(f"описание обновлено: {r.status_code}", flush=True)

    print("готово", flush=True)


if __name__ == "__main__":
    main()
