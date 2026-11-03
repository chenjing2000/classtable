using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

public class DialogWindow : Window
{
    private const double NoticeGap = 8;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Border notice;
    private readonly TextBlock noticeText;
    private readonly ScrollViewer noticeMessage;
    private readonly Border confirmation;
    private readonly StackPanel confirmationContent;
    private readonly ScrollViewer confirmationMessage;
    private StackPanel? confirmationButtons;
    protected readonly DockPanel Layout = new() { Margin = new Thickness(12) };
    protected readonly Grid WindowSurface = new();
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
        WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowSurface.Children.Add(Layout); Content = WindowSurface;
        SourceInitialized += (_, _) => WindowPlacement.DisableMaximize(this);
        noticeText = Ui.Text(""); noticeText.FontSize = 14; noticeText.Foreground = new SolidColorBrush(Color.FromRgb(23, 34, 59));
        noticeMessage = new ScrollViewer { Content = noticeText, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        notice = new Border
        {
            Name = "TransientNotice", Background = new SolidColorBrush(Color.FromRgb(255, 241, 210)) { Opacity = .65 },
            Padding = new Thickness(10), CornerRadius = new CornerRadius(10), MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = .14 },
            Child = noticeMessage
        };
        Panel.SetZIndex(notice, 2); WindowSurface.Children.Add(notice);
        confirmationMessage = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        confirmationContent = new StackPanel();
        confirmation = new Border
        {
            Name = "ConfirmationNotice", Background = (Brush)FindResource("WindowBackground"), BorderBrush = new SolidColorBrush(Color.FromRgb(220, 230, 244)),
            BorderThickness = new Thickness(1), Padding = new Thickness(10), CornerRadius = new CornerRadius(10), MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = .14 }, Child = confirmationContent
        };
        Panel.SetZIndex(confirmation, 1); WindowSurface.Children.Add(confirmation);
        DockPanel.SetDock(Footer, Dock.Bottom); Layout.Children.Add(Footer);
        WindowSurface.SizeChanged += (_, _) => PositionNotifications();
        Footer.SizeChanged += (_, _) => PositionNotifications();
        notice.SizeChanged += (_, _) => PositionNotifications();
        confirmation.SizeChanged += (_, _) => PositionNotifications();
        confirmation.IsVisibleChanged += (_, _) => PositionNotifications();
        PreviewMouseWheel += (_, e) =>
        {
            if (e.OriginalSource is not Visual source) return;
            var viewer = ReferenceEquals(source, confirmation) || confirmation.IsAncestorOf(source) ? confirmationMessage
                : ReferenceEquals(source, notice) || notice.IsAncestorOf(source) ? noticeMessage : null;
            if (viewer is null) return;
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta / 120.0 * 48);
            e.Handled = true;
        };
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
    private void UpdateNotice()
    {
        noticeText.Text = Messages.Current;
        noticeMessage.ScrollToTop();
        notice.Visibility = Messages.Current.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        PositionNotifications();
    }
    private void PositionNotifications()
    {
        var footerHeight = Footer.Visibility == Visibility.Visible ? Footer.ActualHeight + Footer.Margin.Top + Footer.Margin.Bottom : 0;
        var bottom = Layout.Margin.Bottom + footerHeight + NoticeGap;
        var available = Math.Max(0, WindowSurface.ActualHeight - bottom - 12);
        var confirming = confirmation.Visibility == Visibility.Visible;
        var notifying = notice.Visibility == Visibility.Visible;
        var noticeLimit = confirming ? Math.Max(0, (available - NoticeGap) / 2) : available;
        notice.MaxHeight = noticeLimit;
        // Reserve space before the first measurement too, so a tall confirmation cannot starve a new error message.
        var noticeHeight = notifying ? Math.Min(notice.ActualHeight > 0 ? notice.ActualHeight : noticeLimit, noticeLimit) : 0;
        confirmation.Margin = new Thickness(16, 12, 16, bottom);
        confirmation.MaxHeight = Math.Max(0, available - (confirming && notifying ? noticeHeight + NoticeGap : 0));
        confirmationMessage.MaxHeight = Math.Max(0, confirmation.MaxHeight - confirmation.Padding.Top - confirmation.Padding.Bottom
            - confirmation.BorderThickness.Top - confirmation.BorderThickness.Bottom - (confirmationButtons?.ActualHeight ?? 0));
        var confirmationHeight = confirming ? Math.Min(confirmation.ActualHeight, confirmation.MaxHeight) : 0;
        notice.Margin = new Thickness(16, 12, 16, bottom + (confirming ? confirmationHeight + NoticeGap : 0));
    }
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
        confirmationContent.Children.Clear();
        confirmationMessage.Content = Ui.Text(text);
        confirmationMessage.ScrollToTop();
        confirmationContent.Children.Add(confirmationMessage);
        var buttons = choices.Select(c => Ui.Button(c.Label, () => { ClearConfirmation(); c.Action(); })).ToArray();
        foreach (var button in buttons) button.FontSize = 14;
        var buttonRow = Ui.Row(buttons); buttonRow.HorizontalAlignment = HorizontalAlignment.Right;
        confirmationButtons = buttonRow; buttonRow.SizeChanged += (_, _) => PositionNotifications();
        confirmationContent.Children.Add(buttonRow);
        confirmation.Visibility = Visibility.Visible;
        PositionNotifications();
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
