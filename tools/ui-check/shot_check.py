"""Скриншот панели настроек и проверка янтарных тумблеров по пикселям."""
import sys

from PIL import ImageGrab

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

shot = ImageGrab.grab(bbox=(600, 40, 1160, 730))
shot.save(r"C:\Users\Asus\Desktop\Kapsula\tools\ui-check\shot_settings_new.png")

rows = [
    ("автозапуск", 56),
    ("наведение", 108),
    ("не сворачивать", 160),
    ("секунды", 212),
    ("громкость/CapsLock", 264),
    ("полный экран", 316),
    ("маскот", 368),
    ("меньше анимации", 420),
]

for name, top in rows:
    region = shot.crop((485, top - 40, 533, top + 4))
    amber = sum(
        1
        for r, g, b in region.getdata()
        if r > 200 and 120 < g < 210 and b < 150
    )
    print(f"{name:22} янтарных пикселей в зоне тумблера: {amber}")

print("скриншот: tools\\ui-check\\shot_settings_new.png")