"""Кладёт текст в буфер обмена по-настоящему (CF_UNICODETEXT), без потери после выхода."""

import ctypes
import sys
import time
from ctypes import wintypes

text = sys.argv[1]

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

kernel32.GlobalAlloc.restype = ctypes.c_void_p
kernel32.GlobalAlloc.argtypes = [ctypes.c_uint, ctypes.c_size_t]
kernel32.GlobalLock.restype = ctypes.c_void_p
kernel32.GlobalLock.argtypes = [ctypes.c_void_p]
kernel32.GlobalUnlock.argtypes = [ctypes.c_void_p]
user32.SetClipboardData.restype = ctypes.c_void_p
user32.SetClipboardData.argtypes = [ctypes.c_uint, ctypes.c_void_p]

CF_UNICODETEXT = 13
GMEM_MOVEABLE = 0x0002

for _ in range(10):
    if user32.OpenClipboard(None):
        break
    time.sleep(0.1)
else:
    raise SystemExit("буфер занят")

try:
    user32.EmptyClipboard()
    data = text.encode("utf-16-le") + b"\x00\x00"
    handle = kernel32.GlobalAlloc(GMEM_MOVEABLE, len(data))
    pointer = kernel32.GlobalLock(handle)
    ctypes.memmove(pointer, data, len(data))
    kernel32.GlobalUnlock(handle)
    user32.SetClipboardData(CF_UNICODETEXT, handle)
finally:
    user32.CloseClipboard()

print("в буфере:", text)
