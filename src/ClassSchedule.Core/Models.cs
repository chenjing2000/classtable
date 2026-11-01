using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassSchedule.Core;

public sealed class StudentDocument
{
    [JsonRequired] public int SchemaVersion { get; set; } = 2;
    [JsonRequired] public StudentSettings Settings { get; set; } = new();
    [JsonRequired] public List<Course> Courses { get; set; } = [];
    public StudentDocument Clone() => DocumentJson.Read(DocumentJson.Write(this));
    public static StudentDocument Create(DateOnly monday, string mode = "one")
    {
        var doc = new StudentDocument();
        doc.Settings.FirstWeekMonday = monday;
        doc.Settings.ClassesUnit = mode;
        doc.Settings.ClassPeriods = ClassTimeGenerator.Generate(doc.Settings);
        return doc;
    }
}

public sealed class StudentSettings
{
    [JsonRequired] public DateOnly FirstWeekMonday { get; set; }
    [JsonRequired] public int TotalWeeks { get; set; } = 20;
    [JsonRequired] public string SundayBelongsTo { get; set; } = "previous";
    [JsonRequired] public int ClassesPerDay { get; set; } = 8;
    [JsonRequired] public string ClassesUnit { get; set; } = "one";
    [JsonRequired] public TimeGenerationSettings TimeGeneration { get; set; } = new();
    [JsonRequired] public List<ClassPeriod> ClassPeriods { get; set; } = [];
}

public sealed class TimeGenerationSettings
{
    [JsonRequired] public int DurationMinutes { get; set; } = 45;
    [JsonRequired] public int OddBreakMinutes { get; set; } = 10;
    [JsonRequired] public int EvenBreakMinutes { get; set; } = 20;
    [JsonRequired] public string MorningStart { get; set; } = "08:00";
    [JsonRequired] public string AfternoonStart { get; set; } = "14:00";
    [JsonRequired] public string EveningStart { get; set; } = "19:00";
}

public sealed class ClassPeriod
{
    [JsonRequired] public int Period { get; set; }
    [JsonRequired] public string Start { get; set; } = "08:00";
    [JsonRequired] public string End { get; set; } = "08:45";
}

public sealed class Course
{
    [JsonRequired] public Guid Id { get; set; } = Guid.NewGuid();
    [JsonRequired] public string Name { get; set; } = "";
    [JsonRequired] public string Color { get; set; } = "#A8CEFF";
    [JsonRequired] public int ReminderCount { get; set; } = 3;
    [JsonRequired] public int ReminderIntervalMinutes { get; set; } = 3;
    public int ReminderDurationMinutes { get; set; } = 1;
    [JsonRequired] public List<ScheduleEntry> Schedules { get; set; } = [];
}

public sealed class ScheduleEntry
{
    [JsonRequired] public Guid Id { get; set; } = Guid.NewGuid();
    [JsonRequired] public List<int> Weeks { get; set; } = [];
    [JsonRequired] public int DayOfWeek { get; set; } = 1;
    [JsonRequired] public int StartPeriod { get; set; } = 1;
    [JsonRequired] public string Location { get; set; } = "";
    [JsonRequired] public int ReminderMinutes { get; set; } = 30;
}

public static class DocumentJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public static byte[] Write(StudentDocument document) => JsonSerializer.SerializeToUtf8Bytes(document, Options);
    public static StudentDocument Read(byte[] bytes)
    {
        try { return JsonSerializer.Deserialize<StudentDocument>(bytes, Options) ?? throw new InvalidDataException("学生文件不能为空。"); }
        catch (JsonException ex) { throw new InvalidDataException($"JSON格式不合法：{ex.Message}", ex); }
    }
}
