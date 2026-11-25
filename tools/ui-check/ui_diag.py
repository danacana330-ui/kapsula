"""Диагностика: полный дамп имён окна капсулы, клик по времени, повторный дамп."""
import re
import sys
import time

import uiautomation as auto

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

win = auto.WindowControl(searchDepth=1, Name="Kapsula")
if not win.Exists(3):
    print("окно капсулы не найдено")
    sys.exit(1)


def dump(title):
    print(f"── {title} ──")
    for item, depth in auto.WalkControl(win, maxDepth=12):
        c = item[0] if isinstance(item, tuple) else item
        if not c.Name:
            continue
        r = c.BoundingRectangle
        print("  " * depth, c.ControlTypeName, "|", c.Name, "|",
              r.left, r.top, r.right - r.left, r.bottom - r.top)


dump("до клика")

clock = None
for item, _ in auto.WalkControl(win, maxDepth=12):
    c = item[0] if isinstance(item, tuple) else item
    if c.ControlTypeName == "TextControl" and re.fullmatch(r"\d{1,2}:\d{2}(:\d{2})?", c.Name or ""):
        clock = c
        break

if clock is None:
    print("часы не найдены")
    sys.exit(1)

clock.Click()
time.sleep(1.4)
dump("после клика по часам")