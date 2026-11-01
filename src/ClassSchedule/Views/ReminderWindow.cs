using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

internal sealed class ReminderWindow : DialogWindow
{
    private readonly TextBlock details = Ui.Text("");
    private readonly DispatcherTimer flash = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool dark;
    internal bool IsFlashing => flash.IsEnabled;
    public ReminderWindow(Window owner, bool effects) : base("课程表 · 上课提醒", 380, 260, false)
    {
        Owner = owner; MinWidth = 300; MinHeight = 200; Topmost = effects;
        Body(Ui.Column(Ui.Text("上课提醒", 20), details));
        Footer.Children.Add(Ui.Button("关闭提醒", Close));
        flash.Tick += (_, _) =>
        {
            dark = !dark;
            Background = dark ? new SolidColorBrush(Color.FromRgb(37, 74, 114)) : Brushes.White;
            Foreground = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(23, 34, 59));
        };
        Loaded += (_, _) => { if (effects) { flash.Start(); FlashTaskbar(true); } };
        Closed += (_, _) => { flash.Stop(); Topmost = false; FlashTaskbar(false); };
    }
    public void Display(IEnumerable<ReminderNotice> notices) => details.Text = string.Join("\n\n", notices.Select(n =>
        $"{n.StudentName} · {n.CourseName}\n{n.ClassStart:MM-dd HH:mm}　{n.Location}\n第 {n.Number}/{n.Total} 次提醒"));

    [StructLayout(LayoutKind.Sequential)] private struct FlashInfo { public uint Size; public IntPtr Window; public uint Flags, Count, Timeout; }
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FlashInfo info);
    private void FlashTaskbar(bool enabled)
    {
        var info = new FlashInfo { Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = new WindowInteropHelper(this).Handle, Flags = enabled ? 15u : 0u };
        FlashWindowEx(ref info);
    }
}
