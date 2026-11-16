import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32
rows = []


def callback(hwnd, lparam):
    if not user32.IsWindowVisible(hwnd):
        return True
    length = user32.GetWindowTextLengthW(hwnd)
    if length == 0:
        return True
    name = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(hwnd, name, length + 1)
    import os

    pid = wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    rows.append((pid.value, name.value))
    return True


proc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)(callback)
user32.EnumWindows(proc, 0)

import subprocess

for pid, title in rows:
    if title.strip():
        print(pid, "|", title)
