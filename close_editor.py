import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32
WM_CLOSE = 0x0010
targets = []


def callback(hwnd, lparam):
    if not user32.IsWindowVisible(hwnd):
        return True
    length = user32.GetWindowTextLengthW(hwnd)
    if length == 0:
        return True
    name = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(hwnd, name, length + 1)
    if name.value.strip() == "Снимок экрана":
        targets.append(hwnd)
    return True


proc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)(callback)
user32.EnumWindows(proc, 0)

for hwnd in targets:
    user32.PostMessageW(hwnd, WM_CLOSE, 0, 0)

print("закрыто окон:", len(targets))
