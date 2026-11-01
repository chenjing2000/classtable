using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClassSchedule.Views;

internal static class WindowPlacement
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int value);
    public static void DisableMaximize(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        SetWindowLong(handle, -16, GetWindowLong(handle, -16) & ~0x10000);
    }
    internal static bool CanMaximize(Window window) => (GetWindowLong(new WindowInteropHelper(window).Handle, -16) & 0x10000) != 0;
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    public static Rect WorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget is null || !GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) return SystemParameters.WorkArea;
        var transform = source.CompositionTarget.TransformFromDevice;
        return new Rect(transform.Transform(new Point(info.Work.Left, info.Work.Top)), transform.Transform(new Point(info.Work.Right, info.Work.Bottom)));
    }
}
