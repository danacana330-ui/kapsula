"""Нажимает элемент интерфейса по его подписи через UIAutomation."""

import sys
import time

import comtypes.client

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

name_wanted = sys.argv[1]
window_filter = sys.argv[2] if len(sys.argv) > 2 else "Kapsula"

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
true_condition = root.CreateTrueCondition()
desktop = root.GetRootElement()


def find(element, depth=0):
    if depth > 8:
        return None
    try:
        if (element.CurrentName or "") == name_wanted:
            return element
        if (element.CurrentHelpText or "") == name_wanted:
            return element
        children = element.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            found = find(children.GetElement(i), depth + 1)
            if found is not None:
                return found
    except Exception:
        pass
    return None


target = None
windows = desktop.FindAll(UIA.TreeScope_Children, true_condition)
for i in range(windows.Length):
    window = windows.GetElement(i)
    if window_filter.lower() in (window.CurrentName or "").lower():
        target = find(window)
        if target is not None:
            break

if target is None:
    print("не найдено:", name_wanted)
    sys.exit(1)

try:
    pattern = target.GetCurrentPattern(UIA.UIA_InvokePatternId)
    pattern.Invoke()
    print("нажато:", name_wanted)
except Exception as error:
    # не кнопка — пробуем щелчок по центру
    try:
        rect = target.CurrentBoundingRectangle
        import pyautogui

        pyautogui.click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2)
        print("щелчок по:", name_wanted)
    except Exception as second:
        print("не удалось нажать:", name_wanted, error, second)
        sys.exit(1)

time.sleep(1.2)
