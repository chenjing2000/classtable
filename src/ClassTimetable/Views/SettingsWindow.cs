using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ClassTimetable.ViewModels;
using ClassTimetable.Core;

namespace ClassTimetable.Views;

public sealed class SettingsWindow : DialogWindow
{
    public SettingsWindow(SettingsViewModel vm, Action<StudentDocument> save) : base($"总体设置（{vm.StudentName}）", 600, 600)
    {
        DataContext = vm;
        var page = new ScrollViewer { Name = "SettingsPage", VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        void ScrollPage(int delta) => page.ScrollToVerticalOffset(page.VerticalOffset - delta / 120.0 * 48);
        PreviewMouseWheel += (_, e) =>
        {
            if (Ui.Ancestor<Calendar>(e.OriginalSource as DependencyObject) is not null) return;
            ScrollPage(e.Delta); e.Handled = true;
        };
        var monday = new MondayPicker(Messages.Enqueue);
        monday.SetBinding(MondayPicker.SelectedDateProperty, new Binding(nameof(vm.FirstMonday)) { Mode = BindingMode.TwoWay });
        monday.DateChosen += () => Dirty = true;
        var mode = Ui.Choices(nameof(vm.Mode), "单节", "双节"); mode.IsEnabled = vm.CanChangeMode;
        mode.ToolTip = vm.CanChangeMode ? "尚无课程，可以选择单节或双节。" : "已有课程，删除全部课程并保存后才能修改模式。";
        var fields = Ui.Form(("第一周周一", monday), ("本学期总周数", Ui.NumberInput(nameof(vm.TotalWeeks), 3, Messages.Enqueue)),
            ("周日归属", Ui.Choices(nameof(vm.Sunday), "上一周", "下一周")), ("每日课时", Ui.Choices(nameof(vm.Count), "8", "10")), ("课程模式", mode));
        var preview = Ui.Note(""); preview.SetBinding(TextBlock.TextProperty, nameof(vm.ConversionPreview));
        var parameters = Ui.Form(("课程时长（分钟）", Ui.NumberInput(nameof(vm.Duration), 3, Messages.Enqueue)), ("奇数节后（分钟）", Ui.NumberInput(nameof(vm.OddBreak), 2, Messages.Enqueue)),
            ("偶数节后（分钟）", Ui.NumberInput(nameof(vm.EvenBreak), 2, Messages.Enqueue)), ("上午开始", new TimeSelector(nameof(vm.Morning), [8, 9, 10, 11], ScrollPage)), ("下午开始", new TimeSelector(nameof(vm.Afternoon), [13, 14, 15, 16], ScrollPage)), ("晚上开始", new TimeSelector(nameof(vm.Evening), [18, 19], ScrollPage)));
        var table = new DataGrid { ItemsSource = vm.Periods, MaxHeight = 310, RowHeight = 28, ColumnHeaderHeight = 28, FontSize = 12, CanUserResizeColumns = false, CanUserResizeRows = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Ui.TextColumn(table, "节次", "Period"); Ui.TextColumn(table, "开始时间", "Start", 1, false); Ui.TextColumn(table, "结束时间", "End", 1, false);
        Ui.TextColumn(table, "时长（分钟）", "Duration"); Ui.TextColumn(table, "课后间隔", "Gap");
        var content = Ui.Column();
        content.Children.Add(Ui.Card(Ui.Column(Ui.Text("基本信息", 18), fields, preview)));
        content.Children.Add(Ui.Card(Ui.Column(Ui.Text("课时参数", 18), parameters)));
        var tableHeader = new DockPanel();
        void CommitCells() { table.CommitEdit(DataGridEditingUnit.Cell, true); table.CommitEdit(DataGridEditingUnit.Row, true); }
        var refresh = Ui.Button("⟳", () => { CommitCells(); if (Attempt(() => vm.Generate(false))) Dirty = true; }, "根据参数生成默认时间");
        refresh.FontSize = 18; refresh.Padding = new Thickness(8, 2, 8, 2); DockPanel.SetDock(refresh, Dock.Right); tableHeader.Children.Add(refresh); tableHeader.Children.Add(Ui.Text("课时预览", 18));
        content.Children.Add(Ui.Card(Ui.Column(tableHeader, table)));
        page.Content = content; Body(page, false);
        Footer.Children.Add(Ui.Button("恢复默认课时", () => Confirm("仅恢复课时参数和实际课时，不改变学期、课程和模式。", () => { if (Attempt(() => vm.Generate(true))) Dirty = true; })));
        Footer.Children.Add(Ui.Button("取消", Close));
        var saveButton = Ui.Button("保存", () =>
        {
            CommitCells();
            void Commit() { if (Attempt(() => save(vm.Build()))) Accept(); }
            if (vm.ChangesDates) Confirm("修改学期起点会保留课程周次，并改变所有课程实际日期。是否保存？", Commit, "保存新日期");
            else Commit();
        });
        saveButton.IsDefault = true;
        Footer.Children.Add(saveButton);
    }
}
