import subprocess

result = subprocess.run(
    ["python", r"C:\Users\Asus\Desktop\Kapsula\ui_dump.py", "8", "Kapsula"],
    capture_output=True,
    text=True,
    encoding="utf-8",
    errors="replace",
)
lines = result.stdout.splitlines()
start = next((i for i, line in enumerate(lines) if "ChatBubble" in line), None)
print("\n".join(lines[start:]) if start is not None else result.stdout)
