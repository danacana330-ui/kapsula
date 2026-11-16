import ctypes
import time

import pyautogui

user32 = ctypes.windll.user32

VK_MENU = 0x12
VK_S = 0x53
KEYEVENTF_KEYUP = 0x0002


def press_alt_s():
    user32.keybd_event(VK_MENU, 0, 0, 0)
    time.sleep(0.05)
    user32.keybd_event(VK_S, 0, 0, 0)
    time.sleep(0.05)
    user32.keybd_event(VK_S, 0, KEYEVENTF_KEYUP, 0)
    time.sleep(0.05)
    user32.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)


press_alt_s()
time.sleep(2.0)

pyautogui.moveTo(500, 300)
pyautogui.mouseDown()
time.sleep(0.4)
pyautogui.moveTo(900, 650, duration=0.6)
time.sleep(0.3)
pyautogui.mouseUp()
time.sleep(2.5)

formats = [fmt for fmt in (2, 8, 17) if user32.IsClipboardFormatAvailable(fmt)]
print("форматы картинки в буфере:", formats if formats else "нет")
