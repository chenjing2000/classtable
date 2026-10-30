using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using ClassTimetable.Core;
using ClassTimetable.ViewModels;

namespace ClassTimetable.Views;

public sealed class CourseWindow : DialogWindow
{
    public CourseWindow(CourseViewModel vm, Action<StudentDocument> save) : base(vm.IsNew ? "新建课程" : "编辑课程", 540, 550)
    {
        DataContext = vm;
        var color = Ui.Input("Course.Color"); color.ToolTip = "颜色格式：#RRGGBB";
        var swatch = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Margin = new Thickness(4) };
        swatch.SetBinding(Border.BackgroundProperty, new Binding(nameof(TextBox.Text)) { Source = color, Converter = new ColorPreviewConverter() });
        var colorField = new DockPanel(); DockPanel.SetDock(swatch, Dock.Right); colorField.Children.Add(swatch); colorField.Children.Add(color);
        var fields = Ui.Form(("课程名称", Ui.Input("Course.Name")), ("课程颜色", colorField), ("备注（可选）", Ui.Input("Course.Note")));
        var table = new DataGrid { ItemsSource = vm.Rows, IsReadOnly = true, FontSize = 12, CanUserResizeColumns = false, CanUserResizeRows = false };
        // Keep blank space reachable through the window's outer scroll area even with many slots.
        void SizeTable() => table.Height = Math.Max(200, table.ColumnHeaderHeight + (vm.Rows.Count + 1) * table.RowHeight + 4);
        table.Loaded += (_, _) => SizeTable();
        vm.Rows.CollectionChanged += (_, _) => SizeTable();
        ScrollViewer.SetVerticalScrollBarVisibility(table, ScrollBarVisibility.Disabled);
        Ui.TextColumn(table, "上课周次", "Weeks"); Ui.TextColumn(table, "时间", "Time"); Ui.TextColumn(table, "上课地点", "Location");
        var template = new DataTemplate(); var panel = new FrameworkElementFactory(typeof(StackPanel)); panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        foreach (var text in new[] { "✎", "×" })
        {
            var button = new FrameworkElementFactory(typeof(Button)); button.SetValue(ContentControl.ContentProperty, text);
            button.SetValue(Control.PaddingProperty, new Thickness(6, 2, 6, 2)); button.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            button.SetValue(FrameworkElement.ToolTipProperty, text == "✎" ? "编辑时段" : "删除时段");
            button.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, _) =>
            {
                if (((FrameworkElement)sender).DataContext is not ScheduleRow row) return;
                if (text == "✎") Edit(row.Entry);
                else { vm.Course.Schedules.Remove(row.Entry); vm.Refresh(); Dirty = true; }
            })); panel.AppendChild(button);
        }
        panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        template.VisualTree = panel; table.Columns.Add(new DataGridTemplateColumn { Header = "操作", CellTemplate = template, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        void Edit(ScheduleEntry? entry)
        {
            var dialog = new ScheduleWindow(new(vm.Candidate.Settings, entry), result =>
            {
                if (entry is null) vm.Course.Schedules.Add(result);
                else vm.Course.Schedules[vm.Course.Schedules.IndexOf(entry)] = result;
                vm.Refresh(); Dirty = true;
            }) { Owner = this }; dialog.ShowDialog();
        }
        table.MouseDoubleClick += (_, e) =>
        {
            var source = e.OriginalSource as DependencyObject;
            if (Ui.Ancestor<DataGridColumnHeader>(source) is not null || Ui.Ancestor<ScrollBar>(source) is not null || Ui.Ancestor<Button>(source) is not null) return;
            Edit(Ui.Ancestor<DataGridRow>(source)?.Item is ScheduleRow row ? row.Entry : null); e.Handled = true;
        };
        Body(Ui.Column(Ui.Text(vm.IsNew ? "新建课程" : "编辑课程", 23), Ui.Card(fields), Ui.Text("上课时段设置", 18), table));
        if (!vm.IsNew) Footer.Children.Add(Ui.Button("删除课程", () => Confirm("删除整门课程及全部上课时段？", () => { if (Attempt(() => save(vm.WithoutCourse()))) Accept(); }, "删除")));
        Footer.Children.Add(Ui.Button("取消", Close));
        var saveButton = Ui.Button("保存", () => { if (Attempt(() => save(vm.Build()))) Accept(); }); saveButton.IsDefault = true; Footer.Children.Add(saveButton);
    }
}

public sealed class ScheduleWindow : DialogWindow
{
    public ScheduleWindow(ScheduleViewModel vm, Action<ScheduleEntry> accept) : base(vm.IsNew ? "新增上课时段" : "编辑上课时段", 320, 340)
    {
        DataContext = vm;
        MinWidth = 300;
        var weeks = Ui.RestrictedInput(nameof(vm.Weeks), WeekSelectionParser.IsInputText, "周次只接受数字、英文连字符、英文逗号和空格。", Messages.Enqueue);
        var time = Ui.NumberInput(nameof(vm.Time), 5, Messages.Enqueue); time.Width = double.NaN; time.HorizontalAlignment = HorizontalAlignment.Stretch;
        var pattern = Ui.Select(nameof(vm.Pattern), ["自定义", "仅单周", "仅双周"]);
        var weeksField = Ui.Column(weeks, Ui.Note("周次：1-5, 7, 8"));
        var timeField = Ui.Column(time, Ui.Note("时间：周几与节次，如 103, 20304。"));
        var form = Ui.Form(120, ("上课周次", weeksField), ("周次筛选", pattern), ("时间", timeField), ("上课地点", Ui.Input(nameof(vm.Location))));
        // Align each parameter with its input, rather than with the input-plus-comment stack.
        foreach (var label in form.Children.OfType<TextBlock>()) { label.VerticalAlignment = VerticalAlignment.Top; label.Margin = new Thickness(4, 11, 0, 4); }
        Body(form);
        Footer.Children.Add(Ui.Button("取消", Close));
        var acceptButton = Ui.Button("确定", () => { if (Attempt(() => accept(vm.Build()))) Accept(); }); acceptButton.IsDefault = true; Footer.Children.Add(acceptButton);
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
