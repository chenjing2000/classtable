using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class ReminderPopupChecks
{
    public static int Run(Application app, bool layoutOnly = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "reminder-popup-" + Guid.NewGuid()); var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        string Plain(TextBlock text) => string.Concat(text.Inlines.OfType<Run>().Select(r => r.Text));
        TextBlock Text(Window window, string text) => Find<TextBlock>(window).Single(t => Plain(t) == text);
        Button Button(Window window, string text) => Find<Button>(window).Single(b => b.Content as string == text);
        void Click(Window window, string text) { Button(window, text).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Flush(); }
        bool Ink(Brush brush, string color) => brush is SolidColorBrush solid && solid.Color == (Color)ColorConverter.ConvertFromString(color);
        try
        {
            var doc = StudentDocument.Create(new DateOnly(2026, 11, 2));
            doc.Courses.Add(new Course { Name = "机械概论", Schedules = [new ScheduleEntry { Weeks = [1, 2], DayOfWeek = 1, StartPeriod = 1, Location = "A101" }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("检查", doc);
            var saved = File.ReadAllBytes(Path.Combine(path, "检查.json"));
            var main = new MainWindow(workspace); main.Show(); Flush(); main.Left = 120; main.Top = 40;
            using var tray = new TrayService(main, () => { }, false);
            var now = new DateTime(2026, 11, 2, 7, 29, 0);
            using var reminders = new ReminderService(workspace, main, main.Messages.Enqueue, () => now, false, tray.Restore);
            tray.Hide(); now = now.AddMinutes(1); reminders.Advance(now); Flush();
            var alert = reminders.Alert!;
            Check(main.IsVisible && main.ShowInTaskbar && main.WindowState == WindowState.Normal, "Reminder first restores main window from tray");
            Check(alert.Title == "上课提醒" && alert.Topmost && ReferenceEquals(alert.Owner, main), "Reminder title, owner and topmost are correct");
            Check(Math.Abs(alert.Left + alert.ActualWidth / 2 - main.Left - main.ActualWidth / 2) < 1 && Math.Abs(alert.Top + alert.ActualHeight / 2 - main.Top - main.ActualHeight / 2) < 1, "Reminder centers over main window");
            var title = Text(alert, "上课提醒");
            Check(title.FontSize == 18 && title.FontWeight == FontWeights.Bold && Ink(title.Foreground, "#ab1d22"), "Heading is 18 DIP bold red");
            var line = Text(alert, "08:00 机械概论@A101");
            var courseName = line.Inlines.OfType<Run>().Single(r => r.Text == "机械概论");
            Check(line.FontSize == 16 && courseName.FontWeight == FontWeights.Bold && Ink(courseName.Foreground, "#b81a35"), "Class time, course name and optional location use required typography");
            var depart = Text(alert, "请马上出发！"); var count = Text(alert, "第 1/3 次提醒");
            Check(count.FontWeight == FontWeights.Normal && Ink(count.Foreground, "#ab1d22") && new[] { title, line, depart, count }.All(t => t.TextAlignment == TextAlignment.Center), "All four lines center; repetition line is normal-weight red");
            Check(line.FontSize == 16 && depart.FontSize == 16 && count.FontSize == 16, "All reminder body text is 16 DIP");
            var ok = Button(alert, "OK"); var again = Button(alert, "Later"); var panel = (StackPanel)ok.Parent;
            Check(ok.ActualWidth == again.ActualWidth && panel.HorizontalAlignment == HorizontalAlignment.Center && Math.Abs(again.TranslatePoint(new Point(), panel).X - ok.TranslatePoint(new Point(ok.ActualWidth, 0), panel).X - 8) < .1, "Equal-width buttons center with exactly 8 DIP gap");
            Check(ok.FontSize == 16 && again.FontSize == 16 && ok.ActualWidth == 72 && alert.Width == 340 && alert.MinWidth == 300, "Compact window and English buttons have the approved sizes");
            foreach (var button in new[] { ok, again })
            {
                var label = new FormattedText((string)button.Content, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch), button.FontSize, button.Foreground, VisualTreeHelper.GetDpi(button).PixelsPerDip);
                Check(label.WidthIncludingTrailingWhitespace <= button.ActualWidth - button.Padding.Left - button.Padding.Right - 2, $"{button.Content} fits without clipping at 16 DIP");
            }
            if (layoutOnly)
            {
                alert.Display(new ReminderNotice("检查", Guid.NewGuid(), Guid.NewGuid(), string.Concat(Enumerable.Repeat("机械概论长课程名称", 8)), "教学楼四层东侧实验教室", now.AddMinutes(30), now, 1, 3, 1));
                alert.Width = alert.MinWidth; alert.Height = alert.MinHeight; Flush();
                var body = Find<ScrollViewer>(alert).Single(v => v.IsVisible && v.Content is StackPanel);
                Check(body.ScrollableHeight > 0 && Find<TextBlock>(body).All(t => t.TextWrapping == TextWrapping.Wrap), "Long reminder text wraps and scrolls at minimum window size");
                Check(ok.IsVisible && again.IsVisible && panel.HorizontalAlignment == HorizontalAlignment.Center && panel.TranslatePoint(new Point(0, panel.ActualHeight), alert).Y <= alert.ActualHeight, "Compact window keeps both footer buttons accessible");
                reminders.Dismiss(); tray.Dispose(); main.Close();
                Console.WriteLine($"REMINDER POPUP LAYOUT CHECKS PASSED: {checks}"); return 0;
            }
            now = now.AddSeconds(40); Click(alert, "Later");
            Check(reminders.Alert is null, "Again dismisses the current alert");
            again.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            now = new DateTime(2026, 11, 2, 7, 32, 59); reminders.Advance(now); Check(reminders.Alert is null, "Again does not restart interval from click time");
            now = now.AddSeconds(1); reminders.Advance(now); Flush();
            Check(Text(reminders.Alert!, "第 2/4 次提醒") is not null, "Again adds exactly one repetition to this occurrence");
            Click(reminders.Alert!, "OK");
            now = now.AddMinutes(3); reminders.Advance(now); Flush(); Check(Text(reminders.Alert!, "第 3/4 次提醒") is not null, "OK preserves remaining repetitions without adding one");
            reminders.Alert!.Close(); now = now.AddMinutes(3); reminders.Advance(now); Flush();
            Check(Text(reminders.Alert!, "第 4/4 次提醒") is not null, "Title-bar close does not add repetitions");
            Click(reminders.Alert!, "Later"); now = now.AddMinutes(3); reminders.Advance(now); Flush();
            Check(Text(reminders.Alert!, "第 5/5 次提醒") is not null, "Again on final repetition schedules one more");
            now = now.AddMinutes(1); reminders.Advance(now); Check(reminders.Alert is null, "Automatic expiry dismisses alert");
            now = now.AddMinutes(2); reminders.Advance(now); Check(reminders.Alert is null, "Expiry does not add another repetition");
            now = new DateTime(2026, 11, 9, 7, 30, 0); reminders.Advance(now); Flush();
            Check(Text(reminders.Alert!, "第 1/3 次提醒") is not null && workspace.Document!.Courses[0].ReminderCount == 3 && File.ReadAllBytes(Path.Combine(path, "检查.json")).SequenceEqual(saved), "Other weeks and stored course settings are unchanged");
            reminders.Dismiss();
            var concurrent = StudentDocument.Create(new DateOnly(2026, 11, 2));
            concurrent.Courses.Add(new Course { Name = "甲", ReminderCount = 1, Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            concurrent.Courses.Add(new Course { Name = "乙", ReminderCount = 1, Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 2, ReminderMinutes = 85 }] });
            now = new DateTime(2026, 11, 2, 7, 29, 0); workspace.Create("并发", concurrent); now = now.AddMinutes(1); reminders.Advance(now); Flush();
            var alerts = app.Windows.OfType<ReminderWindow>().ToList();
            Check(alerts.Count == 2, "Concurrent class occurrences have independent reminder windows");
            var first = alerts.Single(w => Find<TextBlock>(w).Any(t => Plain(t) == "08:00 甲"));
            Check(Find<TextBlock>(first).All(t => !Plain(t).Contains('@')), "Empty location omits both location and at sign");
            var second = alerts.Single(w => !ReferenceEquals(w, first)); Click(first, "Later");
            Check(second.IsVisible && Text(second, "第 1/1 次提醒") is not null, "Again affects only the chosen occurrence");
            Click(second, "OK"); now = now.AddMinutes(3); reminders.Advance(now); Flush();
            Check(app.Windows.OfType<ReminderWindow>().Count() == 1 && Text(reminders.Alert!, "第 2/2 次提醒") is not null, "Only the extended occurrence repeats");
            reminders.Dismiss(); tray.Dispose(); main.Close();
            Console.WriteLine($"REMINDER POPUP CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
