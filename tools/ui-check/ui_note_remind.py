"""Проверка календаря: создаёт заметку с напоминанием и ставит фильтр по дню."""

import time

import comtypes.client

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
condition = root.CreateTrueCondition()
desktop = root.GetRootElement()

edits = []
named = {}


def walk(element, depth=0):
    if depth > 12:
        return
    try:
        kind = element.CurrentControlType
        if kind == UIA.UIA_EditControlTypeId:
            edits.append(element)
        if (element.CurrentName or "") and (element.CurrentName or "") not in named:
            named[element.CurrentName] = element
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

if window is None:
    print("окно не найдено")
    raise SystemExit(1)

walk(window)


def set_value(element, text):
    pattern = element.GetCurrentPattern(UIA.UIA_ValuePatternId).QueryInterface(UIA.IUIAutomationValuePattern)
    pattern.SetValue(text)
    time.sleep(0.3)


def invoke(name):
    element = named.get(name)
    if element is None:
        print("нет кнопки", name)
        return False
    element.GetCurrentPattern(UIA.UIA_InvokePatternId).QueryInterface(UIA.IUIAutomationInvokePattern).Invoke()
    time.sleep(0.6)
    return True


text = "проверка календаря"
set_value(edits[0], text)
invoke("Записать")

# выбрать заметку в списке
row = named.get(text)
if row is not None:
    try:
        row.GetCurrentPattern(UIA.UIA_SelectionItemPatternId).QueryInterface(
            UIA.IUIAutomationSelectionItemPattern).Select()
        print("заметка выбрана")
    except Exception as error:
        print("не удалось выбрать заметку:", error)
time.sleep(0.4)

set_value(edits[1], "18.11 09:00")
invoke("Поставить")
print("напоминание поставлено")
