using System.Runtime.InteropServices;

namespace Kapsula;

/// <summary>Тонкая обёртка над системным микшером Windows (IAudioEndpointVolume).</summary>
public static class VolumeService
{
    public static float Get() => WithEndpoint(ep =>
    {
        ep.GetMasterVolumeLevelScalar(out var v);
        return v;
    }, 1f);

    public static bool IsMuted() => WithEndpoint(ep =>
    {
        ep.GetMute(out var m);
        return m != 0;
    }, false);

    public static void Set(float value) => WithEndpoint(ep =>
    {
        ep.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), Guid.Empty);
        return 0;
    }, 0);

    public static void ToggleMute() => WithEndpoint(ep =>
    {
        ep.GetMute(out var m);
        ep.SetMute(m == 0 ? 1 : 0, Guid.Empty);
        return 0;
    }, 0);

    private static T WithEndpoint<T>(Func<IAudioEndpointVolume, T> action, T fallback)
    {
        IAudioEndpointVolume? ep = null;
        try
        {
            ep = GetEndpoint();
            return action(ep);
        }
        catch
        {
            return fallback;
        }
        finally
        {
            if (ep is not null) Marshal.ReleaseComObject(ep);
        }
    }

    private static IAudioEndpointVolume GetEndpoint()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device));
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var obj));
                return (IAudioEndpointVolume)obj;
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private enum EDataFlow { Render = 0, Capture = 1, All = 2 }
    private enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int NotImpl1();
        int NotImpl2();
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetMute(int bMute, Guid pguidEventContext);
        [PreserveSig] int GetMute(out int pbMute);
    }
}
