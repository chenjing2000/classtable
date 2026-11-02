using System.IO;
using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;

public sealed class StudentManagementViewModel(WorkspaceViewModel workspace, Action<string>? report = null) : ObservableObject
{
    public List<string> Names => workspace.Names;
    public StudentSnapshot? Selected { get; private set; }
    private string studentName = "";
    public string StudentName { get => studentName; set { studentName = value; Changed(); } }
    public bool IsDirty => StudentName != (Selected?.Name ?? "");
    public void Select(string? name)
    {
        Selected = name is null ? null : workspace.LoadStudent(name);
        StudentName = Selected?.Name ?? ""; Changed(null);
    }
    public void Create()
    {
        var errors = workspace.CreateStudent(StudentName, DateOnly.FromDateTime(DateTime.Today));
        Select(workspace.Current!.Name);
        Report(errors);
    }
    public void Update()
    {
        var errors = workspace.RenameStudent(Selected ?? throw new InvalidDataException("请先选择学生。"), StudentName);
        Select(StudentName);
        Report(errors);
    }
    public void Delete()
    {
        var errors = workspace.DeleteStudent(Selected ?? throw new InvalidDataException("请先选择学生。"));
        Select(null);
        Report(errors);
    }
    private void Report(IEnumerable<string> errors) { foreach (var error in errors) report?.Invoke(error); }
}
