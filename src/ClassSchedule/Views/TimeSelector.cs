using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ClassSchedule.Views;

public sealed class TimeSelector : StackPanel
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(TimeSelector), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (sender, _) => ((TimeSelector)sender).Sync()));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    private readonly ComboBox hour;
    private readonly ComboBox minute;
    private readonly TextBlock legacy = Ui.Note("");
    private bool syncing;
    public void CloseDropDowns() { hour.IsDropDownOpen = false; minute.IsDropDownOpen = false; }
    public TimeSelector(string property, IEnumerable<int> hours)
    {
        hour = new ComboBox { ItemsSource = hours.Select(h => h.ToString(CultureInfo.InvariantCulture)).ToList(), Width = 43, MinWidth = 0, IsEditable = false };
        minute = new ComboBox { ItemsSource = Enumerable.Range(0, 12).Select(i => (i * 5).ToString("00")).ToList(), Width = 43, MinWidth = 0, IsEditable = false };
        foreach (var combo in new[] { hour, minute })
        {
            var connected = false;
            combo.Style = (Style)Application.Current.FindResource("TimeCombo"); combo.MaxDropDownHeight = 112;
            combo.Loaded += (_, _) =>
            {
                if (connected) return;
                combo.ApplyTemplate();
                if (combo.Template.FindName("PART_Popup", combo) is not System.Windows.Controls.Primitives.Popup popup) return;
                if (popup.Child is not Border { Child: ScrollViewer viewer }) return;
                popup.Child.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler((_, e) =>
                {
                    if (e.Delta < 0) viewer.LineDown(); else viewer.LineUp();
                    e.Handled = true;
                }), true);
                connected = true;
            };
        }
        System.Windows.Automation.AutomationProperties.SetName(hour, property + "小时");
        System.Windows.Automation.AutomationProperties.SetName(minute, property + "分钟");
        Children.Add(Ui.Row(hour, Ui.Label("点"), minute, Ui.Label("分"))); Children.Add(legacy);
        hour.SelectionChanged += Changed; minute.SelectionChanged += Changed;
        SetBinding(ValueProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
    }
    private void Sync()
    {
        if (hour is null || minute is null) return;
        syncing = true; var parts = Value.Split(':');
        hour.SelectedItem = parts.Length == 2 && int.TryParse(parts[0], out var h) ? h.ToString(CultureInfo.InvariantCulture) : null;
        minute.SelectedItem = parts.Length == 2 ? parts[1] : null;
        var unsupported = hour.SelectedItem is null || minute.SelectedItem is null;
        legacy.Text = $"原值 {Value}（重新选择后替换）"; legacy.Visibility = unsupported ? Visibility.Visible : Visibility.Collapsed;
        syncing = false;
    }
    private void Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || hour.SelectedItem is not string h || minute.SelectedItem is not string m) return;
        SetCurrentValue(ValueProperty, $"{int.Parse(h, CultureInfo.InvariantCulture):00}:{m}");
    }
}
