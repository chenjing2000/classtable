using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

internal sealed class ReminderWindow : DialogWindow
{
    private readonly TextBlock details = Centered("");
    private readonly TextBlock repetitions = Centered("");
    private readonly DispatcherTimer flash = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool dark;
    internal bool IsFlashing => flash.IsEnabled;
    public ReminderWindow(Window owner, bool effects, Action again) : base("上课提醒", 340, 260, false)
    {
        Owner = owner; MinWidth = 300; MinHeight = 200; Topmost = true;
        var title = Centered("上课提醒"); title.FontSize = 18; title.FontWeight = FontWeights.Bold;
        title.Foreground = repetitions.Foreground = new SolidColorBrush(Color.FromRgb(0xab, 0x1d, 0x22));
        Body(Ui.Column(title, details, Centered("请马上出发！"), repetitions));
        Footer.HorizontalAlignment = HorizontalAlignment.Center;
        var ok = Ui.Button("OK", Close); var repeat = Ui.Button("Later", again);
        ok.Width = repeat.Width = 72;
        ok.FontSize = repeat.FontSize = 16;
        Footer.Children.Add(ok); Footer.Children.Add(repeat);
        flash.Tick += (_, _) =>
        {
            dark = !dark;
            Background = dark ? new SolidColorBrush(Color.FromRgb(255, 241, 241)) : Brushes.White;
        };
        Loaded += (_, _) => { if (effects) { flash.Start(); FlashTaskbar(true); } };
        Closed += (_, _) => { flash.Stop(); Topmost = false; FlashTaskbar(false); };
    }
    public void Display(ReminderNotice notice)
    {
        details.Inlines.Clear();
        details.Inlines.Add(new Run($"{notice.ClassStart:HH:mm} "));
        details.Inlines.Add(new Run(notice.CourseName) { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xb8, 0x1a, 0x35)) });
        if (!string.IsNullOrWhiteSpace(notice.Location)) details.Inlines.Add(new Run("@" + notice.Location));
        repetitions.Text = $"第 {notice.Number}/{notice.Total} 次提醒";
    }
    private static TextBlock Centered(string text) => new()
    {
        Text = text, FontSize = 16, FontWeight = FontWeights.Normal, TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4)
    };

    [StructLayout(LayoutKind.Sequential)] private struct FlashInfo { public uint Size; public IntPtr Window; public uint Flags, Count, Timeout; }
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FlashInfo info);
    private void FlashTaskbar(bool enabled)
    {
        var info = new FlashInfo { Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = new WindowInteropHelper(this).Handle, Flags = enabled ? 15u : 0u };
        FlashWindowEx(ref info);
    }
}
