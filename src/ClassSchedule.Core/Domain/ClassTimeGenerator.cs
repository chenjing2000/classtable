using System.Globalization;

namespace ClassSchedule.Core;

public static class ClassTimeGenerator
{
    public static int Minutes(string value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new InvalidDataException($"时间“{value}”必须使用HH:mm格式。");
        return time.Hour * 60 + time.Minute;
    }
    private static string Format(int value)
    {
        if (value is < 0 or >= 1440) throw new InvalidDataException("课时不能跨午夜，请调整参数。");
        return $"{value / 60:00}:{value % 60:00}";
    }
    public static List<ClassPeriod> Generate(StudentSettings settings)
    {
        var p = settings.TimeGeneration;
        if (settings.ClassesPerDay is not (8 or 10) || p.DurationMinutes is < 1 or > 180 || p.OddBreakMinutes is < 0 or > 99 || p.EvenBreakMinutes is < 0 or > 99)
            throw new InvalidDataException("每日节数、时长或课间参数不合法。");
        var result = new List<ClassPeriod>();
        var current = 0;
        for (var i = 1; i <= settings.ClassesPerDay; i++)
        {
            if (i == 1) current = Minutes(p.MorningStart);
            if (i == 5) current = Minutes(p.AfternoonStart);
            if (i == 9) current = Minutes(p.EveningStart);
            if (result.Count > 0 && current < Minutes(result[^1].End)) throw new InvalidDataException("生成课时发生重叠，请调整课程时长、课间或分段开始时间。");
            result.Add(new ClassPeriod { Period = i, Start = Format(current), End = Format(current + p.DurationMinutes) });
            current += p.DurationMinutes + (i % 2 == 1 ? p.OddBreakMinutes : p.EvenBreakMinutes);
        }
        return result;
    }
}
