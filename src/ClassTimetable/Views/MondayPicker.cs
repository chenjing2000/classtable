using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ClassTimetable.Views;

public sealed class MondayPicker : StackPanel
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(MondayPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));
    public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public event Action? DateChosen;
    private readonly Calendar calendar = new() { Background = Brushes.White, BorderThickness = new Thickness(0), FirstDayOfWeek = DayOfWeek.Monday };
    private readonly Popup popup;
    private int wheelDelta;
    public MondayPicker(Action<string> rejected)
    {
        Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center;
        var label = Ui.Text(""); label.MinWidth = 112; label.SetBinding(TextBlock.TextProperty, new Binding(nameof(SelectedDate)) { Source = this, StringFormat = "{0:yyyy/MM/dd}" });
        var button = Ui.Button("▦", ToggleCalendar, "选择第一周周一");
        button.FontSize = 19; button.Padding = new Thickness(9, 2, 9, 2);
        var surface = Ui.Card(calendar); surface.Padding = new Thickness(8); surface.Margin = new Thickness(3);
        surface.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = .15 };
        popup = new Popup { Child = surface, PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
        calendar.CalendarDayButtonStyle = (Style)Application.Current.FindResource("CalendarDay");
        void UpdateBlackouts()
        {
            calendar.BlackoutDates.Clear();
            var first = new DateTime(calendar.DisplayDate.Year, calendar.DisplayDate.Month, 1);
            var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            for (var i = 0; i < 42 && start <= DateTime.MaxValue.AddDays(-i); i++)
            {
                var day = start.AddDays(i);
                if (day.DayOfWeek != DayOfWeek.Monday) calendar.BlackoutDates.Add(new CalendarDateRange(day));
            }
        }
        calendar.DisplayDateChanged += (_, _) => UpdateBlackouts();
        calendar.DisplayModeChanged += (_, _) => { if (calendar.DisplayMode != CalendarMode.Month) calendar.DisplayMode = CalendarMode.Month; };
        calendar.PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true; wheelDelta += e.Delta;
            if (Math.Abs(wheelDelta) < 120) return;
            var month = (calendar.DisplayDate.Year - 1) * 12 + calendar.DisplayDate.Month - 1;
            month = Math.Clamp(month + (wheelDelta > 0 ? -1 : 1), 0, 9999 * 12 - 1); wheelDelta = 0;
            calendar.DisplayDate = new DateTime(month / 12 + 1, month % 12 + 1, 1);
        };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not DateTime date || date == SelectedDate) return;
            if (date.DayOfWeek != DayOfWeek.Monday) { rejected("请选择星期一作为第一周周一。"); calendar.SelectedDate = SelectedDate; return; }
            SetCurrentValue(SelectedDateProperty, (DateTime?)date); DateChosen?.Invoke(); popup.IsOpen = false; button.Focus();
        };
        calendar.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { popup.IsOpen = false; button.Focus(); e.Handled = true; } };
        Children.Add(label); Children.Add(button); Children.Add(popup); UpdateBlackouts();
        Unloaded += (_, _) => popup.IsOpen = false;
    }
    private void ToggleCalendar() { calendar.DisplayDate = SelectedDate ?? DateTime.Today; calendar.SelectedDate = SelectedDate; popup.IsOpen = !popup.IsOpen; }
    private static void OnDateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((MondayPicker)sender).calendar.SelectedDate = (DateTime?)e.NewValue;
}
