"""Читает дерево интерфейса окон Kapsula через UIAutomation — текстом, без картинок."""

import sys

import comtypes.client

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

comtypes.client.GetModule("UIAutomationCore.dll")
from comtypes.gen import UIAutomationClient as UIA  # noqa: E402

MAX_DEPTH = int(sys.argv[1]) if len(sys.argv) > 1 else 6
FILTER = sys.argv[2] if len(sys.argv) > 2 else "Kapsula"

root = comtypes.client.CreateObject(UIA.CUIAutomation, interface=UIA.IUIAutomation)
true_condition = root.CreateTrueCondition()
desktop = root.GetRootElement()


def walk(element, depth=0, lines=None):
    if lines is None:
        lines = []
    if depth > MAX_DEPTH:
        return lines
    try:
        name = element.CurrentName or ""
        kind = element.CurrentControlType
        value = ""
        try:
            pattern = element.GetCurrentPattern(UIA.UIA_ValuePatternId)
            value = pattern.CurrentValue or ""
        except Exception:
            pass
        try:
            toggle = element.GetCurrentPattern(UIA.UIA_TogglePatternId)
            value += f" toggle={toggle.CurrentToggleState}"
        except Exception:
            pass
        text = name or value
        if text:
            lines.append("  " * depth + f"{text}" + (f"   [value: {value}]" if value and value != name else ""))
        else:
            lines.append("  " * depth + f"<{kind}>")
        children = element.FindAll(UIA.TreeScope_Children, true_condition)
        for i in range(children.Length):
            walk(children.GetElement(i), depth + 1, lines)
    except Exception:
        pass
    return lines


windows = desktop.FindAll(UIA.TreeScope_Children, true_condition)
printed = False
for i in range(windows.Length):
    window = windows.GetElement(i)
    name = window.CurrentName or ""
    if FILTER.lower() in name.lower():
        print("=" * 70)
        print("ОКНО:", name)
        print("=" * 70)
        for line in walk(window):
            print(line)
        printed = True

if not printed:
    print("окно не найдено")
