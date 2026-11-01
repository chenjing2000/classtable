using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ClassSchedule;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class CourseReminderChecks
{
    public static int Run(Application app, bool preview = false, bool soundSmoke = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "course-reminder-ui-" + Guid.NewGuid());
        var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        void Click(Window window, string text) { Find<Button>(window).Single(b => b.Content as string == text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
        TextBox Box(Window window, string binding) => Find<TextBox>(window).Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == binding);
        try
        {
            var start = new DateTime(2026, 11, 2, 8, 0, 0);
            var d = StudentDocument.Create(DateOnly.FromDateTime(start));
            d.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1, Location = "A101" }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("检查学生", d);
            var main = new MainWindow(workspace); main.Show(); Flush();
            Check(main.Title == "课程表", "Main title uses new name");
            void WindowRules(Window w) => Check(double.IsPositiveInfinity(w.MaxWidth) && double.IsPositiveInfinity(w.MaxHeight) && !WindowPlacement.CanMaximize(w), "No maximum size; maximize disabled: " + w.Title);
            WindowRules(main);
            var students = new StudentManagementWindow(workspace) { Owner = main }; students.Show(); Flush();
            var list = Find<ListBox>(students).Single(); var name = Box(students, "StudentName");
            Check(list.SelectedIndex == -1 && name.Text == "" && students.Model.Selected is null, "Student manager initially has no selection or name");
            Check(Math.Abs(name.ActualWidth * 2 - list.ActualWidth) < 1 && name.HorizontalAlignment == HorizontalAlignment.Right && name.TextAlignment == TextAlignment.Center, "Student name is half-list width, right aligned and centered");
            WindowRules(students); students.Close();

            var manager = new CourseManagementWindow(workspace) { Owner = main }; manager.Show(); Flush();
            Check(Find<GroupedCourseTable>(manager).Single().SelectedCourseId is null, "Course management initially selects nothing");
            Click(manager, "确定"); Check(!manager.IsVisible && workspace.Document!.Courses.Count == 1, "Course list OK closes without changing saved courses");

            GeneralSettingsWindow Settings() { var w = new GeneralSettingsWindow(workspace) { Owner = main }; w.Show(); Flush(); return w; }
            var settings = Settings(); WindowRules(settings); settings.State.Settings!.Duration = "40"; Click(settings, "保存");
            Check(!settings.IsVisible && workspace.Document!.Settings.TimeGeneration.DurationMinutes == 40, "Settings save commits then closes");
            settings = Settings(); settings.State.Settings!.Duration = "41"; Click(settings, "取消修改");
            Check(!settings.IsVisible && workspace.Document!.Settings.TimeGeneration.DurationMinutes == 40, "Settings cancel discards and closes directly");
            settings = Settings(); settings.State.Settings!.Duration = ""; Click(settings, "保存");
            Check(settings.IsVisible && settings.Messages.Current.Length > 0, "Failed settings save preserves open draft"); Click(settings, "取消修改");
            settings = Settings(); settings.State.Settings!.FirstMonday = start.AddDays(7); Click(settings, "保存");
            Check(settings.IsVisible && settings.State.Confirmation == WorkspaceConfirmation.Dates, "Date changes still require confirmation before closing");
            Click(settings, "继续编辑"); Click(settings, "取消修改");
            settings = Settings(); settings.State.Settings!.FirstMonday = start.AddDays(7); Click(settings, "保存"); Click(settings, "保存新日期");
            Check(!settings.IsVisible && workspace.Document!.Settings.FirstWeekMonday == DateOnly.FromDateTime(start.AddDays(7)), "Confirmed date save commits and closes");
            workspace.Save(d);

            var course = new CourseManagementWindow(workspace, WorkspacePage.Course, d.Courses[0].Id) { Owner = main }; course.Show(); Flush();
            Check(!Find<TextBox>(course).Any(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path is "Course.Color" or "Course.Note"), "Color and note edit fields removed");
            Check(Box(course, "ReminderCount").Text == "3" && Box(course, "ReminderIntervalMinutes").Text == "3", "Course reminder inputs default to three and three");
            Box(course, "ReminderCount").Text = ""; Click(course, "保存");
            Check(course.IsVisible && course.Messages.Current.Length > 0 && Box(course, "ReminderCount").Text == "", "Invalid course reminder input keeps unsaved draft open"); Box(course, "ReminderCount").Text = "3";
            var table = Find<DataGrid>(course).Single();
            Check(table.Columns.Select(c => (string)c.Header).SequenceEqual(new[] { "上课周次", "时间", "上课地点", "提前提醒" }) && !Find<Button>(table).Any(b => b.ToolTip as string is "删除时段" or "编辑时段"), "Schedule table shows reminder lead, no operation buttons");
            var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0); var deleted = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                var slot = app.Windows.OfType<ScheduleWindow>().Single(); WindowRules(slot);
                Check(Find<TextBox>(slot).Where(b => b.IsVisible).All(b => b.TextAlignment == TextAlignment.Center) && Find<ComboBox>(slot).All(b => b.HorizontalContentAlignment == HorizontalAlignment.Center), "Slot box contents centered");
                Check(Box(slot, "ReminderMinutes").Text == "30", "Slot lead defaults to thirty");
                var buttons = Find<Button>(slot).Where(b => b.Content as string is "删除时段" or "取消" or "保存").ToList();
                var left = buttons.Single(b => b.Content as string == "删除时段").TranslatePoint(new Point(), slot).X;
                Check(left < buttons.Single(b => b.Content as string == "取消").TranslatePoint(new Point(), slot).X, "Slot delete is left of cancel and save");
                Click(slot, "删除时段"); deleted = true;
            }, DispatcherPriority.ApplicationIdle);
            Program.DoubleClick(table, row); Flush();
            Check(deleted && course.State.Course!.Rows.Count == 0 && workspace.Document!.Courses[0].Schedules.Count == 1, "Slot deletion closes child and changes only course draft");
            Click(course, "取消"); Click(course, "放弃修改"); Click(course, "确定");

            ScheduleEntry? accepted = null;
            var slotVm = new ScheduleViewModel(workspace.Document!.Settings, null);
            var newSlot = new ScheduleWindow(slotVm, s => accepted = s) { Owner = main };
            Dispatcher.CurrentDispatcher.BeginInvoke(() => { slotVm.ReminderMinutes = "15"; Click(newSlot, "保存"); }, DispatcherPriority.ApplicationIdle);
            newSlot.ShowDialog(); Check(accepted?.ReminderMinutes == 15 && !newSlot.IsVisible, "Slot save returns new lead and closes");
            var deleteNew = new ScheduleWindow(new(workspace.Document.Settings, null), _ => throw new Exception("Unexpected save")) { Owner = main };
            deleteNew.Show(); Flush(); Box(deleteNew, "Weeks").Text = ""; Click(deleteNew, "删除时段");
            Check(!deleteNew.IsVisible, "Unsaved new slot can be deleted even with invalid inputs");
            var cancelSlot = new ScheduleWindow(new(workspace.Document.Settings, d.Courses[0].Schedules[0]), _ => throw new Exception("Unexpected save")) { Owner = main };
            cancelSlot.Show(); Flush(); Box(cancelSlot, "Location").Text = "草稿"; Click(cancelSlot, "取消");
            Check(!cancelSlot.IsVisible && d.Courses[0].Schedules[0].Location == "A101", "Slot cancel discards changed values without a second prompt");

            var now = start.AddMinutes(-20);
            using var reminders = new ReminderService(workspace, main, _ => { }, () => now, false);
            Check(reminders.Alert?.IsVisible == true, "Runtime starts late future-class alert immediately");
            reminders.Advance(now.AddSeconds(59)); Check(reminders.Alert is not null, "Alert remains until sixty seconds");
            reminders.Advance(now.AddMinutes(1)); Check(reminders.Alert is null, "Alert stops automatically at sixty seconds");
            reminders.Advance(now.AddMinutes(3)); Check(reminders.Alert is not null, "Repeat uses original start plus interval");
            reminders.Alert!.Close(); Check(reminders.Alert is null, "Manual close stops current alert");
            reminders.Advance(now.AddMinutes(6)); Check(reminders.Alert is not null, "Manual close does not cancel next reminder");
            workspace.CreateStudent("其他学生", DateOnly.FromDateTime(now)); Check(reminders.Alert is null, "Switching students stops visible former alert");
            var wave = ReminderSound.Wave(); Check(System.Text.Encoding.ASCII.GetString(wave, 0, 4) == "RIFF" && wave.Length == 105884, "MySchedule waveform generated as PCM WAV");
            if (soundSmoke)
            {
                reminders.Dispose(); workspace.Select("检查学生");
                var editing = new CourseManagementWindow(workspace, WorkspacePage.Course, d.Courses[0].Id) { Owner = main };
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var errors = new List<string>();
                        using var audible = new ReminderService(workspace, main, errors.Add, () => now, true); Flush();
                        var alert = audible.Alert!;
                        Check(alert.IsEnabled && editing.IsEnabled && alert.IsFlashing && alert.Topmost && errors.Count == 0, "Native sound and flashing start while course modal remains open");
                        Click(alert, "关闭提醒");
                        Check(audible.Alert is null && !alert.IsFlashing && !alert.Topmost && ReminderSound.Stop(), "Native close stops sound and flashing without closing editor");
                    }
                    finally { editing.Close(); }
                }, DispatcherPriority.ApplicationIdle);
                editing.ShowDialog();
            }
            if (preview)
            {
                workspace.Select("检查学生"); main.RefreshStudents();
                Console.WriteLine("COURSE_REMINDER_PREVIEW_READY"); Dispatcher.Run();
            }
            Console.WriteLine($"COURSE REMINDER UI CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
