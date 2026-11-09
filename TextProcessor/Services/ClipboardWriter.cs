using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TextProcessor.Services;

/// <summary>Transfers Unicode text ownership to the Windows clipboard.</summary>
internal static class ClipboardWriter
{
    public static bool TrySetText(string value, IntPtr owner) =>
        TrySetText(value, owner, NativeClipboardApi.Instance);

    internal static bool TrySetText(string value, IntPtr owner, IClipboardApi api)
    {
        // EmptyClipboard after OpenClipboard(NULL) leaves no owner and SetClipboardData fails.
        // Reject this before allocating memory or changing the user's clipboard.
        if (owner == IntPtr.Zero)
            return false;

        value ??= string.Empty;
        var bytes = checked(((ulong)value.Length + 1) * 2);
        var memory = api.Allocate((UIntPtr)bytes);
        if (memory == IntPtr.Zero)
            return false;

        var opened = false;
        try
        {
            var target = api.Lock(memory);
            if (target == IntPtr.Zero)
                return false;
            try
            {
                Marshal.Copy(value.ToCharArray(), 0, target, value.Length);
                Marshal.WriteInt16(target, checked(value.Length * 2), 0);
            }
            finally
            {
                api.Unlock(memory);
            }

            // Prepare data before emptying the clipboard, so allocation/lock failures are harmless.
            for (var attempt = 0; attempt < 50; attempt++)
            {
                if (api.Open(owner))
                {
                    opened = true;
                    break;
                }
                api.WaitForRetry();
            }
            if (!opened || !api.Empty() || api.SetUnicodeText(memory) == IntPtr.Zero)
                return false;

            memory = IntPtr.Zero; // Windows now owns the allocation.
            return true;
        }
        finally
        {
            if (opened)
                api.Close();
            if (memory != IntPtr.Zero)
                api.Free(memory);
        }
    }
}

// A narrow seam for deterministic failure/ownership tests without modifying the real clipboard.
internal interface IClipboardApi
{
    IntPtr Allocate(UIntPtr bytes);
    IntPtr Lock(IntPtr memory);
    void Unlock(IntPtr memory);
    void Free(IntPtr memory);
    bool Open(IntPtr owner);
    bool Empty();
    IntPtr SetUnicodeText(IntPtr memory);
    void Close();
    void WaitForRetry();
}

internal sealed class NativeClipboardApi : IClipboardApi
{
    internal static readonly NativeClipboardApi Instance = new();
    public IntPtr Allocate(UIntPtr bytes) => GlobalAlloc(0x0002, bytes);
    public IntPtr Lock(IntPtr memory) => GlobalLock(memory);
    public void Unlock(IntPtr memory) => GlobalUnlock(memory);
    public void Free(IntPtr memory) => GlobalFree(memory);
    public bool Open(IntPtr owner) => OpenClipboard(owner);
    public bool Empty() => EmptyClipboard();
    public IntPtr SetUnicodeText(IntPtr memory) => SetClipboardData(13, memory);
    public void Close() => CloseClipboard();
    public void WaitForRetry() => Thread.Sleep(40);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
