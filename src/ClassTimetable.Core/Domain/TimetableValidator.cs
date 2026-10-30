using System.Text.RegularExpressions;

namespace ClassTimetable.Core;

public static class TimetableValidator
{
    public static void Validate(StudentDocument d)
    {
        void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        Require(d.SchemaVersion == 1, "不支持此数据版本，请保留原文件。");
        var s = d.Settings ?? throw new InvalidDataException("缺少学生设置。");
        Require(s.FirstWeekMonday.DayOfWeek == DayOfWeek.Monday, "本学期第一周周一必须是星期一。");
        Require(s.TotalWeeks is >= 1 and <= 104, "学期总周数必须为1–104。");
        Require(s.SundayBelongsTo is "previous" or "next", "周日归属不合法。");
        Require(s.ClassesPerDay is 8 or 10, "每日课时必须为8或10。");
        Require(s.ClassesUnit is "one" or "two", "课程模式必须为one或two。");
        var p = s.TimeGeneration ?? throw new InvalidDataException("缺少课时生成参数。");
        Require(p.DurationMinutes is >= 1 and <= 180 && p.OddBreakMinutes is >= 0 and <= 99 && p.EvenBreakMinutes is >= 0 and <= 99, "课程时长必须为1–180分钟，课间必须为0–99分钟。");
        ClassTimeGenerator.Minutes(p.MorningStart); ClassTimeGenerator.Minutes(p.AfternoonStart); ClassTimeGenerator.Minutes(p.EveningStart);
        var periods = s.ClassPeriods ?? throw new InvalidDataException("缺少实际课时。");
        Require(periods.Count == s.ClassesPerDay, "实际课时数量必须与每日课时一致。");
        var end = -1;
        for (var i = 0; i < periods.Count; i++)
        {
            var period = periods[i] ?? throw new InvalidDataException("课时不能为空。");
            var start = ClassTimeGenerator.Minutes(period.Start); var stop = ClassTimeGenerator.Minutes(period.End);
            Require(period.Period == i + 1 && start < stop && start >= end, $"第{i + 1}节时间不合法或与上一节重叠。"); end = stop;
        }
        try { WeekCalendar.Date(s, s.TotalWeeks, s.SundayBelongsTo == "next" ? 6 : 7); WeekCalendar.Date(s, 1, s.SundayBelongsTo == "next" ? 7 : 1); }
        catch (ArgumentOutOfRangeException) { throw new InvalidDataException("学期日期超出可表示范围。"); }
        var ids = new HashSet<Guid>();
        var occupied = new Dictionary<(int, int, int), string>();
        var courses = d.Courses ?? throw new InvalidDataException("缺少课程数组。");
        foreach (var course in courses)
        {
            if (course is null) throw new InvalidDataException("课程不能为空。");
            Require(course.Id != Guid.Empty && ids.Add(course.Id), "课程ID为空或重复。");
            Require(!string.IsNullOrWhiteSpace(course.Name), "课程名称不能为空。");
            Require(course.Color is { Length: 7 } && Regex.IsMatch(course.Color, "^#[0-9a-fA-F]{6}$"), $"{course.Name}的颜色格式无效。");
            Require(course.Note is not null, "备注不能为null，请使用空字符串。");
            var schedules = course.Schedules ?? throw new InvalidDataException("缺少上课时段。");
            Require(schedules.Count > 0, $"{course.Name}至少需要一个上课时段。");
            foreach (var slot in schedules)
            {
                if (slot is null) throw new InvalidDataException("时段不能为空。");
                Require(slot.Id != Guid.Empty && ids.Add(slot.Id), "时段ID为空或重复。");
                var weeks = slot.Weeks ?? throw new InvalidDataException("缺少周次。");
                Require(weeks.Count > 0 && weeks.SequenceEqual(weeks.Distinct().Order()) && weeks.All(w => w >= 1 && w <= s.TotalWeeks), $"{course.Name}的周次为空、重复或越界。");
                Require(slot.DayOfWeek is >= 1 and <= 7 && slot.StartPeriod >= 1 && slot.StartPeriod <= s.ClassesPerDay, $"{course.Name}的星期或节次无效。");
                Require(s.ClassesUnit != "two" || slot.StartPeriod % 2 == 1, $"{course.Name}必须选择配对双节，例如1~2。");
                Require(slot.Location is not null, "地点不能为null，请使用空字符串。");
                foreach (var week in weeks)
                    for (var i = 0; i < (s.ClassesUnit == "two" ? 2 : 1); i++)
                    {
                        var key = (week, slot.DayOfWeek, slot.StartPeriod + i);
                        if (occupied.TryGetValue(key, out var other)) throw new InvalidDataException($"第{week}周 周{slot.DayOfWeek} 第{slot.StartPeriod + i}节：{course.Name}与{other}冲突。");
                        occupied.Add(key, course.Name);
                    }
            }
        }
    }
}
