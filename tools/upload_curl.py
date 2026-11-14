"""Заливка больших файлов в релиз GitHub через curl (устойчиво к обрывам).

Использование: python upload_curl.py <tag> <file> [...]
Токен читается из хранилища Windows через `git credential fill` и передаётся curl
через временный файл заголовков, чтобы не светить его в списке процессов и в переписке.
"""
import json
import os
import subprocess
import sys

OWNER = "danacana330-ui"
REPO = "kapsula"
API = "https://api.github.com"


def token():
    out = subprocess.run(
        ["git", "credential", "fill"],
        input="protocol=https\nhost=github.com\n\n",
        capture_output=True, text=True, check=True,
    ).stdout
    for line in out.splitlines():
        if line.startswith("password="):
            return line[len("password="):]
    raise SystemExit("токен не найден в git credential")


def main():
    tag = sys.argv[1]
    files = sys.argv[2:]
    tok = token()

    header_file = os.path.join(os.environ["TEMP"], "gh_headers.txt")
    with open(header_file, "w", encoding="ascii") as fh:
        fh.write(f"Authorization: Bearer {tok}\n")
        fh.write("Accept: application/vnd.github+json\n")

    try:
        rel = subprocess.run(
            ["curl", "-sS", "-H", "@" + header_file,
             f"{API}/repos/{OWNER}/{REPO}/releases/tags/{tag}"],
            capture_output=True, text=True, check=True,
        ).stdout
        data = json.loads(rel)
        rid = data["id"]
        existing = [a["name"] for a in data.get("assets", [])]
        print(f"релиз {tag} id={rid}, уже залито: {existing or '—'}", flush=True)

        for path in files:
            name = os.path.basename(path)
            if name in existing:
                print(f"пропуск (уже есть): {name}", flush=True)
                continue

            print(f"заливаю {name}", flush=True)
            url = (f"https://uploads.github.com/repos/{OWNER}/{REPO}/releases/{rid}"
                   f"/assets?name={name}")
            r = subprocess.run(
                ["curl", "-sS", "--fail", "--retry", "5", "--retry-delay", "10",
                 "--retry-all-errors", "--connect-timeout", "30",
                 "-H", "@" + header_file,
                 "-H", "Content-Type: application/octet-stream",
                 "--data-binary", "@" + path,
                 url],
                capture_output=True, text=True,
            )
            if r.returncode != 0:
                print(f"ОШИБКА curl {r.returncode}: {r.stderr[:300]}", flush=True)
                continue
            try:
                asset = json.loads(r.stdout)
                print(f"готово {name}: {asset.get('state')} {asset.get('size')} байт", flush=True)
            except Exception:
                print(f"готово {name}: {r.stdout[:200]}", flush=True)
    finally:
        try:
            os.remove(header_file)
        except OSError:
            pass

    print("всё", flush=True)


if __name__ == "__main__":
    main()
