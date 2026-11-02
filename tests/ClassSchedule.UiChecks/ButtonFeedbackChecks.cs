using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class ButtonFeedbackChecks
{
    private sealed class PressProbe : Button
    {
        public void Press(bool value) => IsPressed = value;
    }

    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "button-feedback-" + Guid.NewGuid());
        var checks = 0; var failures = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { checks++; if (!value) failures++; Console.WriteLine((value ? "PASS " : "FAIL ") + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        bool Resting(Button button) => button.Template.FindName("Focus", button) is Border { Visibility: Visibility.Collapsed }
            && button.Template.FindName("Surface", button) is Border surface && surface.BorderBrush == button.BorderBrush;
        try
        {
            var probe = new PressProbe { Content = "操作", Style = (Style)app.FindResource(typeof(Button)) };
            var next = new Button { Content = "下一项" };
            var host = new Window { Content = new StackPanel { Children = { probe, next } }, Width = 240, Height = 160 };
            host.Show(); Flush(); probe.Focus(); Flush();
            Check(Resting(probe), "Focused but released button keeps ordinary border");
            var height = probe.ActualHeight;
            probe.Press(true); Flush();
            Check(probe.Template.FindName("Focus", probe) is Border { Visibility: Visibility.Visible, BorderThickness.Left: 2 }, "Pressed button shows thick blue overlay");
            probe.Press(false); Flush();
            Check(Resting(probe) && Math.Abs(probe.ActualHeight - height) < .1, "Release restores border without changing size");
            Check(probe.Focusable && probe.IsTabStop && probe.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)) && next.IsKeyboardFocused, "Keyboard focus navigation remains available");
            Check(probe.FocusVisualStyle is not null && ReferenceEquals(probe.FocusVisualStyle, app.TryFindResource("ButtonKeyboardFocus")), "Keyboard focus uses dedicated rounded overlay");
            var vm = new WorkspaceViewModel(new StudentFileStore(path));
            vm.Create("检查", StudentDocument.Create(new DateOnly(2026, 11, 2)));
            var main = new MainWindow(vm); main.Show(); Flush();
            var today = Find<Button>(main).Single(b => b.Content as string == "本周");
            today.Focus(); today.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(Resting(today), "Today button does not retain thick border after action");
            var gear = Find<Button>(main).Single(b => b.ToolTip as string == "设置");
            gear.Focus(); gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); gear.ContextMenu!.IsOpen = false; Flush();
            Check(Resting(gear), "Gear button returns to ordinary border after menu closes");
            var course = new CourseManagementWindow(vm, WorkspacePage.Course); course.Show(); Flush();
            var add = Find<Button>(course).Single(b => b.Content as string == "＋");
            void SlotAction(bool save)
            {
                add.Focus();
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    var slot = app.Windows.OfType<ScheduleWindow>().Single();
                    if (save) { var draft = (ScheduleViewModel)slot.DataContext; draft.Weeks = "1"; draft.Time = "101"; }
                    Find<Button>(slot).Single(b => b.Content as string == (save ? "保存" : "取消")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }, DispatcherPriority.ApplicationIdle);
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                Check(Resting(add), save ? "Plus returns to ordinary border after adding slot" : "Plus returns to ordinary border after cancelling slot");
            }
            SlotAction(false); SlotAction(true);
            var actions = Find<Button>(course).Where(b => b.Content as string is "保存" or "取消" or "‹ 返回课程列表").ToList();
            Check(actions.Count == 3 && actions.All(b => b.Focusable && b.IsTabStop && b.FocusVisualStyle is not null), "Course footer and back buttons retain keyboard accessibility");
            course.State.RequestClose(); course.State.Discard(); main.Close(); host.Close();
            Console.WriteLine($"BUTTON FEEDBACK CHECKS: {checks - failures}/{checks} passed"); return failures == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
