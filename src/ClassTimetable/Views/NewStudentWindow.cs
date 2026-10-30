using ClassTimetable.Core;
using ClassTimetable.ViewModels;

namespace ClassTimetable.Views;

public sealed class NewStudentWindow : DialogWindow
{
    public NewStudentWindow(NewStudentViewModel vm, Action<string, StudentDocument> save) : base("新增学生", 480, 300)
    {
        DataContext = vm;
        Body(Ui.Card(Ui.Form(("学生姓名", Ui.Input(nameof(vm.StudentName))))));
        Footer.Children.Add(Ui.Button("取消", Close));
        var create = Ui.Button("创建", () =>
        {
            if (Attempt(() => save(vm.StudentName, vm.Build(DateOnly.FromDateTime(DateTime.Today))))) Accept();
        });
        create.IsDefault = true; Footer.Children.Add(create);
    }
}
