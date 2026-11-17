"""Проверка программного затемнения: SetDeviceGammaRamp работает ли на этом экране."""

import ctypes as C
from ctypes import wintypes as W

u = C.windll.user32
g = C.windll.gdi32

DC = u.GetDC(None)

Ramp = W.WORD * (256 * 3)
ramp = Ramp()
ok_read = g.GetDeviceGammaRamp(DC, C.byref(ramp))
print("чтение гаммы:", bool(ok_read), "значение 128:", ramp[128], ramp[256 + 128], ramp[512 + 128])

for factor in (0.6, 1.0):
    test = Ramp()
    for i in range(256):
        v = min(65535, round(i * 257 * factor))
        test[i] = test[256 + i] = test[512 + i] = v
    ok = g.SetDeviceGammaRamp(DC, C.byref(test))
    check = Ramp()
    g.GetDeviceGammaRamp(DC, C.byref(check))
    print(f"фактор {factor}: установка {bool(ok)}, после установки в точке 128 {check[128]}")

u.ReleaseDC(None, DC)
