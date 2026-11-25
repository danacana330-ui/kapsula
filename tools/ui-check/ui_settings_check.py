"""Капсула уже раскрыта: открыть «Настройки» и снять дерево панели."""
import sys
import time

import uiautomation as auto

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

win = auto.WindowControl(searchDepth=1, Name="Kapsula")
if not win.Exists(3):
    print("окно капсулы не найдено")
    sys.exit(1)

target = None
for item, _ in auto.WalkControl(win, maxDepth=12):
    c = item[0] if isinstance(item, tuple) else item
    if c.Name == "Настройки":
        target = c
        break

if target is None:
    print("«Настройки» не найдены — капсула не раскрыта?")
    sys.exit(1)

target.Click()
time.sleep(1.2)

print("── дерево панели настроек ──")
for item, depth in auto.WalkControl(win, maxDepth=14):
    c = item[0] if isinstance(item, tuple) else item
    if not c.Name:
        continue
    r = c.BoundingRectangle
    print("  " * depth, c.ControlTypeName, "|", c.Name, "|",
          r.left, r.top, r.right - r.left, r.bottom - r.top)