using System.IO;
using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;

public sealed class WorkspaceViewModel(StudentFileStore store) : ObservableObject
{
    public StudentSnapshot? Current { get; private set; }
    public List<string> Names { get; private set; } = [];
    public int Week { get; private set; } = 1;
    public StudentDocument? Document => Current?.Document;
    public string Title => $"第 {Week} 周";
    public string TermStatus
    {
        get
        {
            if (Document is null) return "暂无学生，请从设置菜单进入学生管理。";
            var actual = WeekCalendar.CurrentWeek(Document.Settings, DateOnly.FromDateTime(DateTime.Today));
            return actual < 1 ? "学期尚未开始 · 当前展示第1周" : actual > Document.Settings.TotalWeeks ? "本学期已结束 · 可浏览历史课程" : "";
        }
    }
    public List<string> Refresh()
    {
        var old = Current?.Name; var week = Week;
        var scan = store.Scan(); Names = scan.Students.Select(s => s.Name).ToList();
        Current = scan.Students.FirstOrDefault(s => s.Name == old) ?? scan.Students.FirstOrDefault();
        if (Current?.Name == old && Current is not null) Week = Math.Clamp(week, 1, Current.Document.Settings.TotalWeeks);
        else GoToday();
        if (old is not null && Current?.Name != old) scan.Errors.Add("当前学生文件不可用，已选择首个可用学生。");
        Changed(null); return scan.Errors;
    }
    public void Select(string name) { Current = store.Load(name); GoToday(); Changed(null); }
    public void GoToday()
    {
        Week = Document is null ? 1 : Math.Clamp(WeekCalendar.CurrentWeek(Document.Settings, DateOnly.FromDateTime(DateTime.Today)), 1, Document.Settings.TotalWeeks);
        Changed(null);
    }
    public void Move(int delta) { if (Document is not null) Week = Math.Clamp(Week + delta, 1, Document.Settings.TotalWeeks); Changed(null); }
    public void Save(StudentDocument candidate)
    {
        var old = Current ?? throw new InvalidDataException("请先选择学生。");
        Current = store.Save(old.Name, candidate, old.Fingerprint);
        Week = Math.Clamp(Week, 1, candidate.Settings.TotalWeeks); Changed(null);
    }
    public List<string> Create(string name, StudentDocument candidate)
    {
        Current = store.Save(name, candidate, null);
        var scan = store.Scan(); Names = scan.Students.Select(s => s.Name).ToList(); GoToday(); Changed(null);
        return scan.Errors;
    }
    public List<string> CreateStudent(string name, DateOnly today)
        => Create(name, StudentDocument.Create(today.AddDays(-(((int)today.DayOfWeek + 6) % 7))));
    public StudentSnapshot LoadStudent(string name) => store.Load(name);
    public List<string> RenameStudent(StudentSnapshot student, string name)
    {
        var renamed = store.Rename(student, name);
        if (Current?.Name == student.Name) Current = renamed;
        return Refresh();
    }
    public List<string> DeleteStudent(StudentSnapshot student)
    {
        store.Delete(student);
        if (Current?.Name == student.Name) Current = null;
        return Refresh();
    }
}
