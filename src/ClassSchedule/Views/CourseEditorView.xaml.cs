using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public partial class CourseEditorView : UserControl
{
    public CourseEditorView(CourseViewModel vm)
    {
        InitializeComponent(); DataContext = vm;
        var swatch = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Margin = new Thickness(4), ToolTip = "自动生成的课程颜色" };
        swatch.SetBinding(Border.BackgroundProperty, new Binding("Course.Color") { Converter = new ColorPreviewConverter() });
        var nameField = new DockPanel(); DockPanel.SetDock(swatch, Dock.Right); nameField.Children.Add(swatch); nameField.Children.Add(Ui.Input("Course.Name"));
        var count = Ui.NumberInput(nameof(vm.ReminderCount), 2, message => (Window.GetWindow(this) as DialogWindow)?.Messages.Enqueue(message)); count.Width = 48; count.MinWidth = 0; count.TextAlignment = TextAlignment.Center;
        var interval = Ui.NumberInput(nameof(vm.ReminderIntervalMinutes), 3, message => (Window.GetWindow(this) as DialogWindow)?.Messages.Enqueue(message)); interval.Width = 48; interval.MinWidth = 0; interval.TextAlignment = TextAlignment.Center;
        var duration = Ui.NumberInput(nameof(vm.ReminderDurationMinutes), 3, message => (Window.GetWindow(this) as DialogWindow)?.Messages.Enqueue(message)); duration.Width = 48; duration.MinWidth = 0; duration.TextAlignment = TextAlignment.Center;
        var fields = Ui.Form(78, ("课程名称", nameField), ("提醒次数", Ui.Row(count, Ui.Label("次，间隔"), interval, Ui.Label("分钟，时长"), duration, Ui.Label("分钟"))));
        var table = Table; table.ItemsSource = vm.Rows;
        TableHost.SizeChanged += (_, e) => table.Height = Math.Min(table.ColumnHeaderHeight + table.RowHeight * 5 + 2, e.NewSize.Height);
        Ui.TextColumn(table, "上课周次", "Weeks"); Ui.TextColumn(table, "时间", "Time"); Ui.TextColumn(table, "上课地点", "Location"); Ui.TextColumn(table, "提前提醒", "ReminderMinutes");
        void Edit(ScheduleEntry? entry)
        {
            var dialog = new ScheduleWindow(new(vm.Candidate.Settings, entry), result =>
            {
                if (entry is null) vm.Course.Schedules.Add(result);
                else vm.Course.Schedules[vm.Course.Schedules.IndexOf(entry)] = result;
                vm.Refresh();
            }, () => { if (entry is not null) { vm.Course.Schedules.Remove(entry); vm.Refresh(); } }) { Owner = Window.GetWindow(this) }; dialog.ShowDialog();
        }
        table.MouseDoubleClick += (_, e) =>
        {
            var source = e.OriginalSource as DependencyObject;
            if (Ui.Ancestor<DataGridColumnHeader>(source) is not null || Ui.Ancestor<ScrollBar>(source) is not null || Ui.Ancestor<Button>(source) is not null) return;
            Edit(Ui.Ancestor<DataGridRow>(source)?.Item is ScheduleRow row ? row.Entry : null); e.Handled = true;
        };
        Fields.Content = Ui.Card(fields);
        AddSlot.Click += (_, _) => Edit(null);
    }
}

internal sealed class ColorPreviewConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        return text is { Length: 7 } && text[0] == '#' && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)) : Brushes.Transparent;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
