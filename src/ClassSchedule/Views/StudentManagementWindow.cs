using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public sealed class StudentManagementWindow : DialogWindow
{
    public StudentManagementViewModel Model { get; }
    private readonly ListBox students = new() { Name = "StudentList", Margin = new Thickness(4) };
    private readonly TextBox name;
    private readonly Grid form = new();
    private bool rendering;
    private bool confirming;
    private bool allowClose;
    public StudentManagementWindow(WorkspaceViewModel workspace) : base("学生管理", 320, 340, false)
    {
        MinWidth = 300; MinHeight = 300; ShowInTaskbar = false; Model = new(workspace); DataContext = Model;
        students.Style = (Style)FindResource("StudentList");
        Model.Select(null);
        form.RowDefinitions.Add(new() { Height = GridLength.Auto }); form.RowDefinitions.Add(new()); form.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = Ui.Text("学生管理", 20); heading.HorizontalAlignment = HorizontalAlignment.Center; form.Children.Add(heading);
        Grid.SetRow(students, 1); form.Children.Add(students);
        name = Ui.Input(nameof(Model.StudentName)); name.Name = "StudentName";
        name.MinWidth = 0; name.HorizontalAlignment = HorizontalAlignment.Right; name.TextAlignment = TextAlignment.Center;
        students.SizeChanged += (_, e) => name.Width = e.NewSize.Width / 2;
        var entry = Ui.Form(74, ("学生姓名", name));
        Grid.SetRow(entry, 2); form.Children.Add(entry); Body(form, false);
        Footer.Children.Add(Ui.Button("新增", () => Execute(Model.Create)));
        Footer.Children.Add(Ui.Button("更新", () => Execute(Model.Update)));
        Footer.Children.Add(Ui.Button("删除", RequestDelete));
        students.SelectionChanged += (_, _) =>
        {
            if (rendering) return;
            var selected = students.SelectedItem as string;
            if (Model.IsDirty)
            {
                Render(); SetFrozen(true);
                SetConfirmation("放弃尚未更新的姓名并切换学生？",
                    ("放弃修改", () => { SetFrozen(false); Execute(() => Model.Select(selected)); }), ("继续编辑", CancelConfirmation));
            }
            else Execute(() => Model.Select(selected));
        };
        KeyDown += (_, e) =>
        {
            if (e.Handled || e.Key != Key.Escape) return;
            if (confirming) CancelConfirmation(); else Close();
            e.Handled = true;
        };
        Closing += (_, e) =>
        {
            if (allowClose) return;
            if (confirming) { e.Cancel = true; CancelConfirmation(); return; }
            if (!Model.IsDirty) return;
            e.Cancel = true; SetFrozen(true);
            SetConfirmation("放弃尚未更新的姓名并关闭？", ("放弃并关闭", () => { allowClose = true; Close(); }), ("继续编辑", CancelConfirmation));
        };
        Render();
    }
    private void Execute(Action action) { Attempt(action); Render(); }
    private void Render()
    {
        rendering = true; students.ItemsSource = Model.Names; students.SelectedItem = Model.Selected?.Name; rendering = false;
    }
    private void SetFrozen(bool value) { confirming = value; form.IsEnabled = Footer.IsEnabled = !value; }
    private void CancelConfirmation() { ClearConfirmation(); SetFrozen(false); name.Focus(); }
    private void RequestDelete()
    {
        if (Model.Selected is null) { Messages.Enqueue("请先选择学生。"); return; }
        SetFrozen(true);
        SetConfirmation($"是否删除‘{Model.Selected.Name}’的全部课程信息？",
            ("是", () => { SetFrozen(false); Execute(Model.Delete); }), ("否", CancelConfirmation));
    }
}
