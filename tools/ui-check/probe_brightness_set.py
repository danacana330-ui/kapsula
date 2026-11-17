"""Проверка: принимает ли панель SetMonitorBrightness (Dxva2), даже если чтение не работает."""

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
    print("нет физического монитора")
    raise SystemExit(1)

mon = arr[0]
for value in (50, 100):
    ok = d.SetMonitorBrightness(mon.handle, W.DWORD(value))
    print("SetMonitorBrightness", value, "->", bool(ok), "код ошибки:", C.windll.kernel32.GetLastError())

low, cur, high = W.DWORD(), W.DWORD(), W.DWORD()
print("GetMonitorBrightness ->", bool(d.GetMonitorBrightness(mon.handle, C.byref(low), C.byref(cur), C.byref(high))), cur.value)

d.DestroyPhysicalMonitors(count, arr)
