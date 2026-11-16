import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32
found = []


def callback(hwnd, lparam):
    if not user32.IsWindowVisible(hwnd):
        return True
    name = ctypes.create_unicode_buffer(256)
    user32.GetWindowTextW(hwnd, name, 256)
    rect = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    title = name.value
    if title and ("Kapsula" in title or "апсул" in title or "Знакомство" in title):
        found.append((title, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top))
    return True


proc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)(callback)
user32.EnumWindows(proc, 0)

for title, left, top, width, height in found:
    print(f"{title!r}  x={left} y={top} {width}x{height}")

if not found:
    print("окон капсулы не найдено")
