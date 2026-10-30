using System.Windows;
using System.Windows.Controls;
using ClassTimetable.ViewModels;

namespace ClassTimetable.Views;

public sealed class CourseManagerWindow : DialogWindow
{

    public CourseManagerWindow(WorkspaceViewModel vm, Action<Guid?, int?, int, Window?> edit) : base("管理课程", 600, 500, false)
    {
        var table = new GroupedCourseTable();
        void Reload() => table.Load(vm.Document);
        void Open(Guid? id) { edit(id, null, 1, this); Reload(); }
        table.Activated += Open;
        Reload(); Body(table, false);
        Footer.Children.Add(Ui.Button("新建", () => Open(null)));
        Footer.Children.Add(Ui.Button("删除", () =>
        {
            var selected = vm.Document?.Courses.FirstOrDefault(c => c.Id == table.SelectedCourseId);
            if (selected is null) { Messages.Enqueue("请先选择一门课程。"); return; }
            Confirm($"删除“{selected.Name}”及全部时段？", () =>
            {
                if (Attempt(() => { var d = vm.Document!.Clone(); d.Courses.RemoveAll(c => c.Id == selected.Id); vm.Save(d); })) { Reload(); Messages.Enqueue("课程已删除。"); }
            }, "删除");
        }));
        KeyDown += (_, e) => { if (!e.Handled && e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
    }
}
