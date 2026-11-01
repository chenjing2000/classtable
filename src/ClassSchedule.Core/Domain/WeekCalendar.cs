namespace ClassSchedule.Core;

public static class WeekCalendar
{
    public static DateOnly Date(StudentSettings s, int week, int day) =>
        s.FirstWeekMonday.AddDays(7 * (week - 1) + (day == 7 && s.SundayBelongsTo == "next" ? -1 : day - 1));

    public static int CurrentWeek(StudentSettings s, DateOnly date) =>
        (int)Math.Floor((date.DayNumber - s.FirstWeekMonday.DayNumber + (s.SundayBelongsTo == "next" ? 1 : 0)) / 7.0) + 1;

    public static StudentDocument ChangeSunday(StudentDocument original, string target)
    {
        if (target is not ("previous" or "next")) throw new InvalidDataException("周日归属无效。");
        var copy = original.Clone();
        if (target == copy.Settings.SundayBelongsTo) return copy;
        copy.Settings.SundayBelongsTo = target;
        var slots = original.Courses.SelectMany(c => c.Schedules).ToList();
        var dates = slots.Select(s => s.Weeks.Select(w => Date(original.Settings, w, s.DayOfWeek)).ToList()).ToList();
        if (dates.SelectMany(x => x).Any(d => CurrentWeek(copy.Settings, d) < 1))
            copy.Settings.FirstWeekMonday = copy.Settings.FirstWeekMonday.AddDays(-7);
        var newSlots = copy.Courses.SelectMany(c => c.Schedules).ToList();
        for (var i = 0; i < newSlots.Count; i++)
        {
            newSlots[i].Weeks = dates[i].Select(d => CurrentWeek(copy.Settings, d)).Distinct().Order().ToList();
            copy.Settings.TotalWeeks = Math.Max(copy.Settings.TotalWeeks, newSlots[i].Weeks.Max());
        }
        TimetableValidator.Validate(copy);
        return copy;
    }
}
