using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassSchedule.Core;

public static class WeekSelectionParser
{
    public static bool IsInputText(string text) => text.All(c => c is >= '0' and <= '9' or '-' or ',' or ' ');
    public static string Format(IEnumerable<int> weeks)
    {
        var values = weeks.Distinct().Order().ToList(); var parts = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            var first = values[i]; var last = first;
            while (i + 1 < values.Count && values[i + 1] == last + 1) last = values[++i];
            parts.Add(first == last ? first.ToString(CultureInfo.InvariantCulture) : $"{first}-{last}");
        }
        return string.Join(", ", parts);
    }
    public static List<int> Parse(string text, int totalWeeks)
    {
        var result = new SortedSet<int>();
        if (!IsInputText(text)) throw new InvalidDataException("周次只接受ASCII数字、英文连字符、英文逗号和空格。");
        foreach (var token in text.Split(','))
        {
            var match = Regex.Match(token.Trim(), @"^([0-9]+)(?:\s*-\s*([0-9]+))?$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var first))
                throw new InvalidDataException("周次格式示例：1-16 或 1-4,7,9-12。");
            var last = first;
            if (match.Groups[2].Success && !int.TryParse(match.Groups[2].Value, out last)) throw new InvalidDataException("周次数字过大。");
            if (first < 1 || last < first || last > totalWeeks) throw new InvalidDataException($"周次必须在1–{totalWeeks}内，区间起点不能大于终点。");
            for (var w = first; w <= last; w++) result.Add(w);
        }
        return result.ToList();
    }
}
