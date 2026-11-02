using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClassSchedule;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly string key;
    private readonly uint message;
    private HwndSource? source;
    private Window? main;
    private Action? restore;
    public bool IsPrimary { get; }

    public SingleInstance(string key = "ClassSchedule.Desktop.v1")
    {
        this.key = key; message = RegisterWindowMessage(key);
        mutex = new Mutex(false, @"Local\" + key);
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void Attach(Window window, Action? restore = null)
    {
        main = window; this.restore = restore;
        var handle = new WindowInteropHelper(window).EnsureHandle();
        source = HwndSource.FromHwnd(handle); source.AddHook(Receive);
        SetProp(handle, key, new IntPtr(1));
        window.Closed += (_, _) => RemoveProp(handle, key);
    }

    public bool ActivateExisting()
    {
        // The first process may still be creating its main window.
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var found = IntPtr.Zero;
            EnumWindows((handle, _) =>
            {
                if (GetProp(handle, key) == IntPtr.Zero) return true;
                found = handle; return false;
            }, IntPtr.Zero);
            if (found != IntPtr.Zero)
            {
                GetWindowThreadProcessId(found, out var processId);
                AllowSetForegroundWindow(processId);
                return PostMessage(found, message, IntPtr.Zero, IntPtr.Zero);
            }
            Thread.Sleep(50);
        }
        return false;
    }

    private IntPtr Receive(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg != message || main is null) return IntPtr.Zero;
        if (restore is not null) { restore(); handled = true; return IntPtr.Zero; }
        main.Show();
        if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
        var target = main;
        while (target.OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible) is { } child) target = child;
        if (target.WindowState == WindowState.Minimized) target.WindowState = WindowState.Normal;
        target.Activate(); SetForegroundWindow(new WindowInteropHelper(target).Handle);
        handled = true; return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (source is { IsDisposed: false }) source.RemoveHook(Receive);
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
    }

    private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr hwnd, string name, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr hwnd, string name);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
