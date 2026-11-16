"""Проверка: поддерживает ли этот монитор программную яркость (Dxva2)."""

import ctypes as C
from ctypes import wintypes as W


class PHYSICAL_MONITOR(C.Structure):
    _fields_ = [("handle", W.HANDLE), ("description", W.WCHAR * 128)]


u = C.windll.user32
d = C.windll.dxva2

hmon = u.MonitorFromPoint(W.POINT(10, 10), 2)
count = W.DWORD()
if not d.GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, C.byref(count)) or count.value == 0:
    print("мониторы не найдены")
    raise SystemExit(1)

arr = (PHYSICAL_MONITOR * count.value)()
if not d.GetPhysicalMonitorsFromHMONITOR(hmon, count, arr):
    print("не удалось получить физический монитор")
    raise SystemExit(1)

mon = arr[0]
low, cur, high = W.DWORD(), W.DWORD(), W.DWORD()
ok = d.GetMonitorBrightness(mon.handle, C.byref(low), C.byref(cur), C.byref(high))
print("описание:", mon.description)
print("яркость доступна:", bool(ok), "значение:", cur.value, "диапазон:", low.value, "-", high.value)

if ok:
    target = (low.value + high.value) // 2
    set_ok = d.SetMonitorBrightness(mon.handle, target)
    print("установка", target, ":", bool(set_ok))
    if set_ok:
        d.GetMonitorBrightness(mon.handle, C.byref(low), C.byref(cur), C.byref(high))
        print("после установки:", cur.value)
        d.SetMonitorBrightness(mon.handle, high.value)

d.DestroyPhysicalMonitors(count, arr)
