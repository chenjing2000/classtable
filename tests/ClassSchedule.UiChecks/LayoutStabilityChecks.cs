using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class LayoutStabilityChecks
{
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "layout-stability-" + Guid.NewGuid());
        var checks = 0; var failures = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { checks++; if (!value) failures++; Console.WriteLine((value ? "PASS " : "FAIL ") + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        TextBox Field(DependencyObject root, string property) => Find<TextBox>(root).Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == property);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var doc = StudentDocument.Create(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)));
            doc.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            var vm = new WorkspaceViewModel(new StudentFileStore(path)); vm.Create("布局检查", doc);
            var main = new MainWindow(vm); main.Show(); Flush();
            var table = Find<Grid>(main).Single(g => g.ColumnDefinitions.Count == 8);
            var todayButton = Find<Button>(main).Single(b => b.Content as string == "本周");
            var gear = Find<Button>(main).Single(b => b.ToolTip as string == "设置");
            var before = table.TranslatePoint(new Point(), main); var height = table.ActualHeight;
            var buttonHeight = todayButton.ActualHeight;
            gear.Focus(); Flush(); todayButton.Focus(); Flush();
            var focusedHeight = todayButton.ActualHeight;
            gear.Focus(); Flush();
            Check(Math.Abs(buttonHeight - focusedHeight) < .1, $"Button focus keeps height ({buttonHeight:F2}/{focusedHeight:F2})");
            Check((table.TranslatePoint(new Point(), main) - before).Length < .1 && Math.Abs(table.ActualHeight - height) < .1, "Focus keeps timetable position and size");
            var originalCell = table.Children[0];
            todayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(ReferenceEquals(originalCell, table.Children[0]), "Already-current week keeps existing cells");
            main.RefreshStudents(); Flush();
            Check(ReferenceEquals(originalCell, table.Children[0]), "Unchanged file refresh keeps existing cells");
            Check(table.Clip is RectangleGeometry clip && clip.RadiusX > 0 && new[] { new Point(0, 0), new Point(table.ActualWidth, 0), new Point(0, table.ActualHeight), new Point(table.ActualWidth, table.ActualHeight) }.All(p => !clip.FillContains(p)), "Table clips all four square corners");
            main.Width += 80; main.Height += 60; Flush();
            Check(table.Clip is RectangleGeometry resized && Math.Abs(resized.Rect.Width - table.ActualWidth) < .1 && Math.Abs(resized.Rect.Height - table.ActualHeight) < .1, "Rounded clip follows resized table");
            vm.Move(1); main.RefreshStudents(); Flush();
            Check(!ReferenceEquals(originalCell, table.Children[0]), "Changed week refreshes cells");
            vm.GoToday(); main.RefreshStudents(); Flush();
            var candidate = vm.Document!.Clone(); candidate.Courses[0].Name = "新课程名"; vm.Save(candidate); main.RefreshStudents(); Flush();
            Check(Find<TextBlock>(table).Any(t => t.Text == "新课程名"), "Changed course data appears without changing week");
            var firstStudentCell = table.Children[0]; var fingerprint = vm.Current!.Fingerprint;
            vm.Create("另一位", candidate); main.RefreshStudents(); Flush();
            Check(vm.Current!.Fingerprint == fingerprint && !ReferenceEquals(firstStudentCell, table.Children[0]), "Switching students refreshes even with identical file content");
            var course = new CourseManagementWindow(vm, WorkspacePage.Course, vm.Document!.Courses[0].Id); course.Show(); Flush();
            var editor = Find<CourseEditorView>(course).Single();
            var name = Field(editor, "Course.Name"); var count = Field(editor, "ReminderCount");
            Check(Math.Abs(name.TranslatePoint(new Point(), editor).X - count.TranslatePoint(new Point(), editor).X) < .1, "Name and reminder count inputs share left edge");
            var reminderRow = (StackPanel)count.Parent;
            bool FitsReminderRow() => reminderRow.Children.OfType<FrameworkElement>().All(c => c.TranslatePoint(new Point(c.ActualWidth, 0), editor).X <= editor.ActualWidth);
            Check(FitsReminderRow(), "Reminder fields fit default course window width");
            course.Width = course.MinWidth; Flush();
            Check(FitsReminderRow() && Math.Abs(name.TranslatePoint(new Point(), editor).X - count.TranslatePoint(new Point(), editor).X) < .1, "Reminder fields fit and align at minimum course window width");
            var add = Find<Button>(editor).Single(b => b.Content as string == "＋");
            Check(add.ActualWidth == 24 && add.ActualHeight == 24, "Add slot button is 24 by 24 DIP");
            var glyph = Find<ContentPresenter>(add).Single();
            var center = glyph.TranslatePoint(new Point(glyph.ActualWidth / 2, glyph.ActualHeight / 2), add);
            Check(Math.Abs(center.X - 12) < .1 && Math.Abs(center.Y - 12) < .1, "Plus content is centered");
            var opened = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                var slot = app.Windows.OfType<ScheduleWindow>().Single(); opened = true;
                Find<Button>(slot).Single(b => b.Content as string == "取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }, DispatcherPriority.ApplicationIdle);
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(opened, "Small plus still opens new slot dialog");
            course.Close(); main.Close();
            Console.WriteLine($"LAYOUT STABILITY CHECKS: {checks - failures}/{checks} passed"); return failures == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
