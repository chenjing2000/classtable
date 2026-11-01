namespace ClassSchedule.Core;

public sealed record ReminderNotice(string StudentName, Guid CourseId, Guid ScheduleId, string CourseName,
    string Location, DateTime ClassStart, DateTime StartedAt, int Number, int Total, int DurationMinutes);

// A session owns the progress for one selected student. Dismissing a visible alert
// does not alter this schedule; only switching students or editing a plan does.
public sealed class ReminderSession
{
    private sealed class Pending(string student, Course course, ScheduleEntry slot, DateTime start, DateTime first)
    {
        public string Student = student;
        public Course Course = course;
        public ScheduleEntry Slot = slot;
        public DateTime Start = start;
        public DateTime Next = first;
        public int Fired;
        public readonly (int Count, int Interval, int Lead) Timing = (course.ReminderCount, course.ReminderIntervalMinutes, slot.ReminderMinutes);
    }
    private readonly Dictionary<(Guid Course, Guid Slot, DateTime Start), Pending> pending = [];
    private string? student;

    public void Synchronize(string? name, StudentDocument? document, DateTime now)
    {
        if (student != name) { pending.Clear(); student = name; }
        var retained = new HashSet<(Guid, Guid, DateTime)>();
        if (document is not null && name is not null)
            foreach (var course in document.Courses)
                foreach (var slot in course.Schedules)
                    foreach (var week in slot.Weeks)
                    {
                        var day = WeekCalendar.Date(document.Settings, week, slot.DayOfWeek);
                        var start = day.ToDateTime(TimeOnly.ParseExact(document.Settings.ClassPeriods[slot.StartPeriod - 1].Start, "HH:mm"));
                        var key = (course.Id, slot.Id, start);
                        if (pending.TryGetValue(key, out var existing) && existing.Timing == (course.ReminderCount, course.ReminderIntervalMinutes, slot.ReminderMinutes))
                        {
                            existing.Course = course; existing.Slot = slot; retained.Add(key); continue;
                        }
                        if (start <= now) continue;
                        var lead = TimeSpan.FromMinutes(slot.ReminderMinutes).Ticks;
                        var first = new DateTime(Math.Max(DateTime.MinValue.Ticks, start.Ticks - lead));
                        pending[key] = new(name, course, slot, start, first < now ? now : first); retained.Add(key);
                    }
        foreach (var key in pending.Keys.Where(k => !retained.Contains(k)).ToList()) pending.Remove(key);
    }

    public List<ReminderNotice> Tick(DateTime now)
    {
        var result = new List<ReminderNotice>();
        foreach (var item in pending.Values.OrderBy(p => p.Next))
        {
            if (item.Fired >= item.Timing.Count || item.Next > now) continue;
            item.Fired++;
            result.Add(new(item.Student, item.Course.Id, item.Slot.Id, item.Course.Name, item.Slot.Location, item.Start, now, item.Fired, item.Timing.Count, item.Course.ReminderDurationMinutes));
            var interval = TimeSpan.FromMinutes(item.Timing.Interval).Ticks;
            item.Next = new DateTime(Math.Min(DateTime.MaxValue.Ticks, now.Ticks + interval));
        }
        return result;
    }
}
