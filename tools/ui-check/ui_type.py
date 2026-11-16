"""Ставит текст в поле чата по AutomationId и жмёт «Отправить»."""

import sys
import time

import comtypes.client

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

text = sys.argv[1]
automation_id = sys.argv[2] if len(sys.argv) > 2 else "Input"

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
true_condition = root.CreateTrueCondition()
desktop = root.GetRootElement()

found = []


def walk(element, depth=0):
    if depth > 10:
        return
    try:
        ident = element.CurrentAutomationId or ""
        kind = element.CurrentControlType
        if kind == UIA.UIA_EditControlTypeId:
            print("edit:", repr(element.CurrentName), "id:", repr(ident))
        if ident == automation_id:
            found.append(element)
        children = element.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            walk(children.GetElement(i), depth + 1)
    except Exception:
        pass


windows = desktop.FindAll(UIA.TreeScope_Children, true_condition)
target_window = None
for i in range(windows.Length):
    window = windows.GetElement(i)
    if (window.CurrentName or "") == "Kapsula":
        target_window = window
        walk(window)

if target_window is None:
    print("окно капсулы не найдено")
    sys.exit(1)

if not found:
    print("поле не найдено по id:", automation_id)
    sys.exit(1)

element = found[0]
pattern = element.GetCurrentPattern(UIA.UIA_ValuePatternId).QueryInterface(UIA.IUIAutomationValuePattern)
pattern.SetValue(text)
time.sleep(0.5)

# кнопка отправки
button = []


def find_button(node, depth=0):
    if depth > 10 or button:
        return
    try:
        if (node.CurrentName or "") == "Отправить" and node.CurrentControlType == UIA.UIA_ButtonControlTypeId:
            button.append(node)
            return
        children = node.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            find_button(children.GetElement(i), depth + 1)
    except Exception:
        pass


find_button(target_window)
if not button:
    print("кнопка «Отправить» не найдена")
    sys.exit(1)

button[0].GetCurrentPattern(UIA.UIA_InvokePatternId).QueryInterface(UIA.IUIAutomationInvokePattern).Invoke()
print("отправлено:", text)
