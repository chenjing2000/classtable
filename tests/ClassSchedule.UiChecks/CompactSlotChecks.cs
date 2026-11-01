using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class CompactSlotChecks
{
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "compact-slot-" + Guid.NewGuid()); var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        try
        {
            var doc = StudentDocument.Create(new DateOnly(2026, 11, 2));
            doc.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("检查", doc);
            var window = new CourseManagementWindow(workspace, WorkspacePage.Course, doc.Courses[0].Id); window.Show(); Flush();
            var view = Find<CourseEditorView>(window).Single(); var table = Find<DataGrid>(view).Single();
            Check(!Find<Border>(view).Any(b => b.Name == "NewSlotArea"), "Extra full-width blank row is removed");
            var add = Find<Button>(view).Single(b => b.Content as string == "＋");
            var heading = Find<TextBlock>(view).Single(t => t.Text == "上课时段设置");
            Check(add.TranslatePoint(new Point(), view).X > heading.TranslatePoint(new Point(heading.ActualWidth, 0), view).X && add.HorizontalAlignment == HorizontalAlignment.Right, "Add button is right aligned beside slot heading");
            Check(window.Height < 600 && window.Height % 1 == 0, "Course window has a smaller integer default height");
            Check(Math.Abs(table.ActualHeight - (table.ColumnHeaderHeight + table.RowHeight * 5 + 2)) < 1, $"Default table viewport fits header and five rows ({table.ActualHeight:F1} DIP)");
            void ExpectDialog(Action open, bool isNew, bool save = false)
            {
                var opened = false; var seen = false;
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    var slot = app.Windows.OfType<ScheduleWindow>().Single(); seen = true; opened = ((ScheduleViewModel)slot.DataContext).IsNew;
                    if (save) { var draft = (ScheduleViewModel)slot.DataContext; draft.Weeks = "1"; draft.Time = "107"; }
                    Find<Button>(slot).Single(b => b.Content as string == (save ? "保存" : "取消")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }, DispatcherPriority.ApplicationIdle);
                open(); Flush(); Check(seen && opened == isNew, isNew ? "Add opens a new slot" : "Double click edits existing slot");
            }
            ExpectDialog(() => add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), true);
            ExpectDialog(() => Program.DoubleClick(table, (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0)), false);
            var presenter = Find<ScrollContentPresenter>(table).First();
            ExpectDialog(() => Program.DoubleClick(table, presenter), true);
            for (var i = 2; i <= 6; i++) window.State.Course!.Course.Schedules.Add(new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = i });
            window.State.Course!.Refresh(); Flush();
            var scroll = Find<ScrollViewer>(table).First();
            Check(table.Items.Count == 6 && scroll.ScrollableHeight > 0, "More than five slots scroll inside the table");
            Check(add.IsVisible && add.IsEnabled, "Right-aligned add remains available when table is full");
            ExpectDialog(() => add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), true);
            table.ScrollIntoView(table.Items[^1]); Flush();
            Check(table.ItemContainerGenerator.ContainerFromIndex(5) is DataGridRow { IsVisible: true }, "Last slot remains reachable by table scrolling");
            ExpectDialog(() => add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), true, true);
            Check(table.Items.Count == 7 && workspace.Document!.Courses[0].Schedules.Count == 1, "Plus saves another slot into course draft without a five-slot limit");
            var save = Find<Button>(window).Single(b => b.Content as string == "保存");
            Check(table.TranslatePoint(new Point(0, table.ActualHeight), window).Y <= save.TranslatePoint(new Point(), window).Y, "Compact table does not overlap footer buttons");
            window.Width = 600; window.Height = 600; Flush();
            Check(table.ActualHeight <= 206.1, "Enlarging window keeps five-row table viewport");
            window.Height = window.MinHeight; Flush();
            Check(add.IsVisible && table.ActualHeight < 206 && table.TranslatePoint(new Point(0, table.ActualHeight), window).Y <= save.TranslatePoint(new Point(), window).Y, "Minimum window height shrinks table and keeps add/footer reachable");
            window.State.RequestClose(); window.State.Discard(); Flush();
            Console.WriteLine($"COMPACT SLOT CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
