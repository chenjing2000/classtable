using System.Collections.ObjectModel;
using System.IO;
using ClassTimetable.Core;

namespace ClassTimetable.ViewModels;

public sealed class PeriodRow : ObservableObject
{
    private string start;
    private string end;
    private string gap = "—";
    public int Period { get; }
    public string Start { get => start; set { if (Set(ref start, value)) { Changed(nameof(Duration)); Edited?.Invoke(); } } }
    public string End { get => end; set { if (Set(ref end, value)) { Changed(nameof(Duration)); Edited?.Invoke(); } } }
    public string Duration { get { try { return (ClassTimeGenerator.Minutes(End) - ClassTimeGenerator.Minutes(Start)).ToString(); } catch (InvalidDataException) { return "—"; } } }
    public string Gap { get => gap; set => Set(ref gap, value); }
    public Action? Edited { get; set; }
    public PeriodRow(ClassPeriod p) { Period = p.Period; start = p.Start; end = p.End; }
    public ClassPeriod ToModel() => new() { Period = Period, Start = Start, End = End };
}

public sealed class SettingsViewModel : ObservableObject
{
    private readonly StudentDocument original;
    private string count;
    private string sunday;
    private DateTime? firstMonday;
    public string StudentName { get; set; } = "";
    public bool CanChangeMode => original.Courses.Count == 0;
    public DateTime? FirstMonday { get => firstMonday; set => Set(ref firstMonday, value); }
    public string TotalWeeks { get; set; }
    public string Mode { get; set; }
    public string Duration { get; set; }
    public string OddBreak { get; set; }
    public string EvenBreak { get; set; }
    public string Morning { get; set; }
    public string Afternoon { get; set; }
    public string Evening { get; set; }
    public ObservableCollection<PeriodRow> Periods { get; } = [];
    public string Sunday { get => sunday; set { if (Set(ref sunday, value)) Changed(nameof(ConversionPreview)); } }
    public string Count
    {
        get => count;
        set
        {
            if (!Set(ref count, value)) return;
            var size = value == "10" ? 10 : 8;
            while (Periods.Count > size) Periods.RemoveAt(Periods.Count - 1);
            var defaults = StudentDocument.Create(original.Settings.FirstWeekMonday, original.Settings.ClassesUnit);
            defaults.Settings.ClassesPerDay = 10;
            var generated = ClassTimeGenerator.Generate(defaults.Settings);
            while (Periods.Count < size) AddPeriod(generated[Periods.Count]);
            UpdateGaps();
        }
    }
    public string ConversionPreview
    {
        get
        {
            if (sunday == (original.Settings.SundayBelongsTo == "next" ? "下一周" : "上一周")) return "切换周日归属将保留所有课程实际日期，自动调整周次。";
            try
            {
                var converted = WeekCalendar.ChangeSunday(original, sunday == "下一周" ? "next" : "previous");
                return $"保存后：第一周周一 {converted.Settings.FirstWeekMonday:yyyy/MM/dd}，共{converted.Settings.TotalWeeks}周；所有课程日期不变。";
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { return ex.Message; }
        }
    }
    public SettingsViewModel(StudentDocument document)
    {
        original = document.Clone();
        var s = document.Settings; var p = s.TimeGeneration;
        FirstMonday = s.FirstWeekMonday.ToDateTime(TimeOnly.MinValue); TotalWeeks = s.TotalWeeks.ToString();
        count = s.ClassesPerDay.ToString(); sunday = s.SundayBelongsTo == "next" ? "下一周" : "上一周";
        Mode = s.ClassesUnit == "one" ? "单节" : "双节";
        Duration = p.DurationMinutes.ToString(); OddBreak = p.OddBreakMinutes.ToString(); EvenBreak = p.EvenBreakMinutes.ToString();
        Morning = p.MorningStart; Afternoon = p.AfternoonStart; Evening = p.EveningStart;
        foreach (var period in s.ClassPeriods) AddPeriod(period);
        UpdateGaps();
    }
    private void AddPeriod(ClassPeriod p) { var row = new PeriodRow(p) { Edited = UpdateGaps }; Periods.Add(row); }
    private void UpdateGaps()
    {
        for (var i = 0; i < Periods.Count; i++)
        {
            try { Periods[i].Gap = i is 3 or 7 || i == Periods.Count - 1 ? "—" : (ClassTimeGenerator.Minutes(Periods[i + 1].Start) - ClassTimeGenerator.Minutes(Periods[i].End)).ToString(); }
            catch (InvalidDataException) { Periods[i].Gap = "—"; }
        }
    }
    private TimeGenerationSettings Parameters() => new()
    {
        DurationMinutes = Number(Duration, "课程时长"), OddBreakMinutes = GapNumber(OddBreak, "奇数节后间隔"), EvenBreakMinutes = GapNumber(EvenBreak, "偶数节后间隔"),
        MorningStart = Morning, AfternoonStart = Afternoon, EveningStart = Evening
    };
    private static int GapNumber(string text, string label)
    {
        if (!NumericText.IsValid(text, 2)) throw new InvalidDataException($"{label}只接受最多两位数字，范围0–99。");
        return Number(text, label);
    }
    private static int Number(string text, string label)
    {
        if (text.Length == 0 || text.Any(c => c is < '0' or > '9') || !int.TryParse(text, out var number)) throw new InvalidDataException($"{label}请输入有效整数。");
        return number;
    }
    public void Generate(bool defaults)
    {
        if (defaults)
        {
            Duration = "45"; OddBreak = "10"; EvenBreak = "20"; Morning = "08:00"; Afternoon = "14:00"; Evening = "19:00";
            Changed(nameof(Duration)); Changed(nameof(OddBreak)); Changed(nameof(EvenBreak)); Changed(nameof(Morning)); Changed(nameof(Afternoon)); Changed(nameof(Evening));
        }
        var settings = new StudentSettings { ClassesPerDay = int.Parse(Count), TimeGeneration = Parameters() };
        var generated = ClassTimeGenerator.Generate(settings);
        Periods.Clear(); foreach (var p in generated) AddPeriod(p); UpdateGaps();
    }
    public StudentDocument Build()
    {
        if (FirstMonday is null) throw new InvalidDataException("请选择本学期第一周周一。");
        if (Mode is not ("单节" or "双节")) throw new InvalidDataException("请选择单节或双节。");
        var monday = DateOnly.FromDateTime(FirstMonday.Value); var weeks = Number(TotalWeeks, "总周数");
        var target = Sunday == "下一周" ? "next" : "previous";
        var converting = target != original.Settings.SundayBelongsTo;
        if (converting && (monday != original.Settings.FirstWeekMonday || weeks != original.Settings.TotalWeeks))
            throw new InvalidDataException("请先单独保存周日归属转换，再修改学期起点或总周数。");
        var candidate = converting ? WeekCalendar.ChangeSunday(original, target) : original.Clone();
        var s = candidate.Settings;
        if (!converting) { s.FirstWeekMonday = monday; s.TotalWeeks = weeks; }
        s.SundayBelongsTo = target; s.ClassesPerDay = int.Parse(Count); s.ClassesUnit = Mode == "单节" ? "one" : "two";
        if (!CanChangeMode && s.ClassesUnit != original.Settings.ClassesUnit) throw new InvalidDataException("已有课程时不能更改课程模式。");
        s.TimeGeneration = Parameters(); s.ClassPeriods = Periods.Select(p => p.ToModel()).ToList();
        TimetableValidator.Validate(candidate); return candidate;
    }
    public bool ChangesDates => FirstMonday is not null && DateOnly.FromDateTime(FirstMonday.Value) != original.Settings.FirstWeekMonday;
}
