using ClassTimetable.Core;

namespace ClassTimetable.ViewModels;

public sealed class NewStudentViewModel
{
    public string StudentName { get; set; } = "蜡笔小新";
    public StudentDocument Build(DateOnly today) => StudentDocument.Create(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)));
}
