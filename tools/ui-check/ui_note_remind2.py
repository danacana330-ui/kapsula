"""Выбирает заметку щелчком по строке и ставит ей напоминание."""

import time

import comtypes.client
import pyautogui

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

pyautogui.FAILSAFE = False

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
condition = root.CreateTrueCondition()
desktop = root.GetRootElement()

edits = []
named = {}


def walk(element, depth=0):
    if depth > 12:
        return
    try:
        if element.CurrentControlType == UIA.UIA_EditControlTypeId:
            edits.append(element)
        name = element.CurrentName or ""
        if name and name not in named:
            named[name] = element
        children = element.FindAll(UIA.TreeScope_Children, condition)
        for i in range(children.Length):
            walk(children.GetElement(i), depth + 1)
    except Exception:
        pass


window = None
for i in range(desktop.FindAll(UIA.TreeScope_Children, condition).Length):
    candidate = desktop.FindAll(UIA.TreeScope_Children, condition).GetElement(i)
    if (candidate.CurrentName or "") == "Kapsula":
        window = candidate
        break

walk(window)

row = named.get("проверка календаря")
if row is None:
    print("строка заметки не найдена")
    raise SystemExit(1)

rect = row.CurrentBoundingRectangle
pyautogui.click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2)
time.sleep(0.6)

pattern = edits[1].GetCurrentPattern(UIA.UIA_ValuePatternId).QueryInterface(UIA.IUIAutomationValuePattern)
pattern.SetValue("18.11 09:00")
time.sleep(0.3)

named["Поставить"].GetCurrentPattern(UIA.UIA_InvokePatternId).QueryInterface(
    UIA.IUIAutomationInvokePattern).Invoke()
print("нажато «Поставить»")
time.sleep(0.8)
