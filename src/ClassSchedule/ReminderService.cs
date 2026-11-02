using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

namespace ClassSchedule;

internal sealed class ReminderService : IDisposable
{
    private readonly WorkspaceViewModel workspace;
    private readonly Window main;
    private readonly Action<string> report;
    private readonly Func<DateTime> clock;
    private readonly bool effects;
    private readonly Action restoreMain;
    private readonly ReminderSession session = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<(Guid Course, Guid Slot, DateTime Start), (ReminderNotice Notice, ReminderWindow Window)> active = [];
    private StudentSnapshot? previous;
    internal ReminderWindow? Alert => active.Values.Select(a => a.Window).FirstOrDefault();

    public ReminderService(WorkspaceViewModel workspace, Window main, Action<string> report, Func<DateTime>? clock = null, bool effects = true, Action? restoreMain = null)
    {
        this.workspace = workspace; this.main = main; this.report = report; this.clock = clock ?? (() => DateTime.Now); this.effects = effects;
        this.restoreMain = restoreMain ?? (() =>
        {
            main.ShowInTaskbar = true;
            if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
            main.Show(); if (effects) main.Activate();
        });
        workspace.PropertyChanged += WorkspaceChanged;
        timer.Tick += Tick; timer.Start(); Synchronize();
    }
    private void WorkspaceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Synchronize();
    private void Synchronize()
    {
        var current = workspace.Current;
        if (current?.Name == previous?.Name && current?.Fingerprint == previous?.Fingerprint) return;
        Dismiss(); previous = current;
        var now = clock(); session.Synchronize(current?.Name, current?.Document, now); Advance(now);
    }
    private void Tick(object? sender, EventArgs e) => Advance(clock());
    internal void Advance(DateTime now)
    {
        foreach (var item in active.Values.Where(a => now - a.Notice.StartedAt >= TimeSpan.FromMinutes(a.Notice.DurationMinutes)).ToArray())
            CloseAlert(Key(item.Notice), item.Window);
        var notices = session.Tick(now);
        if (notices.Count == 0) return;
        var startSound = active.Count == 0;
        restoreMain();
        foreach (var notice in notices)
        {
            var key = Key(notice);
            ReminderWindow? alert = null;
            alert = new ReminderWindow(main, effects, () =>
            {
                if (!active.TryGetValue(key, out var current) || !ReferenceEquals(current.Window, alert)) return;
                session.AddReminder(current.Notice); CloseAlert(key, alert);
            });
            active.Add(key, (notice, alert));
            alert.Closed += (_, _) => CloseAlert(key, alert);
            alert.Display(notice);
            alert.Show(); if (effects) alert.Activate();
        }
        if (effects && startSound)
            try { ReminderSound.Start(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { report("提醒声音播放失败：" + ex.Message); }
    }
    private static (Guid Course, Guid Slot, DateTime Start) Key(ReminderNotice notice) => (notice.CourseId, notice.ScheduleId, notice.ClassStart);
    private void CloseAlert((Guid Course, Guid Slot, DateTime Start) key, ReminderWindow window)
    {
        if (!active.TryGetValue(key, out var item) || !ReferenceEquals(item.Window, window)) return;
        active.Remove(key); window.Close();
        if (effects && active.Count == 0) ReminderSound.Stop();
    }
    internal void Dismiss()
    {
        var windows = active.Values.Select(a => a.Window).ToArray(); active.Clear();
        if (effects) ReminderSound.Stop();
        foreach (var window in windows) window.Close();
    }
    public void Dispose()
    {
        timer.Stop(); timer.Tick -= Tick; workspace.PropertyChanged -= WorkspaceChanged; Dismiss();
    }
}

// Same two-note waveform and looping WinMM playback used by MySchedule.
internal static class ReminderSound
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool PlaySound(string? sound, IntPtr module, uint flags);
    public static void Start()
    {
        var path = Path.Combine(Path.GetTempPath(), "classschedule_remind.wav");
        if (!File.Exists(path)) File.WriteAllBytes(path, Wave());
        if (!PlaySound(path, IntPtr.Zero, 0x0002000B)) throw new InvalidOperationException("Windows未能播放提醒音。");
    }
    public static bool Stop() => PlaySound(null, IntPtr.Zero, 0);
    internal static byte[] Wave()
    {
        const int rate = 22050, count = 52920;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + count * 2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(count * 2);
        for (var i = 0; i < count; i++)
        {
            var t = i / (double)rate; var m = t % 1.2;
            var frequency = m < 0.3 ? 880 : m is >= 0.4 and < 0.7 ? 1174.7 : 0;
            var fade = m < 0.05 ? m / 0.05 : m is > 0.25 and < 0.3 ? (0.3 - m) / 0.05 : m is > 0.4 and < 0.45 ? (m - 0.4) / 0.05 : m is > 0.65 and < 0.7 ? (0.7 - m) / 0.05 : 1;
            writer.Write((short)(Math.Sin(2 * Math.PI * frequency * t) * 0.45 * Math.Max(0, fade) * short.MaxValue));
        }
        writer.Flush(); return stream.ToArray();
    }
}
