using System.IO;
using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;

public sealed class StudentManagementViewModel(WorkspaceViewModel workspace) : ObservableObject
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
        workspace.CreateStudent(StudentName, DateOnly.FromDateTime(DateTime.Today));
        Select(workspace.Current!.Name);
    }
    public void Update()
    {
        workspace.RenameStudent(Selected ?? throw new InvalidDataException("请先选择学生。"), StudentName);
        Select(StudentName);
    }
    public void Delete()
    {
        workspace.DeleteStudent(Selected ?? throw new InvalidDataException("请先选择学生。"));
        Select(null);
    }
}
