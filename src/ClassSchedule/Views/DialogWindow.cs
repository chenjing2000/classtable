using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

public class DialogWindow : Window
{
    private const double NoticeGap = 8;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly StackPanel notices = new() { VerticalAlignment = VerticalAlignment.Bottom };
    private Border? departingNotice;
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
        WindowSurface.ClipToBounds = true;
        Panel.SetZIndex(notices, 2); WindowSurface.Children.Add(notices);
        confirmationMessage = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        confirmationContent = new StackPanel();
        confirmation = new Border
        {
            Name = "ConfirmationNotice", Background = (Brush)FindResource("WindowBackground"), BorderBrush = new SolidColorBrush(Color.FromRgb(220, 230, 244)),
            BorderThickness = new Thickness(1), Padding = new Thickness(10), CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = .14 }, Child = confirmationContent
        };
        Panel.SetZIndex(confirmation, 1); WindowSurface.Children.Add(confirmation);
        DockPanel.SetDock(Footer, Dock.Bottom); Layout.Children.Add(Footer);
        WindowSurface.SizeChanged += (_, _) => PositionNotifications();
        Footer.SizeChanged += (_, _) => PositionNotifications();
        confirmation.SizeChanged += (_, _) => PositionNotifications();
        confirmation.IsVisibleChanged += (_, _) => PositionNotifications();
        PreviewMouseWheel += (_, e) =>
        {
            if (e.OriginalSource is not Visual source) return;
            var viewer = ReferenceEquals(source, confirmation) || confirmation.IsAncestorOf(source) ? confirmationMessage
                : notices.IsAncestorOf(source) ? Ui.Ancestor<ScrollViewer>(source) : null;
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
        Closed += (_, _) => { timer.Stop(); timer.Tick -= Tick; Messages.Changed -= UpdateNotice; Messages.Dispose(); notices.Children.Clear(); };
    }
    protected void Body(UIElement element, bool scroll = true) => Layout.Children.Add(scroll ? new ScrollViewer { Content = element, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } : element);
    private void Tick(object? sender, EventArgs e) => Messages.Advance();
    private Border CreateNotice(NotificationQueue.Notice message)
    {
        var text = Ui.Text(message.Text, 14);
        text.Foreground = new SolidColorBrush(Color.FromRgb(23, 34, 59));
        var viewer = new ScrollViewer
        {
            Content = text, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        UIElement content = viewer;
        if (message.Operation is not null)
        {
            var close = Ui.Button("×", () => Messages.DismissError(message), "关闭错误提示");
            close.VerticalAlignment = VerticalAlignment.Top;
            var row = new DockPanel();
            DockPanel.SetDock(close, Dock.Right); row.Children.Add(close); row.Children.Add(viewer);
            content = row;
        }
        return new Border
        {
            Name = "TransientNotice", Tag = message,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) { Opacity = .75 },
            Padding = new Thickness(10), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, NoticeGap, 0, 0),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = .14 },
            Child = content
        };
    }

    private void UpdateNotice()
    {
        var previous = notices.Children.Cast<Border>().ToArray();
        var visible = Messages.Visible;
        var exiting = Messages.Exiting;
        if (previous.Select(card => card.Tag).SequenceEqual(visible, ReferenceEqualityComparer.Instance)
            && ReferenceEquals(departingNotice?.Tag, exiting)) return;

        var offsets = previous.ToDictionary(card => card, card => card.TranslatePoint(new Point(), WindowSurface).Y);
        notices.Children.Clear();
        foreach (var message in visible)
            notices.Children.Add(previous.FirstOrDefault(card => ReferenceEquals(card.Tag, message)) ?? CreateNotice(message));

        bool newExit = !ReferenceEquals(departingNotice?.Tag, exiting);
        if (newExit)
        {
            if (departingNotice is not null) WindowSurface.Children.Remove(departingNotice);
            departingNotice = exiting is null ? null : previous.FirstOrDefault(card => ReferenceEquals(card.Tag, exiting)) ?? CreateNotice(exiting);
            if (departingNotice is { } card)
            {
                card.IsHitTestVisible = false; card.VerticalAlignment = VerticalAlignment.Bottom;
                Panel.SetZIndex(card, 2); WindowSurface.Children.Insert(WindowSurface.Children.IndexOf(notices), card);
            }
        }

        PositionNotifications();
        if (!IsLoaded) return;
        WindowSurface.UpdateLayout();
        foreach (Border card in notices.Children)
        {
            if (!offsets.TryGetValue(card, out var oldTop)) continue;
            card.RenderTransform = Transform.Identity;
            var shift = oldTop - card.TranslatePoint(new Point(), WindowSurface).Y;
            if (Math.Abs(shift) > .5) Slide(card, shift, 0);
        }
        if (newExit && departingNotice is { } outgoing)
        {
            outgoing.RenderTransform = Transform.Identity;
            var top = outgoing.TranslatePoint(new Point(), WindowSurface).Y;
            var shift = offsets.TryGetValue(outgoing, out var previousTop) ? previousTop - top : 0;
            Slide(outgoing, shift, WindowSurface.ActualHeight - top);
        }
    }

    private static void Slide(Border card, double from, double to)
    {
        var transform = new TranslateTransform();
        card.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from, to, new Duration(NotificationQueue.ExitDuration)));
    }

    private void PositionNotifications()
    {
        var footerHeight = Footer.Visibility == Visibility.Visible ? Footer.ActualHeight + Footer.Margin.Top + Footer.Margin.Bottom : 0;
        var bottom = Layout.Margin.Bottom + footerHeight + NoticeGap;
        var available = Math.Max(0, WindowSurface.ActualHeight - bottom - Layout.Margin.Top);
        var confirming = confirmation.Visibility == Visibility.Visible;
        var notifying = notices.Children.Count > 0 || departingNotice is not null;
        confirmation.Margin = new Thickness(Layout.Margin.Left, Layout.Margin.Top, Layout.Margin.Right, bottom);
        confirmation.MaxHeight = confirming && notifying ? available / 2 : available;
        confirmationMessage.MaxHeight = Math.Max(0, confirmation.MaxHeight - confirmation.Padding.Top - confirmation.Padding.Bottom
            - confirmation.BorderThickness.Top - confirmation.BorderThickness.Bottom - (confirmationButtons?.ActualHeight ?? 0));
        var noticeBottom = confirming ? bottom + Math.Min(confirmation.ActualHeight, confirmation.MaxHeight) + NoticeGap : Layout.Margin.Bottom;
        notices.Margin = new Thickness(Layout.Margin.Left, 0, Layout.Margin.Right, noticeBottom);
        var noticeHeight = Math.Max(0, WindowSurface.ActualHeight - noticeBottom - Layout.Margin.Top);
        var maxCardHeight = noticeHeight / Math.Max(1, notices.Children.Count);
        foreach (Border card in notices.Children) card.MaxHeight = Math.Max(0, maxCardHeight - NoticeGap);
        if (departingNotice is { } outgoing)
        {
            outgoing.Margin = notices.Margin;
            outgoing.MaxHeight = noticeHeight;
        }
    }

    public bool Attempt(Action action, object? operation = null)
    {
        operation ??= action.Method;
        try { action(); Messages.ResolveErrors(operation); return true; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException)
        { Messages.Error(ex.Message, operation); return false; }
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
