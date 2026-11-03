using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class FloatingConfirmationChecks
{
    private sealed class Probe : DialogWindow
    {
        public Border Page { get; } = new() { Background = Brushes.White };
        public Probe() : base("悬浮确认测试", 420, 360, false)
        {
            MinWidth = 300; MinHeight = 300; Body(Page, false); Footer.Children.Add(Ui.Button("固定操作", () => { }));
        }
        public void Ask(string text, params (string Label, Action Action)[] choices) => SetConfirmation(text, choices);
    }
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "floating-confirmation-" + Guid.NewGuid()); var failed = 0; var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { checks++; if (!value) failed++; Console.WriteLine((value ? "PASS " : "FAIL ") + label); }
        IEnumerable<T> Find<T>(DependencyObject root) => Program.Descendants(root).OfType<T>();
        Border Card(Window window) => Find<Border>(window).Single(b => b.Name == "ConfirmationNotice");
        Button GetButton(Window window, string text) => Find<Button>(window).Single(b => b.Content as string == text);
        bool Clickable(Window window, Button button) => Ui.Ancestor<Button>(window.InputHitTest(button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window)) as DependencyObject) == button;
        try
        {
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.CreateStudent("检查", DateOnly.FromDateTime(DateTime.Today));
            var window = new StudentManagementWindow(workspace); window.Show(); Flush();
            Program.Descendants(window).OfType<ListBox>().Single().SelectedItem = "检查";
            var list = Program.Descendants(window).OfType<ListBox>().Single(); var height = list.ActualHeight;
            Program.Descendants(window).OfType<Button>().Single(b => b.Content as string == "删除").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            var card = Card(window);
            Check(card.Parent is Grid, "Interactive confirmation floats in the window overlay");
            Check(Math.Abs(list.ActualHeight - height) < .1, "Confirmation does not shrink student list");
            Check(card.Background is SolidColorBrush brush && brush.Color == (Color)ColorConverter.ConvertFromString("#F5F8FD") && brush.Opacity == 1 && card.Opacity == 1, "Confirmation background matches main window and is opaque");
            var no = GetButton(window, "否");
            Check(no.IsKeyboardFocused && ((StackPanel)no.Parent).HorizontalAlignment == HorizontalAlignment.Right && Clickable(window, no), "Default No remains right aligned and clickable");
            Program.Descendants(window).OfType<Button>().Single(b => b.Content as string == "否").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.Close();
            Check(workspace.Names.Contains("检查"), "No preserves student data");

            var probe = new Probe { FontSize = 19 }; probe.Show(); Flush();
            var pageHeight = probe.Page.ActualHeight; var decisions = 0;
            probe.Ask("是否继续？", ("是", () => decisions++), ("否", () => decisions++)); Flush();
            card = Card(probe); var surface = (FrameworkElement)probe.Content;
            var question = (ScrollViewer)((StackPanel)card.Child).Children[0];
            Check(((TextBlock)question.Content).FontSize == 14 && Find<Button>(card).All(b => b.FontSize == 14), "Confirmation text and choices explicitly use 14 DIP");
            Check(ReferenceEquals(card.Background, probe.Background) && card.Background.Opacity == 1, "Confirmation shares the opaque application background resource");
            Check(Math.Abs(card.TranslatePoint(new Point(card.ActualWidth / 2, 0), surface).X - surface.ActualWidth / 2) < .1 && Math.Abs(probe.Page.ActualHeight - pageHeight) < .1, "Confirmation centers without resizing the page");
            probe.Messages.Enqueue("操作失败，请修正后重试。"); Flush();
            var notice = Find<Border>(probe).Single(b => b.Name == "TransientNotice");
            Check(notice.IsVisible && notice.Background.Opacity == .75 && notice.TranslatePoint(new Point(0, notice.ActualHeight), probe).Y + 7 <= card.TranslatePoint(new Point(), probe).Y, "Ordinary message floats above confirmation with a gap");
            Check(Clickable(probe, GetButton(probe, "固定操作")) && Clickable(probe, GetButton(probe, "否")), "Floating cards do not cover fixed footer or confirmation buttons");
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while ((probe.Messages.Current.Length > 0 || probe.Messages.Exiting is not null) && DateTime.UtcNow < deadline) { Flush(); Thread.Sleep(20); }
            Flush();
            Check(!notice.IsVisible && card.IsVisible && decisions == 0, "Transient timeout does not dismiss or choose the confirmation");

            probe.Width = probe.MinWidth; probe.Height = probe.MinHeight;
            probe.Ask(string.Concat(Enumerable.Repeat("较长的确认信息需要完整阅读。", 30)), ("是", () => decisions++), ("否", () => decisions++)); Flush();
            question = (ScrollViewer)((StackPanel)card.Child).Children[0];
            Check(question.ScrollableHeight > 0 && Clickable(probe, GetButton(probe, "是")) && Clickable(probe, GetButton(probe, "否")), "Long confirmation scrolls while both choices stay accessible at minimum size");
            var noticeText = string.Concat(Enumerable.Repeat("较长的错误提示。", 30)); probe.Messages.Enqueue(noticeText); Flush();
            notice = Find<Border>(probe).Single(b => b.Name == "TransientNotice");
            Check(notice.IsVisible && notice.ActualHeight > 0 && card.ActualHeight > 0 && notice.TranslatePoint(new Point(0, notice.ActualHeight), probe).Y + 7 <= card.TranslatePoint(new Point(), probe).Y, "Tall confirmation and long error both receive visible nonoverlapping space");
            Check(((ScrollViewer)notice.Child).ScrollableHeight > 0 && Clickable(probe, GetButton(probe, "否"))  , "Long notice can scroll without obstructing confirmation choices");
            var compactPageHeight = probe.Page.ActualHeight;
            GetButton(probe, "否").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(!card.IsVisible && notice.IsVisible && decisions == 1 && Math.Abs(probe.Page.ActualHeight - compactPageHeight) < .1, "Explicit No hides only confirmation; ordinary error remains");
            probe.Width = 480;
            probe.Ask("有未保存的修改，如何处理？", ("保存", () => decisions++), ("放弃修改", () => decisions++), ("继续编辑", () => decisions++)); Flush();
            Check(Find<Button>(card).Select(b => b.Content as string).SequenceEqual(new[] { "保存", "放弃修改", "继续编辑" }) && GetButton(probe, "继续编辑").IsKeyboardFocused, "Three-choice confirmation preserves order and safe default focus");
            GetButton(probe, "继续编辑").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); probe.Close();
            var settings = new GeneralSettingsWindow(workspace); settings.Show(); settings.Width = settings.MinWidth; settings.Height = settings.MinHeight; Flush();
            settings.State.Settings!.Duration = "40"; settings.State.RequestClose(); Flush();
            var settingsCard = Card(settings);
            var settingsQuestion = (ScrollViewer)((StackPanel)settingsCard.Child).Children[0];
            ((TextBlock)settingsQuestion.Content).Text = string.Concat(Enumerable.Repeat("较长的确认文字。", 60));
            settings.Messages.Enqueue(noticeText); Flush();
            var page = Find<ScrollViewer>(settings).Single(v => v.Name == "SettingsPage");
            void Wheel(ScrollViewer target) => target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
            var offset = page.VerticalOffset; Wheel(settingsQuestion); Flush();
            Check(settingsQuestion.VerticalOffset > 0 && Math.Abs(page.VerticalOffset - offset) < .1, "Settings wheel scrolls confirmation content without scrolling underlying page");
            var settingsNotice = Find<Border>(settings).Single(b => b.Name == "TransientNotice");
            var settingsError = (ScrollViewer)settingsNotice.Child; offset = page.VerticalOffset; Wheel(settingsError); Flush();
            Check(settingsError.VerticalOffset > 0 && Math.Abs(page.VerticalOffset - offset) < .1, "Settings wheel scrolls temporary error without scrolling underlying page");
            settings.State.CancelConfirmation(); settings.State.CancelSettings();
            Console.WriteLine($"FLOATING CONFIRMATION CHECKS: {checks - failed}/{checks} passed");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
