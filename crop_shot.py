import sys

import pyautogui
from PIL import Image

path = sys.argv[1]
box = tuple(int(v) for v in sys.argv[2:6])  # left top right bottom
shot = pyautogui.screenshot(region=box)
shot = shot.resize((shot.width * 2, shot.height * 2), Image.LANCZOS)
shot.save(path)
print(f"сохранено {path} {shot.size}")
