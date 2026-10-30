using System.Collections.ObjectModel;
using System.IO;
using ClassTimetable.Core;

namespace ClassTimetable.ViewModels;


public sealed class CourseViewModel
{
    public StudentDocument Candidate { get; }
    public Course Course { get; }
    public bool IsNew { get; }
    public ObservableCollection<ScheduleRow> Rows { get; } = [];
    public CourseViewModel(StudentDocument document, Guid? courseId, int? day = null, int period = 1, int week = 1)
    {
        Candidate = document.Clone(); IsNew = courseId is null;
        Course = IsNew ? new Course() : Candidate.Courses.Single(c => c.Id == courseId);
        if (IsNew)
        {
            Course.Color = CourseColors.Suggest(document.Courses.Select(c => c.Color));
            Candidate.Courses.Add(Course);
            if (day is not null) Course.Schedules.Add(new ScheduleEntry { DayOfWeek = day.Value, StartPeriod = Candidate.Settings.ClassesUnit == "two" ? ((period - 1) / 2) * 2 + 1 : period, Weeks = [week] });
        }
        Refresh();
    }
    public void Refresh()
    {
        Rows.Clear();
        foreach (var row in SchedulePresentation.Rows(Course, Candidate.Settings)) Rows.Add(row);
    }
    public StudentDocument Build() { Course.Name = Course.Name.Trim(); TimetableValidator.Validate(Candidate); return Candidate; }
    public StudentDocument WithoutCourse() { var d = Candidate.Clone(); d.Courses.RemoveAll(c => c.Id == Course.Id); TimetableValidator.Validate(d); return d; }
}

public sealed class ScheduleViewModel
{
    private readonly ScheduleEntry? original;
    private readonly StudentSettings settings;
    public string Weeks { get; set; }
    public string Pattern { get; set; } = "自定义";
    public string Time { get; set; }
    public string Location { get; set; }
    public bool IsNew => original is null;
    public ScheduleViewModel(StudentSettings s, ScheduleEntry? entry)
    {
        original = entry; settings = s;
        Weeks = entry is null ? $"1-{s.TotalWeeks}" : WeekSelectionParser.Format(entry.Weeks);
        Time = CourseTimeCode.EncodeTime(entry?.DayOfWeek ?? 1, entry?.StartPeriod ?? 1, s);
        Location = entry?.Location ?? "";
    }
    public ScheduleEntry Build()
    {
        var weeks = WeekSelectionParser.Parse(Weeks, settings.TotalWeeks);
        if (Pattern == "仅单周") weeks = weeks.Where(w => w % 2 == 1).ToList();
        if (Pattern == "仅双周") weeks = weeks.Where(w => w % 2 == 0).ToList();
        if (weeks.Count == 0) throw new InvalidDataException("筛选后没有上课周次。");
        var time = CourseTimeCode.DecodeTime(Time, settings);
        return new() { Id = original?.Id ?? Guid.NewGuid(), Weeks = weeks, DayOfWeek = time.DayOfWeek, StartPeriod = time.StartPeriod, Location = Location.Trim() };
    }
}
