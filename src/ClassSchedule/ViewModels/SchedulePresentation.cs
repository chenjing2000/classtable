using ClassSchedule.Core;

namespace ClassSchedule.ViewModels;

public sealed record ScheduleRow(ScheduleEntry Entry, string Weeks, string Time, string Location)
{
    public int ReminderMinutes => Entry.ReminderMinutes;
}

public static class SchedulePresentation
{
    public static List<ScheduleRow> Rows(Course course, StudentSettings settings) => course.Schedules
        .OrderBy(s => s, Comparer<ScheduleEntry>.Create(Compare))
        .Select(s => new ScheduleRow(s, WeekSelectionParser.Format(s.Weeks), CourseTimeCode.EncodeTime(s.DayOfWeek, s.StartPeriod, settings), s.Location)).ToList();

    private static int Compare(ScheduleEntry a, ScheduleEntry b)
    {
        var c = a.Weeks[0].CompareTo(b.Weeks[0]); if (c != 0) return c;
        c = a.DayOfWeek.CompareTo(b.DayOfWeek); if (c != 0) return c;
        c = a.StartPeriod.CompareTo(b.StartPeriod); if (c != 0) return c;
        for (var i = 0; i < Math.Min(a.Weeks.Count, b.Weeks.Count); i++) { c = a.Weeks[i].CompareTo(b.Weeks[i]); if (c != 0) return c; }
        c = a.Weeks.Count.CompareTo(b.Weeks.Count); if (c != 0) return c;
        c = StringComparer.Ordinal.Compare(a.Location, b.Location); return c != 0 ? c : a.Id.CompareTo(b.Id);
    }
}
