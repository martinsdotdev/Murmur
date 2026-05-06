using System;
using System.Runtime.InteropServices;

namespace Murmur.Services;

/// <summary>Asserts SYSTEM-required (CPU stays awake; screen can sleep) via Win32
/// PowerCreateRequest / PowerSetRequest. Calls are idempotent.</summary>
public sealed class PowerService : IDisposable
{
    private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

    private IntPtr _request = IntPtr.Zero;

    public bool IsActive => _request != IntPtr.Zero;

    public void Activate(string reason = "Murmur is playing ambient audio")
    {
        if (IsActive) return;

        var ctx = new REASON_CONTEXT
        {
            Version = POWER_REQUEST_CONTEXT_VERSION,
            Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
            SimpleReasonString = reason,
        };

        var handle = PowerCreateRequest(ref ctx);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;

        if (!PowerSetRequest(handle, PowerRequestType.SystemRequired))
        {
            CloseHandle(handle);
            return;
        }
        _request = handle;
        DiagnosticLog.Log("PowerService: SystemRequired asserted.");
    }

    public void Release()
    {
        if (!IsActive) return;
        PowerClearRequest(_request, PowerRequestType.SystemRequired);
        CloseHandle(_request);
        _request = IntPtr.Zero;
        DiagnosticLog.Log("PowerService: SystemRequired released.");
    }

    public void Dispose() => Release();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private enum PowerRequestType : uint
    {
        DisplayRequired = 0,
        SystemRequired = 1,
        AwayModeRequired = 2,
        ExecutionRequired = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString;
    }
}
