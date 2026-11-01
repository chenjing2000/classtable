using System.Globalization;

namespace ClassSchedule.Core;

public static class CourseColors
{
    public static string Suggest(IEnumerable<string> colors) => Suggest(colors, Random.Shared.NextDouble());
    public static string Suggest(IEnumerable<string> colors, double sample)
    {
        if (sample is < 0 or >= 1 || double.IsNaN(sample)) throw new ArgumentOutOfRangeException(nameof(sample));
        var hues = colors.Select(Hue).Where(h => h.HasValue).Select(h => h!.Value).Distinct().Order().ToList();
        var hue = sample * 360;
        if (hues.Count > 0)
        {
            var gaps = hues.Select((h, i) => (Start: h, Size: (i + 1 < hues.Count ? hues[i + 1] : hues[0] + 360) - h)).ToList();
            var largest = gaps.Max(g => g.Size); var choices = gaps.Where(g => Math.Abs(g.Size - largest) < .000001).ToList();
            var scaled = sample * choices.Count; var gap = choices[(int)scaled];
            hue = (gap.Start + gap.Size * (.4 + .2 * (scaled - Math.Floor(scaled)))) % 360;
        }
        // Fixed lightness/saturation keep black text readable; jitter stays in the middle of the largest hue gap.
        const double lightness = .82, saturation = .6;
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation; var x = c * (1 - Math.Abs(hue / 60 % 2 - 1)); var m = lightness - c / 2;
        var (r, g, b) = (int)(hue / 60) switch { 0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x), 3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x) };
        return $"#{(int)Math.Round((r + m) * 255):X2}{(int)Math.Round((g + m) * 255):X2}{(int)Math.Round((b + m) * 255):X2}";
    }
    private static double? Hue(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color)) throw new InvalidDataException("颜色必须为#RRGGBB。");
        var r = (color >> 16) & 255; var g = (color >> 8) & 255; var b = color & 255;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
        if (delta == 0) return null;
        var h = max == r ? (g - b) / (double)delta % 6 : max == g ? (b - r) / (double)delta + 2 : (r - g) / (double)delta + 4;
        return (h * 60 + 360) % 360;
    }
}
