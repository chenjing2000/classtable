using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class ConfirmationChecks
{
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "confirmation-check-" + Guid.NewGuid());
        var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Assert(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        Button Button(Window window, string label) => Program.Descendants(window).OfType<Button>().Single(b => b.Content as string == label);
        void Click(Window window, string label) { Button(window, label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Flush(); }
        void RightAligned(Window window, string message, params string[] labels)
        {
            var text = Program.Descendants(window).OfType<TextBlock>().Single(t => t.Text == message);
            var card = Program.Descendants(window).OfType<Border>().Single(b => b.Name == "ConfirmationNotice");
            var panel = (StackPanel)card.Child;
            var row = panel.Children.OfType<StackPanel>().Single();
            Assert(row.HorizontalAlignment == HorizontalAlignment.Right && row.Children.OfType<Button>().Select(b => (string)b.Content).SequenceEqual(labels), "Confirmation choices align right: " + message);
            Assert(Math.Abs(row.TranslatePoint(new Point(row.ActualWidth, 0), panel).X - panel.ActualWidth) < 1, "Confirmation button row reaches right edge");
        }
        try
        {
            var document = StudentDocument.Create(new DateOnly(2026, 10, 26));
            document.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1, 2], DayOfWeek = 1, StartPeriod = 1 }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("检查学生", document);
            var student = new StudentManagementWindow(workspace); student.Show(); Flush(); Program.Descendants(student).OfType<ListBox>().Single().SelectedItem = "检查学生"; Flush();
            Click(student, "删除"); RightAligned(student, "是否删除‘检查学生’的全部课程信息？", "是", "否");
            Assert(Keyboard.FocusedElement == Button(student, "否"), "Student deletion still focuses No");
            Click(student, "否"); Assert(workspace.Names.Contains("检查学生"), "No preserves student"); student.Close();

            var course = new CourseManagementWindow(workspace, WorkspacePage.Course, document.Courses[0].Id); course.Show(); Flush();
            var table = Program.Descendants(course).OfType<DataGrid>().Single();
            var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0);
            var actions = Program.Descendants(row).OfType<Button>().Where(b => b.ToolTip as string is "编辑时段" or "删除时段").ToList();
            Assert(actions.Count == 0 && (string)table.Columns[^1].Header == "提前提醒", "Schedule operations replaced by reminder lead column");
            var slotOpened = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                var slot = app.Windows.OfType<ScheduleWindow>().Single(); slotOpened = slot.DataContext is ScheduleViewModel { IsNew: false }; slot.Close();
            }, DispatcherPriority.ApplicationIdle);
            Program.DoubleClick(table, row); Flush(); Assert(slotOpened, "Double-click still edits existing schedule");

            Click(course, "删除课程"); RightAligned(course, "是否删除这门课程及全部上课时段？", "是", "否");
            Assert(Keyboard.FocusedElement == Button(course, "否"), "Course deletion defaults to No");
            Click(course, "否"); Assert(workspace.Document!.Courses.Count == 1, "No preserves course and schedules");
            Click(course, "删除课程"); course.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(course), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent }); Flush();
            Assert(course.State.Confirmation == WorkspaceConfirmation.None && workspace.Document.Courses.Count == 1, "Escape cancels course deletion");
            course.State.Course!.Course.Name = "草稿"; course.State.ShowCourses(); Flush();
            RightAligned(course, "有未保存的修改，如何处理？", "保存", "放弃修改", "继续编辑");
            course.State.CancelConfirmation(); course.State.Course.Course.Name = "数学";
            Click(course, "删除课程"); Click(course, "是");
            Assert(workspace.Document.Courses.Count == 0 && course.State.Page == WorkspacePage.Courses, "Yes deletes course and its schedules"); course.Close();

            var settings = new GeneralSettingsWindow(workspace); settings.Show(); Flush();
            settings.State.Settings!.TodayColor = "#123456"; settings.State.Settings.Duration = "35";
            Click(settings, "默认值"); Flush();
            Assert(settings.State.Confirmation == WorkspaceConfirmation.None
                && settings.State.Settings.TodayColor == StudentSettings.DefaultTodayColor
                && settings.State.Settings.Periods[0].End == "08:45", "Defaults restore the draft color, parameters, and preview immediately");
            var settingsDraft = settings.State.Settings;
            settingsDraft.Duration = "180";
            Click(settings, "保存"); Flush();
            Assert(settings.IsVisible && settings.State.Settings == settingsDraft && settings.Messages.Visible.Single().Operation is not null
                && workspace.Document!.Settings.ClassPeriods[0].End == "08:45", "Invalid save reports a persistent error and keeps the draft and saved data");
            settingsDraft.Duration = "40"; settingsDraft.TodayColor = "#A1B2C3";
            Click(settings, "保存"); Flush();
            Assert(!settings.IsVisible && workspace.Document!.Settings.TodayColor == "#A1B2C3"
                && workspace.Document.Settings.ClassPeriods[0].End == "08:40", "Save regenerates preview from draft parameters and persists the color");

            Console.WriteLine($"CONFIRMATION CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
