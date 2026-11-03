using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class TodayToastChecks
{
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "today-toast-" + Guid.NewGuid());
        var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        bool ColorIs(Brush brush, string color) => brush is SolidColorBrush solid && solid.Color == (Color)ColorConverter.ConvertFromString(color);
        Border Notice(Window window) => Find<Border>(window).Single(b => b.Name == "TransientNotice");
        void Click(Window window, string label) => Find<Button>(window).Single(b => b.IsVisible && b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void WaitUntil(Func<bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (!predicate() && DateTime.UtcNow < deadline) { Flush(); Thread.Sleep(20); }
            if (!predicate()) throw new Exception("Message queue timed out");
            Flush();
        }
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var document = StudentDocument.Create(monday);
            document.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("检查", document);
            var date = monday;
            var main = new MainWindow(workspace, () => date); main.Show(); Flush();
            var table = Find<Grid>(main).Single(g => g.ColumnDefinitions.Count == 8);
            Border Cell(int row, int col) => table.Children.OfType<Border>().Single(b => Grid.GetRow(b) == row && Grid.GetColumn(b) == col && (row == 0 || b.Child is Grid));
            bool NoToday() => table.Children.OfType<Border>().Where(b => Grid.GetRow(b) == 0).All(b => !ColorIs(b.Background, "#FFE7BE"));
            var header = Cell(0, 1); var original = table.Children[0];
            var course = table.Children.OfType<Border>().Single(b => b.Child is TextBlock text && text.Text == "数学"); var courseBrush = course.Background;
            Check(header.Background is SolidColorBrush color && color.Color == (Color)ColorConverter.ConvertFromString("#FFE7BE"), "Today header uses warm background");
            Check(Enumerable.Range(1, document.Settings.ClassesPerDay).All(row => ColorIs(Cell(row, 1).Background, "#FFF2D8")), "Entire today column has warm cell backgrounds");
            date = monday.AddDays(1); main.RefreshDateHighlight(); Flush();
            Check(ColorIs(Cell(0, 1).Background, "#EEF5FF") && ColorIs(Cell(0, 2).Background, "#FFE7BE") && ColorIs(Cell(1, 2).Background, "#FFF2D8"), "Date change moves highlight and clears yesterday");
            Check(ReferenceEquals(original, table.Children[0]) && ReferenceEquals(courseBrush, course.Background), "Date update keeps existing cells and course colors");
            date = monday.AddMonths(1); main.RefreshDateHighlight(); Check(NoToday(), "Matching day number in another month is not today");
            date = monday.AddDays(6); main.RefreshDateHighlight(); Check(ColorIs(Cell(0, 7).Background, "#FFE7BE"), "Previous-week Sunday highlights the last column");
            date = monday; main.Hide(); main.Show(); Flush(); Check(ColorIs(Cell(0, 1).Background, "#FFE7BE"), "Restoring visible window rechecks the date");
            workspace.Move(1); main.RefreshStudents(); Flush(); Check(workspace.Week == 2 && NoToday(), "Browsing another week does not jump or highlight wrong weekday");
            date = monday.AddDays(7); main.RefreshDateHighlight(); Check(workspace.Week == 2 && ColorIs(Cell(0, 1).Background, "#FFE7BE"), "Date entering displayed week updates without navigation");
            var nextSunday = document.Clone(); nextSunday.Settings.SundayBelongsTo = "next"; nextSunday.Courses.Clear();
            workspace.Create("周日", nextSunday); workspace.Move(1 - workspace.Week); main.RefreshStudents();
            date = monday.AddDays(-1); main.RefreshDateHighlight(); Flush();
            Check(ColorIs(Cell(0, 1).Background, "#FFE7BE"), "Next-week Sunday highlights first column by actual date");
            date = monday; main.RefreshDateHighlight(); Check(ColorIs(Cell(0, 2).Background, "#FFE7BE"), "Monday follows first-column Sunday correctly");
            var height = table.ActualHeight;
            main.Messages.Enqueue("浮动提示"); main.Messages.Enqueue("下一条"); Flush();
            var text = Find<TextBlock>(main).Single(t => t.Text == "浮动提示");
            var notice = Notice(main);
            Check(Math.Abs(table.ActualHeight - height) < .1, "Transient notice does not shrink timetable");
            Check(notice.Parent is Grid && Math.Abs(notice.Background.Opacity - .65) < .001 && text.FontSize == 14 && text.Opacity == 1, "Toast overlays with 35 percent transparent background and opaque 14 DIP text");
            var root = (FrameworkElement)main.Content;
            Check(Math.Abs(notice.TranslatePoint(new Point(notice.ActualWidth / 2, 0), root).X - root.ActualWidth / 2) < .1, "Toast is horizontally centered");
            Check(main.Messages.Current == "浮动提示", "First queued notice remains until its timeout");
            WaitUntil(() => main.Messages.Current == "下一条"); Check(notice.IsVisible && text.Text == "下一条", "FIFO switches to the next notice after five seconds");
            WaitUntil(() => main.Messages.Current.Length == 0); Check(!notice.IsVisible && Math.Abs(table.ActualHeight - height) < .1, "Final timeout hides toast without resizing timetable");

            var manager = new StudentManagementWindow(workspace) { Owner = main }; manager.Show(); Flush();
            Find<ListBox>(manager).Single().SelectedItem = "周日"; Click(manager, "删除"); Flush();
            var confirmationText = Find<TextBlock>(manager).Single(t => t.Text == "是否删除‘周日’的全部课程信息？");
            var confirmation = Find<Border>(manager).Single(b => b.Name == "ConfirmationNotice");
            manager.Messages.Enqueue("确认期间的普通警告"); Flush(); var floating = Notice(manager);
            Check(confirmation.Parent is Grid && confirmation.IsVisible, "Interactive confirmation uses its own persistent overlay");
            Check(floating.TranslatePoint(new Point(0, floating.ActualHeight), manager).Y <= confirmation.TranslatePoint(new Point(), manager).Y, "Toast stays above the confirmation overlay");
            var no = Find<Button>(manager).Single(b => b.Content as string == "否");
            var point = no.TranslatePoint(new Point(no.ActualWidth / 2, no.ActualHeight / 2), manager);
            Check(Ui.Ancestor<Button>(manager.InputHitTest(point) as DependencyObject) == no && ((StackPanel)no.Parent).HorizontalAlignment == HorizontalAlignment.Right, "Confirmation No remains right aligned and clickable");
            manager.Width = manager.MinWidth; manager.Height = manager.MinHeight;
            manager.Messages.Enqueue("较长提示排队");
            var floatingText = (TextBlock)((ScrollViewer)floating.Child).Content;
            floatingText.Text = string.Concat(Enumerable.Repeat("较长的错误提示信息需要完整阅读，", 20)); Flush();
            Check(floating.IsVisible && floating.ActualHeight > 0 && ((ScrollViewer)floating.Child).ScrollableHeight > 0 && floating.TranslatePoint(new Point(0, floating.ActualHeight), manager).Y <= no.TranslatePoint(new Point(), manager).Y, "Long toast scrolls at minimum size without covering confirmation choices");
            Click(manager, "否"); Flush(); Check(workspace.Names.Contains("周日") && !confirmation.IsVisible, "No still cancels deletion without changing student data"); manager.Close();

            var settings = new GeneralSettingsWindow(workspace) { Owner = main };
            var courses = new CourseManagementWindow(workspace) { Owner = main };
            var slot = new ScheduleWindow(new ScheduleViewModel(workspace.Document!.Settings, null), _ => { }) { Owner = main };
            var reminder = new ReminderWindow(main, false, () => { });
            foreach (var window in new DialogWindow[] { settings, courses, slot, reminder })
            {
                window.Show(); Flush(); var surface = (Grid)window.Content;
                var layout = (DockPanel)surface.Children[0]; var body = (FrameworkElement)layout.Children[^1]; var before = body.ActualHeight;
                window.Messages.Enqueue("统一悬浮信息"); Flush(); var overlay = Notice(window);
                var label = (TextBlock)((ScrollViewer)overlay.Child).Content;
                Check(overlay.IsVisible && label.FontSize == 14 && overlay.Background.Opacity == .65 && Math.Abs(body.ActualHeight - before) < .1, "Common floating notice preserves layout: " + window.Title);
                if (ReferenceEquals(window, slot)) Click(slot, "取消"); else window.Close();
            }
            var ten = StudentDocument.Create(monday, "two"); ten.Settings.ClassesPerDay = 10;
            ten.Settings.ClassPeriods = ClassTimeGenerator.Generate(ten.Settings);
            ten.Courses.Add(new Course { Name = "双节", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 5 }] });
            workspace.Create("十节", ten); workspace.Move(1 - workspace.Week); main.RefreshStudents(); date = monday; main.RefreshDateHighlight(); Flush();
            var doubleBlock = table.Children.OfType<Border>().Single(b => b.Child is TextBlock label && label.Text == "双节");
            Check(Enumerable.Range(1, 10).All(row => ColorIs(Cell(row, 1).Background, "#FFF2D8")) && Grid.GetRowSpan(doubleBlock) == 2, "Ten-period double-class mode highlights every cell and preserves course span");
            main.Close(); Console.WriteLine($"TODAY/TOAST CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
