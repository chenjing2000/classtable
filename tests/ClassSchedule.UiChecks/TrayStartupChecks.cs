using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using ClassSchedule;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class TrayStartupChecks
{
    public static int Run(Application app, bool smoke)
    {
        var path = Path.Combine(Path.GetTempPath(), "tray-startup-" + Guid.NewGuid()); Directory.CreateDirectory(path);
        var checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        void Click(Window window, string text) => Find<Button>(window).Single(b => b.IsVisible && b.Content as string == text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            var applied = false; var fail = false; var settingsPath = Path.Combine(path, "settings.json");
            var settings = new StartupSettings(settingsPath, enabled => { if (fail && enabled) throw new IOException("denied"); applied = enabled; });
            Check(settings.Load() && applied && settings.StartWithWindows, "First launch enables and persists startup preference");
            Check(settings.SetStartWithWindows(false) && !applied && !settings.StartWithWindows, "Unchecking disables startup registration");
            Check(new StartupSettings(settingsPath, enabled => applied = enabled).Load() && !applied, "Disabled preference survives reload");
            var savedSettings = File.ReadAllBytes(settingsPath); fail = true;
            Check(!settings.SetStartWithWindows(true) && !settings.StartWithWindows && !applied && File.ReadAllBytes(settingsPath).SequenceEqual(savedSettings), "Registration failure rolls back preference and registration");
            fail = false;
            File.WriteAllText(settingsPath, "broken");
            Check(!settings.Load() && File.ReadAllText(settingsPath) == "broken", "Corrupt settings remain unchanged");
            File.WriteAllBytes(settingsPath, savedSettings); Check(settings.Load(), "Valid settings load after repair");
            File.WriteAllText(settingsPath, "{\"startWithWindows\":true}");
            Check(!settings.SetStartWithWindows(true) && File.ReadAllText(settingsPath).Contains("true"), "External preference edits are not overwritten");
            File.WriteAllBytes(settingsPath, savedSettings); settings.Load();
            var uncertain = new StartupSettings(Path.Combine(path, "uncertain.json"), _ => throw new IOException("registry unavailable"));
            Check(!uncertain.Load() && !uncertain.StartWithWindows && uncertain.LastError.Contains("恢复开机启动失败"), "Registration and rollback failure remains an explicit error");

            var doc = StudentDocument.Create(new DateOnly(2026, 11, 2));
            doc.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(Path.Combine(path, "data"))); workspace.Create("检查", doc);
            var main = new MainWindow(workspace); main.ConfigureStartup(settings); main.Show(); Flush();
            var gear = Find<Button>(main).Single(b => b.ToolTip as string == "设置"); var menu = gear.ContextMenu!;
            Check(menu.Items.OfType<MenuItem>().Select(i => i.Header as string).SequenceEqual(new[] { "开机启动", "总体设置", "课程管理", "学生管理", "更新学生信息" }), "Startup is the first gear menu option");
            var startup = (MenuItem)menu.Items[0];
            Check(startup.IsCheckable && !startup.IsChecked, "Gear check state reflects loaded preference");
            gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            var menuBorder = Find<Border>(menu).First();
            Check(menuBorder.MinWidth == 0 && menuBorder.ActualWidth < 160, $"Gear menu sizes to content ({menuBorder.ActualWidth:F1} DIP)");
            var separators = menu.Items.OfType<Separator>().ToList();
            Check(separators.Count == 2 && separators.All(s =>
            {
                var line = Find<Border>(s).Single(); var left = line.TranslatePoint(new Point(), menuBorder).X;
                var right = menuBorder.ActualWidth - line.TranslatePoint(new Point(line.ActualWidth, 0), menuBorder).X;
                Console.WriteLine($"Separator insets {left:F2}/{right:F2}, container {s.ActualWidth:F2}, alignment {s.HorizontalAlignment}");
                return Math.Abs(left - right) < .1;
            }), "Both menu separators have equal left and right insets");
            menu.IsOpen = false;

            var exited = 0; ExitCoordinator? exit = null;
            using var tray = new TrayService(main, () => exit!.Request(), smoke);
            exit = new ExitCoordinator(main, tray.Restore, () => exited++);
            main.Close(); Flush(); Check(main.IsLoaded && !main.IsVisible && !main.ShowInTaskbar, "Main close hides without exiting");
            tray.Restore(); Flush(); Check(main.IsVisible && main.ShowInTaskbar && main.WindowState == WindowState.Normal && !main.Topmost, "Tray restore shows and activates without permanent topmost");
            main.WindowState = WindowState.Minimized; Flush(); Check(!main.IsVisible && !main.ShowInTaskbar, "Minimize hides to tray");
            tray.Restore(); Flush();
            tray.ShowMenu(); Flush();
            Check(tray.Menu.Items.OfType<MenuItem>().Select(i => i.Header as string).SequenceEqual(new[] { "打开", "退出" }) && tray.Menu.Width == 72, "Tray menu matches compact Open/Exit design");
            tray.Menu.IsOpen = false;
            var initial = new Window { Width = 240, Height = 160 };
            using (var backgroundTray = new TrayService(initial, () => { }, false))
            {
                var activeBefore = app.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
                backgroundTray.ShowInitialWindow(true); Flush();
                Check(!initial.IsVisible && !initial.ShowInTaskbar && initial.Opacity == 1 && !initial.IsActive && (activeBefore is null || activeBefore.IsActive), "Automatic startup stays hidden without taking activation");
                backgroundTray.Restore(); Flush(); Check(initial.IsVisible && initial.ShowInTaskbar, "Automatic startup window can be opened normally");
            }
            initial.Close();
            var racing = new Window { Width = 240, Height = 160 };
            using (var racingTray = new TrayService(racing, () => { }, false))
            {
                racingTray.ShowInitialWindow(true); racingTray.Restore(); Flush();
                Check(racing.IsVisible && racing.ShowInTaskbar && racing.Opacity == 1, "Manual activation wins over queued startup hiding");
            }
            racing.Close();
            var key = "ClassSchedule.TrayCheck." + Guid.NewGuid();
            using var instance = new SingleInstance(key); instance.Attach(main, tray.Restore);
            tray.Hide();
            var child = Process.Start(new ProcessStartInfo("dotnet") { ArgumentList = { typeof(Program).Assembly.Location, "--activate-existing", key }, UseShellExecute = false, CreateNoWindow = true })!;
            var until = DateTime.UtcNow.AddSeconds(8);
            while (!child.HasExited && DateTime.UtcNow < until) { Flush(); Thread.Sleep(10); }
            Flush(); Check(child.HasExited && child.ExitCode == 0 && main.IsVisible, "Second process restores hidden primary instance"); child.Dispose();

            var clock = new DateTime(2026, 11, 2, 7, 29, 0);
            using (var reminders = new ReminderService(workspace, main, main.Messages.Enqueue, () => clock, false))
            {
                tray.Hide(); clock = clock.AddMinutes(1); reminders.Advance(clock); Flush();
                Check(main.IsVisible && reminders.Alert is { IsVisible: true }, "New reminder restores main and shows alert");
                tray.Restore(); tray.Hide(); clock = clock.AddMinutes(1); reminders.Advance(clock); Flush();
                tray.Restore(); Flush();
                Check(reminders.Alert is null && !app.Windows.OfType<ReminderWindow>().Any(), "Restore does not resurrect expired hidden reminder");
            }

            var course = new CourseManagementWindow(workspace, WorkspacePage.Course, doc.Courses[0].Id) { Owner = main }; course.Show(); Flush();
            var draft = new ScheduleViewModel(doc.Settings, null) { Weeks = "1", Time = "201" };
            var slot = new ScheduleWindow(draft, entry => { course.State.Course!.Course.Schedules.Add(entry); course.State.Course.Refresh(); }) { Owner = course };
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                draft.Location = "教室";
                Find<TextBox>(slot).First().RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
                tray.Hide(); Flush(); tray.Restore(); Flush();
                Check(course.IsVisible && slot.IsVisible, "Tray restores nested modal editor chain");
                exit.Request(); Flush(); Check(exited == 0 && slot.ApplicationExitPending && !course.ApplicationExitPending, "Exit asks innermost editor first");
                slot.Close(); Flush(); Check(slot.IsVisible && exited == 0, "Closing confirmation cannot bypass exit choice");
                slot.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(slot)!, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(slot.IsVisible && slot.ApplicationExitPending, "Escape cannot bypass exit choice");
                Click(slot, "是");
            }, DispatcherPriority.ApplicationIdle);
            slot.ShowDialog(); Flush();
            Check(course.ApplicationExitPending && exited == 0 && course.State.Course!.Course.Schedules.Count == 2, "Saved slot becomes parent draft and parent still needs a decision");
            var no = Find<Button>(course).Single(b => b.IsVisible && b.Content as string == "否");
            Check(no.IsKeyboardFocused && ((StackPanel)no.Parent).HorizontalAlignment == HorizontalAlignment.Right, "Exit choices align right and default to No");
            course.State.Course!.ReminderCount = "0"; Click(course, "是"); Flush();
            Check(exited == 0 && course.IsVisible && course.ApplicationExitPending && course.Messages.Current.Length > 0, "Invalid save reports at bottom and blocks exit");
            course.State.Course.ReminderCount = "3"; Click(course, "是"); Flush();
            Check(exited == 1 && workspace.Document!.Courses[0].Schedules.Count == 2, "Valid parent save persists nested slot and permits exit");

            var discarded = new CourseManagementWindow(workspace, WorkspacePage.Course, doc.Courses[0].Id) { Owner = main }; discarded.Show();
            discarded.State.Course!.Course.Name = "放弃"; var discardExit = new ExitCoordinator(main, tray.Restore, () => exited++);
            discardExit.Request(); Flush(); Click(discarded, "否"); Flush();
            Check(exited == 2 && workspace.Document!.Courses[0].Name == "数学", "No explicitly discards draft and permits exit");
            var manager = new StudentManagementWindow(workspace) { Owner = main }; manager.Show(); manager.Model.StudentName = "新增";
            var studentExit = new ExitCoordinator(main, tray.Restore, () => exited++); studentExit.Request(); Flush(); Click(manager, "是"); Flush();
            Check(exited == 3 && workspace.Names.Contains("新增"), "Exit Yes saves unsaved new student name");
            var general = new GeneralSettingsWindow(workspace) { Owner = main }; general.Show(); Flush();
            general.State.Settings!.FirstMonday = new DateTime(2026, 11, 9);
            var datesExit = new ExitCoordinator(main, tray.Restore, () => exited++); datesExit.Request(); Flush(); Click(general, "是"); Flush();
            Check(exited == 3 && general.IsVisible && Find<TextBlock>(general).Any(t => t.IsVisible && t.Text.StartsWith("修改学期起点")), "Exit save preserves explicit date-change confirmation");
            Click(general, "继续编辑"); Flush(); general.State.Settings.TotalWeeks = "0"; Click(general, "是"); Flush(); Click(general, "保存新日期"); Flush();
            Check(exited == 3 && general.IsVisible && general.ApplicationExitPending, "Invalid general settings block application exit");
            general.State.Settings.TotalWeeks = "20"; Click(general, "是"); Flush(); Click(general, "保存新日期"); Flush();
            Check(exited == 4 && workspace.Document!.Settings.FirstWeekMonday == new DateOnly(2026, 11, 9), "Confirmed valid settings save permits exit");
            exit = new ExitCoordinator(main, tray.Restore, () => exited++);
            tray.ShowMenu(); Flush(); ((MenuItem)tray.Menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Flush();
            Check(exited == 5 && !tray.Menu.IsOpen, "Tray Exit closes menu and invokes exit coordination");
            tray.Dispose(); main.Close();
            Console.WriteLine($"TRAY STARTUP CHECKS PASSED: {checks}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
