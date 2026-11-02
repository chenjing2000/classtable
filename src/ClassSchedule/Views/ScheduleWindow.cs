using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public sealed class ScheduleWindow : DialogWindow
{
    private readonly Action saveForExit;
    public ScheduleWindow(ScheduleViewModel vm, Action<ScheduleEntry> accept, Action? deleted = null) : base(vm.IsNew ? "新增上课时段" : "编辑上课时段", 320, 390)
    {
        saveForExit = () => accept(vm.Build());
        DataContext = vm;
        MinWidth = 300;
        var weeks = Ui.RestrictedInput(nameof(vm.Weeks), WeekSelectionParser.IsInputText, "周次只接受数字、英文连字符、英文逗号和空格。", Messages.Enqueue);
        var time = Ui.NumberInput(nameof(vm.Time), 5, Messages.Enqueue); time.Width = double.NaN; time.HorizontalAlignment = HorizontalAlignment.Stretch;
        var pattern = Ui.Select(nameof(vm.Pattern), ["自定义", "仅单周", "仅双周"]);
        pattern.HorizontalContentAlignment = HorizontalAlignment.Center;
        var location = Ui.Input(nameof(vm.Location));
        var reminder = Ui.NumberInput(nameof(vm.ReminderMinutes), 4, Messages.Enqueue); reminder.Width = 70; reminder.MinWidth = 0;
        foreach (var box in new[] { weeks, time, location, reminder }) box.TextAlignment = TextAlignment.Center;
        var weeksField = Ui.Column(weeks, Ui.Note("周次：1-5, 7, 8"));
        var timeField = Ui.Column(time, Ui.Note("时间：周几与节次，如 103, 20304。"));
        var form = Ui.Form(100, ("上课周次", weeksField), ("周次筛选", pattern), ("时间", timeField), ("上课地点", location), ("提前提醒", Ui.Row(reminder, Ui.Label("分钟"))));
        // Align each parameter with its input, rather than with the input-plus-comment stack.
        foreach (var label in form.Children.OfType<TextBlock>()) { label.VerticalAlignment = VerticalAlignment.Top; label.Margin = new Thickness(4, 11, 0, 4); }
        Body(form);
        Footer.HorizontalAlignment = HorizontalAlignment.Stretch; Footer.Orientation = Orientation.Vertical;
        var bar = new DockPanel { LastChildFill = false };
        var delete = Ui.Button("删除时段", () => { deleted?.Invoke(); DiscardAndClose(); }); DockPanel.SetDock(delete, Dock.Left); bar.Children.Add(delete);
        var actions = Ui.Row(); DockPanel.SetDock(actions, Dock.Right); bar.Children.Add(actions);
        actions.Children.Add(Ui.Button("取消", DiscardAndClose));
        var acceptButton = Ui.Button("保存", () => { if (Attempt(() => accept(vm.Build()))) Accept(); }); acceptButton.IsDefault = true; actions.Children.Add(acceptButton); Footer.Children.Add(bar);
    }
    protected override void SaveApplicationExit(Action saved) { saveForExit(); saved(); }
}
