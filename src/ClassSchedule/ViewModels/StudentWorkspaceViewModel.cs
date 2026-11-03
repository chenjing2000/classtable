using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;

public enum WorkspacePage { Courses, Course, Settings }
public enum WorkspaceConfirmation { None, Leave, Dates, Delete }

public sealed class StudentWorkspaceViewModel(WorkspaceViewModel workspace) : ObservableObject
{
    private Action? pending;
    private StudentDocument? dateCandidate;
    public WorkspacePage Page { get; private set; }
    public WorkspaceConfirmation Confirmation { get; private set; }
    public CourseViewModel? Course { get; private set; }
    public SettingsViewModel? Settings { get; private set; }
    public Guid? SelectedCourseId { get; private set; }
    public StudentDocument Document => workspace.Document!;
    public string StudentName { get; } = workspace.Current?.Name ?? throw new InvalidOperationException("请先选择学生。");
    public bool IsDirty => Course?.IsDirty == true || Settings?.IsDirty == true;
    public event Action? CloseRequested;

    private void Open(WorkspacePage page, Guid? id = null, int? day = null, int period = 1, int week = 1)
    {
        Page = page;
        Course = page == WorkspacePage.Course ? new(Document, id, day, period, week) : null;
        Settings = page == WorkspacePage.Settings ? new(Document) { StudentName = StudentName } : null;
        Changed(null);
    }
    private void Request(Action next)
    {
        if (Confirmation != WorkspaceConfirmation.None) return;
        if (IsDirty) { pending = next; Confirmation = WorkspaceConfirmation.Leave; Changed(null); }
        else next();
    }
    public void ShowCourses() => Request(() => Open(WorkspacePage.Courses));
    public void ShowSettings() => Request(() => Open(WorkspacePage.Settings));
    public void Edit(Guid? id, int? day = null, int period = 1, int week = 1) => Request(() => Open(WorkspacePage.Course, id, day, period, week));
    public void ResetSettings() => Request(() => Open(WorkspacePage.Settings));
    public void RequestClose() => Request(() => CloseRequested?.Invoke());
    public void SaveForExit()
    {
        workspace.Save(Course is not null ? Course.Build() : Settings!.Build());
    }
    public void CancelSettings()
    {
        Settings = null; pending = null; dateCandidate = null; Confirmation = WorkspaceConfirmation.None;
        CloseRequested?.Invoke();
    }
    public void CancelConfirmation()
    {
        pending = null; dateCandidate = null; Confirmation = WorkspaceConfirmation.None; Changed(null);
    }
    public void Discard()
    {
        if (Confirmation != WorkspaceConfirmation.Leave) return;
        var next = pending; CancelConfirmation(); next?.Invoke();
    }
    public void Save()
    {
        try
        {
            var next = pending ?? (Page == WorkspacePage.Settings ? () => CloseRequested?.Invoke() : () => Open(WorkspacePage.Courses));
            var candidate = Course is not null ? Course.Build() : Settings!.Build();
            if (Settings?.ChangesDates == true)
            {
                pending = next; dateCandidate = candidate; Confirmation = WorkspaceConfirmation.Dates; Changed(null); return;
            }
            Commit(candidate, next);
        }
        catch { CancelConfirmation(); throw; }
    }
    private void Commit(StudentDocument candidate, Action next)
    {
        var selected = Course?.Course.Id;
        workspace.Save(candidate);
        if (selected is not null) SelectedCourseId = selected;
        Course = null; Settings = null;
        pending = null; dateCandidate = null; Confirmation = WorkspaceConfirmation.None; next();
    }
    public void RequestDelete(Guid id)
    {
        if (Confirmation != WorkspaceConfirmation.None) return;
        SelectedCourseId = id; Confirmation = WorkspaceConfirmation.Delete; Changed(null);
    }
    public void RequestDefaults()
    {
        if (Confirmation != WorkspaceConfirmation.None || Settings is null) return;
        Settings.Generate(true);
    }
    public void Confirm()
    {
        try
        {
            if (Confirmation == WorkspaceConfirmation.Dates) Commit(dateCandidate!, pending!);
            else if (Confirmation == WorkspaceConfirmation.Delete)
            {
                var candidate = Document.Clone(); candidate.Courses.RemoveAll(c => c.Id == SelectedCourseId);
                Commit(candidate, () => Open(WorkspacePage.Courses));
            }
        }
        catch { CancelConfirmation(); throw; }
    }
}
