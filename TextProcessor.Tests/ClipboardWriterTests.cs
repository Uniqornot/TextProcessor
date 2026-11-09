using System.Runtime.InteropServices;
using TextProcessor.Services;
using Xunit;

namespace TextProcessor.Tests;

public class ClipboardWriterTests
{
    [Fact]
    public void MissingOwner_DoesNotTouchClipboard()
    {
        using var api = new FakeClipboard();
        Assert.False(ClipboardWriter.TrySetText("text", IntPtr.Zero, api));
        Assert.Empty(api.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Привет 😀\r\n𠀀")]
    public void Success_PassesOwnerAndTransfersNullTerminatedUnicodeText(string text)
    {
        using var api = new FakeClipboard();
        Assert.True(ClipboardWriter.TrySetText(text, (IntPtr)123, api));
        Assert.Equal((IntPtr)123, api.Owner);
        Assert.Equal(text, api.CopiedText);
        Assert.Equal((ulong)(text.Length + 1) * 2, api.AllocatedBytes);
        Assert.Equal(new[] { "allocate", "lock", "unlock", "open", "empty", "set", "close" }, api.Calls);
        Assert.Equal(0, api.FreeCount); // System owns memory after SetClipboardData succeeds.
    }

    [Theory]
    [InlineData("allocate")]
    [InlineData("lock")]
    [InlineData("open")]
    [InlineData("empty")]
    [InlineData("set")]
    public void NativeFailure_ReleasesOnlyResourcesWeOwn(string failure)
    {
        using var api = new FakeClipboard { Failure = failure };
        Assert.False(ClipboardWriter.TrySetText("text", (IntPtr)123, api));
        Assert.Equal(failure == "allocate" ? 0 : 1, api.FreeCount);
        Assert.Equal(failure is "empty" or "set" ? 1 : 0, api.Calls.Count(c => c == "close"));
        if (failure is "allocate" or "lock" or "open")
            Assert.DoesNotContain("empty", api.Calls);
        if (failure == "open")
            Assert.Equal(50, api.Calls.Count(c => c == "open"));
    }

    [Fact]
    public void BusyClipboard_RetriesAndClosesOnce()
    {
        using var api = new FakeClipboard { BusyAttempts = 2 };
        Assert.True(ClipboardWriter.TrySetText("text", (IntPtr)123, api));
        Assert.Equal(3, api.Calls.Count(c => c == "open"));
        Assert.Equal(2, api.Calls.Count(c => c == "wait"));
        Assert.Equal(1, api.Calls.Count(c => c == "close"));
    }

    private sealed class FakeClipboard : IClipboardApi, IDisposable
    {
        public string? Failure { get; init; }
        public int BusyAttempts { get; set; }
        public List<string> Calls { get; } = new();
        public IntPtr Owner { get; private set; }
        public ulong AllocatedBytes { get; private set; }
        public string? CopiedText { get; private set; }
        public int FreeCount { get; private set; }
        private IntPtr memory;

        public IntPtr Allocate(UIntPtr bytes)
        {
            Calls.Add("allocate");
            AllocatedBytes = bytes.ToUInt64();
            return Failure == "allocate" ? IntPtr.Zero : memory = Marshal.AllocHGlobal((int)AllocatedBytes);
        }
        public IntPtr Lock(IntPtr handle) { Calls.Add("lock"); return Failure == "lock" ? IntPtr.Zero : handle; }
        public void Unlock(IntPtr handle) => Calls.Add("unlock");
        public void Free(IntPtr handle) { Calls.Add("free"); FreeCount++; Marshal.FreeHGlobal(handle); memory = IntPtr.Zero; }
        public bool Open(IntPtr owner) { Calls.Add("open"); Owner = owner; return Failure != "open" && BusyAttempts-- <= 0; }
        public bool Empty() { Calls.Add("empty"); return Failure != "empty"; }
        public IntPtr SetUnicodeText(IntPtr handle)
        {
            Calls.Add("set");
            CopiedText = Marshal.PtrToStringUni(handle);
            return Failure == "set" ? IntPtr.Zero : handle;
        }
        public void Close() => Calls.Add("close");
        public void WaitForRetry() => Calls.Add("wait");
        public void Dispose() { if (memory != IntPtr.Zero) Marshal.FreeHGlobal(memory); }
    }
}
