"""Пишет текст в поле ввода капсулы и нажимает кнопку отправки."""

import sys
import time

import comtypes.client
import pyautogui

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

text = sys.argv[1]

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
true_condition = root.CreateTrueCondition()
desktop = root.GetRootElement()

edits = []


def walk(element, depth=0):
    if depth > 10:
        return
    try:
        if element.CurrentControlType == UIA.UIA_EditControlTypeId and element.CurrentIsEnabled:
            rect = element.CurrentBoundingRectangle
            if rect.right - rect.left > 40:
                edits.append((element, rect))
        children = element.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            walk(children.GetElement(i), depth + 1)
    except Exception:
        pass


windows = desktop.FindAll(UIA.TreeScope_Children, true_condition)
for i in range(windows.Length):
    window = windows.GetElement(i)
    if "Kapsula" == (window.CurrentName or ""):
        walk(window)

if not edits:
    print("поле ввода не найдено")
    sys.exit(1)

element, rect = edits[-1]
pyautogui.click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2)
time.sleep(0.4)

# кириллица — через буфер обмена, посимвольный ввод её ломает
import tkinter

clip = tkinter.Tk()
clip.withdraw()
clip.clipboard_clear()
clip.clipboard_append(text)
clip.update()
clip.destroy()

pyautogui.hotkey("ctrl", "v")
time.sleep(0.6)
pyautogui.press("enter")
print("отправлено:", text)
