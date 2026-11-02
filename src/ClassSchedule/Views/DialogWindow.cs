using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

public class DialogWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Border notice;
    private readonly TextBlock noticeText;
    private readonly Border confirmation;
    private readonly StackPanel confirmationContent;
    protected readonly DockPanel Layout = new() { Margin = new Thickness(12) };
    protected readonly StackPanel Footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    protected bool Dirty;
    private bool allowClose;
    private Action? exitCompleted;
    internal bool ApplicationExitPending { get; private set; }
    protected bool ExitApproved { get; private set; }
    public NotificationQueue Messages { get; } = new();
    public DialogWindow(string title, double width, double height, bool editor = true)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = title; Width = width; Height = height; MinWidth = 360; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Content = Layout;
        SourceInitialized += (_, _) => WindowPlacement.DisableMaximize(this);
        noticeText = Ui.Text("");
        notice = new Border { Background = new SolidColorBrush(Color.FromRgb(255, 241, 210)), Padding = new Thickness(10), CornerRadius = new CornerRadius(10), Margin = new Thickness(4, 8, 4, 0), Visibility = Visibility.Collapsed, Child = new ScrollViewer { Content = noticeText, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        DockPanel.SetDock(notice, Dock.Bottom); Layout.Children.Add(notice);
        confirmationContent = new StackPanel(); confirmation = Ui.Card(confirmationContent); confirmation.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(confirmation, Dock.Bottom); Layout.Children.Add(confirmation);
        DockPanel.SetDock(Footer, Dock.Bottom); Layout.Children.Add(Footer);
        Messages.Changed += UpdateNotice; timer.Tick += Tick; timer.Start();
        Loaded += (_, _) =>
        {
            var area = WindowPlacement.WorkArea(this);
            Width = Math.Min(Width, area.Width); Height = Math.Min(Height, area.Height);
            if (WindowStartupLocation == WindowStartupLocation.CenterScreen)
            { Left = area.Left + (area.Width - Width) / 2; Top = area.Top + (area.Height - Height) / 2; }
            else if (Owner is not null)
            { Left = Owner.Left + (Owner.ActualWidth - Width) / 2; Top = Owner.Top + (Owner.ActualHeight - Height) / 2; }
            Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
            Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
            if (editor)
            {
                AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => Dirty = true));
                AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler((_, args) => { if (args.OriginalSource is ComboBox) Dirty = true; }));
                AddHandler(RadioButton.CheckedEvent, new RoutedEventHandler((_, _) => Dirty = true));
            }
        };
        Closing += OnClosing;
        PreviewKeyDown += (_, args) =>
        {
            if (ApplicationExitPending && args.Key == System.Windows.Input.Key.Escape)
            { ShowExitConfirmation(); args.Handled = true; }
        };
        KeyDown += (_, args) =>
        {
            if (!editor || args.Handled || args.Key != System.Windows.Input.Key.Escape) return;
            if (confirmation.Visibility == Visibility.Visible) confirmation.Visibility = Visibility.Collapsed;
            else Close();
            args.Handled = true;
        };
        Closed += (_, _) => { timer.Stop(); timer.Tick -= Tick; Messages.Changed -= UpdateNotice; Messages.Dispose(); };
    }
    protected void Body(UIElement element, bool scroll = true) => Layout.Children.Add(scroll ? new ScrollViewer { Content = element, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } : element);
    private void Tick(object? sender, EventArgs e) => Messages.Advance();
    private void UpdateNotice() { noticeText.Text = Messages.Current; notice.Visibility = Messages.Current.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }
    public bool Attempt(Action action)
    {
        try { action(); return true; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException)
        { Messages.Enqueue(ex.Message); return false; }
    }
    protected void Confirm(string text, Action accepted, string acceptText = "确定")
    {
        SetConfirmation(text, (acceptText, accepted), ("取消", ClearConfirmation));
    }
    protected void ClearConfirmation() => confirmation.Visibility = Visibility.Collapsed;
    protected void SetConfirmation(string text, params (string Label, Action Action)[] choices)
    {
        confirmationContent.Children.Clear(); confirmationContent.Children.Add(Ui.Text(text));
        var buttons = choices.Select(c => Ui.Button(c.Label, () => { ClearConfirmation(); c.Action(); })).ToArray();
        var buttonRow = Ui.Row(buttons); buttonRow.HorizontalAlignment = HorizontalAlignment.Right;
        confirmationContent.Children.Add(buttonRow);
        confirmation.Visibility = Visibility.Visible;
        buttons.LastOrDefault()?.Focus();
    }
    protected void Accept() { allowClose = true; DialogResult = true; }
    protected void DiscardAndClose() { allowClose = true; Close(); }
    protected virtual bool HasExitChanges => Dirty;
    protected virtual void PrepareApplicationExit() { }
    protected virtual void SaveApplicationExit(Action saved) => saved();
    internal void RequestApplicationExit(Action completed)
    {
        if (ApplicationExitPending) return;
        PrepareApplicationExit(); exitCompleted = completed;
        if (!HasExitChanges) { FinishApplicationExit(); return; }
        ApplicationExitPending = true; Footer.IsEnabled = false; ShowExitConfirmation();
    }
    protected void ShowExitConfirmation() => SetConfirmation("是否保存修改？",
        ("是", () => { if (!Attempt(() => SaveApplicationExit(FinishApplicationExit))) ShowExitConfirmation(); }),
        ("否", FinishApplicationExit));
    private void FinishApplicationExit()
    {
        ExitApproved = allowClose = true; ApplicationExitPending = false;
        var next = exitCompleted; exitCompleted = null;
        Close();
        if (next is not null) Dispatcher.BeginInvoke(next, DispatcherPriority.Background);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ApplicationExitPending) { e.Cancel = true; ShowExitConfirmation(); return; }
        if (!Dirty || allowClose) return;
        e.Cancel = true; Confirm("有未保存的修改，是否放弃？", () => { allowClose = true; Close(); }, "放弃修改");
    }
}
