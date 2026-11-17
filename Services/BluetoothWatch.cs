using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Kapsula.Services;

/// <summary>
/// Следит за Bluetooth-устройствами: когда наушники или колонка подключаются, показываем плашку.
/// Без адаптера наблюдатель просто ничего не находит.
/// </summary>
public static class BluetoothWatch
{
    private static DeviceWatcher? _watcher;
    private static DateTime _startedAt;

    public static bool Available => _watcher is not null;

    public static void Start(Action<string> connected)
    {
        if (_watcher is not null) return;

        try
        {
            var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
            _watcher = DeviceInformation.CreateWatcher(selector);
            _startedAt = DateTime.UtcNow;

            _watcher.Added += (_, info) =>
            {
                // первые секунды наблюдатель перечисляет уже подключённое — про это не сообщаем
                if ((DateTime.UtcNow - _startedAt).TotalSeconds < 4) return;
                var name = string.IsNullOrWhiteSpace(info.Name) ? "устройство" : info.Name;
                connected(name);
            };

            _watcher.Start();
        }
        catch
        {
            _watcher = null;
        }
    }
}
