using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ClassSchedule.ViewModels;
using ClassSchedule.Core;

namespace ClassSchedule.Views;

public partial class SettingsView : UserControl
{
    private readonly TimeSelector[] timeSelectors;
    public SettingsView(SettingsViewModel vm, Action<string> rejected)
    {
        InitializeComponent(); DataContext = vm;
        var page = SettingsPage;
        timeSelectors = [new TimeSelector(nameof(vm.Morning), [8, 9, 10, 11]), new TimeSelector(nameof(vm.Afternoon), [13, 14, 15, 16]), new TimeSelector(nameof(vm.Evening), [18, 19])];
        var monday = new MondayPicker(rejected);
        monday.SetBinding(MondayPicker.SelectedDateProperty, new Binding(nameof(vm.FirstMonday)) { Mode = BindingMode.TwoWay });
        var mode = Ui.Choices(nameof(vm.Mode), "单节", "双节"); mode.IsEnabled = vm.CanChangeMode;
        mode.ToolTip = vm.CanChangeMode ? "尚无课程，可以选择单节或双节。" : "已有课程，删除全部课程并保存后才能修改模式。";
        var fields = Ui.Form(92, ("第一周周一", monday), ("本学期总周数", Ui.NumberInput(nameof(vm.TotalWeeks), 3, rejected)),
            ("周日归属", Ui.Choices(nameof(vm.Sunday), "上一周", "下一周")), ("每日课时", Ui.Choices(nameof(vm.Count), "8", "10")), ("课程模式", mode));
        var preview = Ui.Note(""); preview.SetBinding(TextBlock.TextProperty, nameof(vm.ConversionPreview));
        FrameworkElement MinutesInput(string property, int digits)
        {
            var box = Ui.NumberInput(property, digits, rejected); box.Width = 64; box.MinWidth = 0;
            return Ui.Row(box, Ui.Label("分"));
        }
        var parameters = Ui.Form(70, ("课程时长", MinutesInput(nameof(vm.Duration), 3)), ("奇数节后", MinutesInput(nameof(vm.OddBreak), 2)),
            ("偶数节后", MinutesInput(nameof(vm.EvenBreak), 2)), ("上午开始", timeSelectors[0]), ("下午开始", timeSelectors[1]), ("晚上开始", timeSelectors[2]));
        periodTable = new DataGrid { ItemsSource = vm.Periods, MaxHeight = 310, RowHeight = 28, ColumnHeaderHeight = 28, FontSize = 12, CanUserResizeColumns = false, CanUserResizeRows = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var table = periodTable;
        Ui.TextColumn(table, "节次", "Period"); Ui.TextColumn(table, "开始时间", "Start", 1, false); Ui.TextColumn(table, "结束时间", "End", 1, false);
        Ui.TextColumn(table, "时长（分钟）", "Duration"); Ui.TextColumn(table, "课后间隔", "Gap");
        var content = Ui.Column();
        var basic = Ui.Card(Ui.Column(Ui.Text("基本信息", 18), fields, preview)); basic.Name = "BasicInformation";
        var timing = Ui.Card(Ui.Column(Ui.Text("课时参数", 18), parameters)); timing.Name = "ClassParameters";
        var groups = new Grid { Name = "SettingsGroups" }; groups.Children.Add(basic); groups.Children.Add(timing);
        bool? sideBySide = null;
        void ArrangeGroups(double width)
        {
            var side = width >= 530; if (side == sideBySide) return; sideBySide = side;
            groups.RowDefinitions.Clear(); groups.ColumnDefinitions.Clear();
            if (side)
            {
                groups.ColumnDefinitions.Add(new() { Width = new GridLength(1.1, GridUnitType.Star) });
                groups.ColumnDefinitions.Add(new() { Width = new GridLength(10) }); groups.ColumnDefinitions.Add(new());
                Grid.SetRow(timing, 0); Grid.SetColumn(timing, 2);
            }
            else
            {
                groups.ColumnDefinitions.Add(new()); groups.RowDefinitions.Add(new() { Height = GridLength.Auto }); groups.RowDefinitions.Add(new() { Height = GridLength.Auto });
                Grid.SetRow(timing, 1); Grid.SetColumn(timing, 0);
            }
        }
        groups.SizeChanged += (_, e) => ArrangeGroups(e.NewSize.Width); ArrangeGroups(600);
        content.Children.Add(groups);
        var tableHeader = new DockPanel();
        var refresh = Ui.Button("⟳", () => { CommitCells(); try { vm.Generate(false); } catch (Exception ex) when (ex is System.IO.InvalidDataException or FormatException or OverflowException or ArgumentException) { rejected(ex.Message); } }, "根据参数生成默认时间");
        refresh.FontSize = 18; refresh.Padding = new Thickness(8, 2, 8, 2); DockPanel.SetDock(refresh, Dock.Right); tableHeader.Children.Add(refresh); tableHeader.Children.Add(Ui.Text("课时预览", 18));
        content.Children.Add(Ui.Card(Ui.Column(tableHeader, table)));
        page.Content = content;
    }
    private DataGrid periodTable;
    internal void HandlePageWheel(MouseWheelEventArgs e)
    {
        // Popups have their own visual tree and consume their own wheel events.
        if (e.OriginalSource is System.Windows.Media.Visual source && Window.GetWindow(this) is { } owner && !owner.IsAncestorOf(source)) return;
        if (Ui.Ancestor<Calendar>(e.OriginalSource as DependencyObject) is not null) return;
        foreach (var selector in timeSelectors) selector.CloseDropDowns();
        SettingsPage.ScrollToVerticalOffset(SettingsPage.VerticalOffset - e.Delta / 120.0 * 48);
        e.Handled = true;
    }
    public void CommitCells() { periodTable.CommitEdit(DataGridEditingUnit.Cell, true); periodTable.CommitEdit(DataGridEditingUnit.Row, true); }
}
