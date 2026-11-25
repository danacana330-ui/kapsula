"""Кликает по точке экрана и/или печатает текст в капсуле.
Пример: python ui_act.py click 640 30
"""
import sys
import time

import pyautogui

cmd = sys.argv[1]
if cmd == "click":
    x, y = int(sys.argv[2]), int(sys.argv[3])
    pyautogui.click(x, y)
    print("клик", x, y)
elif cmd == "wheel":
    pyautogui.scroll(int(sys.argv[2]))
    print("колесо", sys.argv[2])
time.sleep(1.0)