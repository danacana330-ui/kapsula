"""Ставит значение в N-е поле окна Kapsula и нажимает кнопку по её названию.

Использование: python ui_set.py "0:40" "Старт" [номер_поля_с_конца]
"""

import sys
import time

import comtypes.client

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

value = sys.argv[1]
button_name = sys.argv[2]
index = int(sys.argv[3]) if len(sys.argv) > 3 else 0

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
true_condition = root.CreateTrueCondition()
desktop = root.GetRootElement()

edits = []
buttons = []


def walk(element, depth=0):
    if depth > 12:
        return
    try:
        kind = element.CurrentControlType
        if kind == UIA.UIA_EditControlTypeId:
            edits.append(element)
        elif kind == UIA.UIA_ButtonControlTypeId and (element.CurrentName or "") == button_name:
            buttons.append(element)
        children = element.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            walk(children.GetElement(i), depth + 1)
    except Exception:
        pass


window = None
for i in range(desktop.FindAll(UIA.TreeScope_Children, true_condition).Length):
    candidate = desktop.FindAll(UIA.TreeScope_Children, true_condition).GetElement(i)
    if (candidate.CurrentName or "") == "Kapsula":
        window = candidate
        break

if window is None:
    print("окно капсулы не найдено")
    sys.exit(1)

walk(window)

if not edits:
    print("поля не найдены")
    sys.exit(1)

target = edits[-1 - index] if len(edits) > index else edits[-1]
pattern = target.GetCurrentPattern(UIA.UIA_ValuePatternId).QueryInterface(UIA.IUIAutomationValuePattern)
pattern.SetValue(value)
time.sleep(0.4)

if not buttons:
    print("кнопка не найдена:", button_name)
    sys.exit(1)

buttons[0].GetCurrentPattern(UIA.UIA_InvokePatternId).QueryInterface(UIA.IUIAutomationInvokePattern).Invoke()
print(f"в поле {len(edits) - 1 - index} поставлено {value!r}, нажата «{button_name}»")
