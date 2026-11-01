using System.Globalization;

namespace ClassSchedule.Core;

public readonly record struct ClassOccurrence(int Week, int DayOfWeek, int StartPeriod, int EndPeriod);
public readonly record struct ClassTime(int DayOfWeek, int StartPeriod, int EndPeriod);

public static class CourseTimeCode
{
    public static string EncodeTime(int day, int start, StudentSettings settings)
    {
        Validate(1, day, start, settings);
        return day.ToString(CultureInfo.InvariantCulture) + start.ToString("00", CultureInfo.InvariantCulture)
            + (settings.ClassesUnit == "two" ? (start + 1).ToString("00", CultureInfo.InvariantCulture) : "");
    }
    public static ClassTime DecodeTime(string code, StudentSettings settings)
    {
        if (code.Length != (settings.ClassesUnit == "two" ? 5 : 3) || code.Any(c => c is < '0' or > '9'))
            throw new InvalidDataException(settings.ClassesUnit == "two" ? "双节时间示例30102：周三第1、2节。" : "单节时间示例401：周四第1节。");
        var day = code[0] - '0'; var start = int.Parse(code.Substring(1, 2), CultureInfo.InvariantCulture);
        if (EncodeTime(day, start, settings) != code) throw new InvalidDataException("时间编码的节次配对不合法。");
        return new(day, start, settings.ClassesUnit == "two" ? start + 1 : start);
    }
    public static string Encode(int week, int day, int start, StudentSettings settings)
    {
        Validate(week, day, start, settings);
        return week.ToString(CultureInfo.InvariantCulture) + day.ToString("00", CultureInfo.InvariantCulture) + start.ToString("00", CultureInfo.InvariantCulture)
            + (settings.ClassesUnit == "two" ? (start + 1).ToString("00", CultureInfo.InvariantCulture) : "");
    }

    public static ClassOccurrence Decode(string code, StudentSettings settings)
    {
        var suffix = settings.ClassesUnit == "two" ? 6 : 4;
        if (settings.ClassesUnit is not ("one" or "two") || code.Length <= suffix || code.Any(c => c is < '0' or > '9') ||
            !int.TryParse(code[..^suffix], NumberStyles.None, CultureInfo.InvariantCulture, out var week))
            throw new InvalidDataException("上课时间编码格式不合法。");
        var day = int.Parse(code.Substring(code.Length - suffix, 2), CultureInfo.InvariantCulture);
        var start = int.Parse(code.Substring(code.Length - suffix + 2, 2), CultureInfo.InvariantCulture);
        if (Encode(week, day, start, settings) != code) throw new InvalidDataException("上课时间编码不规范，或双节不是合法配对。");
        return new(week, day, start, settings.ClassesUnit == "two" ? start + 1 : start);
    }

    public static string Format(Course course, StudentSettings settings) => string.Join(", ", course.Schedules
        .SelectMany(s => s.Weeks.Select(w => (Week: w, Day: s.DayOfWeek, Start: s.StartPeriod)))
        .OrderBy(x => x.Week).ThenBy(x => x.Day).ThenBy(x => x.Start)
        .Select(x => Encode(x.Week, x.Day, x.Start, settings)));

    private static void Validate(int week, int day, int start, StudentSettings s)
    {
        if (s.ClassesUnit is not ("one" or "two") || s.ClassesPerDay is not (8 or 10) || week < 1 || week > s.TotalWeeks || day is < 1 or > 7 ||
            start < 1 || start > s.ClassesPerDay || (s.ClassesUnit == "two" && (start % 2 == 0 || start + 1 > s.ClassesPerDay)))
            throw new InvalidDataException("上课时间编码的周次、星期、节次或模式不合法。");
    }
}
