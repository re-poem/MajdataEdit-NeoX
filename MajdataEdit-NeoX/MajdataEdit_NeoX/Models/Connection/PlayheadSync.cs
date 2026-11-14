using EasyShm;
using MajdataEdit_NeoX.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Threading;

namespace MajdataEdit_NeoX.Models.Connection;

internal sealed class PlayheadSync
{
    private static readonly bool PlatformSupported =
        !(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsBrowser());

    private readonly ISharedMemory<double>? _shm;

    public bool TryRead(out double value)
    {
        value = 0;
        if (!PlatformSupported) return false;
        if (_shm == null) return false;

        value = _shm.Span[0];
        return true;
    }
    public bool TryWrite(double value)
    {
        if (!PlatformSupported) return false;
        if (_shm == null) return false;

        _shm.Span[0] = (float)value;
        return true;
    }

    public PlayheadSync(string shmName)
    {
        if (!PlatformSupported) return;
        try
        {
            _shm = SharedMemory.CreateOrOpen<double>(shmName, 1);
        }
        catch { }
    }
}
