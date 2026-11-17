"""Закрывает панель: жмёт кнопку «назад» слева от заголовка панели."""

import sys
import time

import comtypes.client
import pyautogui

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

title = sys.argv[1] if len(sys.argv) > 1 else "Музыка"
pyautogui.FAILSAFE = False

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
condition = root.CreateTrueCondition()
desktop = root.GetRootElement()


def find(element, depth=0):
    if depth > 10:
        return None
    try:
        if (element.CurrentName or "") == title:
            return element
        children = element.FindAll(UIA.TreeScope_Children, condition)
        for i in range(children.Length):
            found = find(children.GetElement(i), depth + 1)
            if found is not None:
                return found
    except Exception:
        pass
    return None


target = None
windows = desktop.FindAll(UIA.TreeScope_Children, condition)
for i in range(windows.Length):
    window = windows.GetElement(i)
    if "kapsula" in (window.CurrentName or "").lower():
        target = find(window)
        if target is not None:
            break

if target is None:
    print("заголовок панели не найден")
    sys.exit(1)

rect = target.CurrentBoundingRectangle
pyautogui.click(rect.left - 26, (rect.top + rect.bottom) // 2)
print("нажал назад у панели", title)
time.sleep(1.2)
