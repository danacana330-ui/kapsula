using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class SystemPanel : UserControl
{
    public event Action? Back;

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(2) };

    public SystemPanel()
    {
        InitializeComponent();
        Render();
        _tick.Tick += (_, _) => Render();
        _tick.Start();
        Unloaded += (_, _) => _tick.Stop();
    }

    private void Render()
    {
        var cpu = SysInfo.CpuPercent();
        CpuText.Text = $"{cpu}%";
        CpuBar.Value = cpu;

        var (used, total) = SysInfo.Memory();
        var memoryShare = total > 0 ? used / total * 100 : 0;
        MemoryText.Text = total > 0 ? $"ОЗУ {used:0.0} из {total:0.0} ГБ" : "ОЗУ —";
        MemoryBar.Value = memoryShare;

        var (free, diskTotal) = SysInfo.Disk();
        var diskShare = diskTotal > 0 ? (diskTotal - free) / diskTotal * 100 : 0;
        DiskText.Text = diskTotal > 0 ? $"диск: свободно {free:0.0} из {diskTotal:0} ГБ" : "диск —";
        DiskBar.Value = diskShare;

        var (percent, charging, hasBattery) = SysInfo.Battery();
        if (!hasBattery)
        {
            BatteryText.Text = "батареи нет";
            BatteryBar.Value = 0;
            return;
        }

        BatteryText.Text = charging ? $"батарея {percent}%, на зарядке" : $"батарея {percent}%";
        BatteryBar.Value = percent;
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        Back?.Invoke();
    }
}
