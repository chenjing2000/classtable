using System.Collections.ObjectModel;
using System.IO;
using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;


public sealed class CourseViewModel
{
    public StudentDocument Candidate { get; }
    public Course Course { get; }
    private readonly string initial;
    public string ReminderCount { get; set; }
    public string ReminderIntervalMinutes { get; set; }
    public string ReminderDurationMinutes { get; set; }
    private string Snapshot() => System.Text.Json.JsonSerializer.Serialize(new { Course, ReminderCount, ReminderIntervalMinutes, ReminderDurationMinutes });
    public bool IsDirty => initial != Snapshot();
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
        ReminderCount = Course.ReminderCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReminderIntervalMinutes = Course.ReminderIntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReminderDurationMinutes = Course.ReminderDurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Refresh();
        initial = Snapshot();
    }
    public void Refresh()
    {
        Rows.Clear();
        foreach (var row in SchedulePresentation.Rows(Course, Candidate.Settings)) Rows.Add(row);
    }
    public StudentDocument Build()
    {
        var candidate = Candidate.Clone();
        var course = candidate.Courses.Single(c => c.Id == Course.Id); course.Name = course.Name.Trim();
        if (!NumericText.IsValid(ReminderCount, 2) || !int.TryParse(ReminderCount, out var count) ||
            !NumericText.IsValid(ReminderIntervalMinutes, 3) || !int.TryParse(ReminderIntervalMinutes, out var interval) ||
            !NumericText.IsValid(ReminderDurationMinutes, 3) || !int.TryParse(ReminderDurationMinutes, out var duration))
            throw new InvalidDataException("请输入有效的提醒次数、间隔和时长。");
        course.ReminderCount = count; course.ReminderIntervalMinutes = interval; course.ReminderDurationMinutes = duration;
        TimetableValidator.Validate(candidate); return candidate;
    }
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
    public string ReminderMinutes { get; set; }
    public ScheduleViewModel(StudentSettings s, ScheduleEntry? entry)
    {
        original = entry; settings = s;
        Weeks = entry is null ? $"1-{s.TotalWeeks}" : WeekSelectionParser.Format(entry.Weeks);
        Time = CourseTimeCode.EncodeTime(entry?.DayOfWeek ?? 1, entry?.StartPeriod ?? 1, s);
        Location = entry?.Location ?? "";
        ReminderMinutes = (entry?.ReminderMinutes ?? 30).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public ScheduleEntry Build()
    {
        var weeks = WeekSelectionParser.Parse(Weeks, settings.TotalWeeks);
        if (Pattern == "仅单周") weeks = weeks.Where(w => w % 2 == 1).ToList();
        if (Pattern == "仅双周") weeks = weeks.Where(w => w % 2 == 0).ToList();
        if (weeks.Count == 0) throw new InvalidDataException("筛选后没有上课周次。");
        var time = CourseTimeCode.DecodeTime(Time, settings);
        if (!NumericText.IsValid(ReminderMinutes, 4) || !int.TryParse(ReminderMinutes, out var reminder) || reminder > 9999)
            throw new InvalidDataException("提前提醒必须为0–9999分钟。");
        return new() { Id = original?.Id ?? Guid.NewGuid(), Weeks = weeks, DayOfWeek = time.DayOfWeek, StartPeriod = time.StartPeriod, Location = Location.Trim(), ReminderMinutes = reminder };
    }
}
