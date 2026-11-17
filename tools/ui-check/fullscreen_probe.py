"""Полноэкранное окно на 20 секунд — проверка игровой полоски капсулы."""

import tkinter as tk

root = tk.Tk()
root.overrideredirect(True)
w, h = root.winfo_screenwidth(), root.winfo_screenheight()
root.geometry(f"{w}x{h}+0+0")
root.attributes("-topmost", True)
root.configure(bg="#101010")
root.after(20000, root.destroy)
root.mainloop()
