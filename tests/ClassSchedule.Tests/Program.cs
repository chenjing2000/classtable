using ClassSchedule.Core;
using ClassSchedule.ViewModels;

if (args.Contains("--reminders")) return ReminderChecks.Run();

int passed = 0;
void Test(string name, Action action) { action(); passed++; Console.WriteLine($"PASS {name}"); }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected validation failure"); }
StudentDocument Sample(string mode = "two") {
    var d = StudentDocument.Create(new DateOnly(2026, 8, 31), mode);
    d.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1, 20], DayOfWeek = 7, StartPeriod = 1 }] });
    return d;
}
string Dates(StudentDocument d) => string.Join(',', d.Courses.SelectMany(c => c.Schedules).SelectMany(s => s.Weeks.Select(w => WeekCalendar.Date(d.Settings, w, s.DayOfWeek))).Order());

Test("Sunday dates and mathematical floor", () => {
    var s = Sample().Settings;
    Equal(new DateOnly(2026, 9, 6), WeekCalendar.Date(s, 1, 7));
    Equal(0, WeekCalendar.CurrentWeek(s, new DateOnly(2026, 8, 30)));
    s.SundayBelongsTo = "next";
    Equal(new DateOnly(2026, 8, 30), WeekCalendar.Date(s, 1, 7));
    Equal(1, WeekCalendar.CurrentWeek(s, new DateOnly(2026, 8, 30)));
    Equal(0, WeekCalendar.CurrentWeek(s, new DateOnly(2026, 8, 29)));
});
Test("Sunday conversion preserves dates and extends final week", () => {
    var d = Sample(); var dates = Dates(d); var copy = WeekCalendar.ChangeSunday(d, "next");
    Equal(dates, Dates(copy)); Equal(21, copy.Settings.TotalWeeks); Equal(1, d.Courses[0].Schedules[0].Weeks[0]);
    Equal(dates, Dates(WeekCalendar.ChangeSunday(copy, "previous")));
});
Test("First Sunday moves anchor and all course weeks", () => {
    var d = Sample(); d.Settings.SundayBelongsTo = "next";
    d.Courses[0].Schedules.Add(new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 3 });
    var copy = WeekCalendar.ChangeSunday(d, "previous");
    Equal(Dates(d), Dates(copy)); Equal(new DateOnly(2026, 8, 24), copy.Settings.FirstWeekMonday);
    Equal(2, copy.Courses[0].Schedules[1].Weeks[0]);
});
Test("Generation respects lunch and evening", () => {
    var d = Sample(); d.Settings.ClassesPerDay = 10;
    var p = ClassTimeGenerator.Generate(d.Settings);
    Equal(10, p.Count); Equal("14:00", p[4].Start); Equal("19:00", p[8].Start); Equal("09:40", p[1].End);
});
Test("Week parser normalizes unions and rejects malformed ranges", () => {
    Equal("1,2,3,5,7", string.Join(',', WeekSelectionParser.Parse("1-3,5,7,2", 20)));
    foreach (var v in new[] { "", "4-2", "0", "21", "1,,2", "-1", "1.5", "1-9999999" }) Reject(() => WeekSelectionParser.Parse(v, 20));
});
Test("Validation allows empty location but rejects double even start and collisions", () => {
    var d = Sample(); TimetableValidator.Validate(d);
    d.Courses[0].Schedules[0].StartPeriod = 2; Reject(() => TimetableValidator.Validate(d));
    d.Courses[0].Schedules[0].StartPeriod = 1;
    d.Courses[0].Schedules.Add(new ScheduleEntry { Weeks = [20], DayOfWeek = 7, StartPeriod = 1 });
    Reject(() => TimetableValidator.Validate(d));
    d.Courses[0].Schedules[1].Weeks = [2]; TimetableValidator.Validate(d);
});
Test("Validation rejects invalid dates times and week sets", () => {
    var d = Sample(); d.Settings.FirstWeekMonday = new DateOnly(2026, 9, 1); Reject(() => TimetableValidator.Validate(d));
    d = Sample(); d.Settings.ClassPeriods[1].Start = "08:40"; Reject(() => TimetableValidator.Validate(d));
    d = Sample(); d.Courses[0].Schedules[0].Weeks = [1, 1]; Reject(() => TimetableValidator.Validate(d));
    d = Sample(); d.Settings.ClassesPerDay = 8; d.Courses[0].Schedules[0].StartPeriod = 9; Reject(() => TimetableValidator.Validate(d));
});
Test("Color validation rejects trailing whitespace before loading into views", () => {
    foreach (var color in new[] { "#112233\n", "#112233\r\n", "#112233 ", " #112233", "#11223300" }) {
        var d = Sample(); d.Courses[0].Color = color; Reject(() => TimetableValidator.Validate(d));
    }
    foreach (var color in new[] { "#112233", "#aAbBcC" }) {
        var d = Sample(); d.Courses[0].Color = color; TimetableValidator.Validate(d);
        _ = new CourseViewModel(d, null);
    }
});
Test("Clone owns settings courses schedules and weeks", () => {
    var d = Sample(); var c = d.Clone(); c.Courses[0].Schedules[0].Weeks[0] = 4; c.Settings.ClassPeriods[0].Start = "07:00";
    Equal(1, d.Courses[0].Schedules[0].Weeks[0]); Equal("08:00", d.Settings.ClassPeriods[0].Start);
});
Test("Storage roundtrip backup external changes and student isolation", () => {
    var path = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(path); var a = store.Save("张三", Sample(), null);
        var b = store.Save("李四", Sample("one"), null);
        Equal("two", store.Load("张三").Document.Settings.ClassesUnit);
        Reject(() => store.Save("张三", Sample(), null));
        var edit = a.Document.Clone(); edit.Courses[0].Name = "修改后";
        a = store.Save("张三", edit, a.Fingerprint);
        Equal(true, File.Exists(Path.Combine(path, "张三.bak")));
        Equal("数学", store.Load("李四").Document.Courses[0].Name);
        File.AppendAllText(Path.Combine(path, "张三.json"), " ");
        Reject(() => store.Save("张三", edit, a.Fingerprint));
        edit.Settings.ClassesUnit = "one";
        Reject(() => store.Save("张三", edit, store.Load("张三").Fingerprint));
        File.WriteAllText(Path.Combine(path, "坏文件.json"), "{invalid");
        Equal(2, store.Scan().Students.Count); Equal(1, store.Scan().Errors.Count);
        Equal("{invalid", File.ReadAllText(Path.Combine(path, "坏文件.json")));
        foreach (var name in new[] { "../outside", "CON", "con.txt", "张三.", "张三 ", "", "a/b" }) Reject(() => store.Save(name, Sample(), null));
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Strict JSON rejects missing renamed null and unknown fields", () => {
    foreach (var value in new[] { "{}", "{\"schemaVersion\":2}", "{\"studentName\":\"x\"}", "null" })
        Reject(() => { var d = DocumentJson.Read(System.Text.Encoding.UTF8.GetBytes(value)); TimetableValidator.Validate(d); });
    var json = System.Text.Encoding.UTF8.GetString(DocumentJson.Write(Sample())).Replace("\"classesUnit\"", "\"courseUnit\"");
    Reject(() => DocumentJson.Read(System.Text.Encoding.UTF8.GetBytes(json)));
});
Test("Notifications grant every visible message five seconds and collapse", () => {
    var now = DateTimeOffset.UtcNow; using var q = new NotificationQueue(() => now);
    q.Enqueue("A"); now = now.AddSeconds(4); q.Enqueue("B"); q.Enqueue("B"); q.Advance(); Equal("A", q.Current);
    now = now.AddSeconds(1); q.Advance(); Equal("B", q.Current);
    now = now.AddSeconds(4); q.Advance(); Equal("B", q.Current);
    now = now.AddSeconds(1); q.Advance(); Equal("", q.Current);
    q.Dispose(); q.Enqueue("C"); Equal("", q.Current);
});
Test("Full calendar range validated for next mode near year 10000", () => {
    var d = StudentDocument.Create(new DateOnly(9999, 12, 27), "one"); d.Settings.TotalWeeks = 1; d.Settings.SundayBelongsTo = "next";
    Reject(() => TimetableValidator.Validate(d));
});
Test("Settings conversion preview is repeatable and cancellation isolated", () => {
    var d = Sample(); var vm = new SettingsViewModel(d); vm.Sunday = "下一周";
    Equal(Dates(d), Dates(vm.Build())); Equal(21, vm.Build().Settings.TotalWeeks);
    vm.Sunday = "上一周"; Equal(20, vm.Build().Settings.TotalWeeks); Equal(20, d.Settings.TotalWeeks);
    vm.Mode = "单节"; Reject(() => vm.Build());
});
Test("Course and child edits remain drafts until committed", () => {
    var d = Sample(); var vm = new CourseViewModel(d, d.Courses[0].Id);
    vm.Course.Name = "草稿"; var child = new ScheduleViewModel(d.Settings, vm.Course.Schedules[0]); child.Location = "草稿地点";
    Equal("", vm.Course.Schedules[0].Location); Equal("数学", d.Courses[0].Name);
    Equal("草稿地点", child.Build().Location); Equal("", d.Courses[0].Schedules[0].Location);
    Equal("草稿", vm.Build().Courses[0].Name);
});
Test("Time generation preserves user edits until explicit action", () => {
    var d = Sample(); var vm = new SettingsViewModel(d); vm.Periods[0].End = "08:40"; vm.Duration = "40";
    Equal("08:40", vm.Build().Settings.ClassPeriods[0].End); Equal("08:55", vm.Build().Settings.ClassPeriods[1].Start);
    vm.Generate(false); Equal("08:50", vm.Periods[1].Start); vm.Generate(true); Equal("08:55", vm.Periods[1].Start);
    Equal("08:45", d.Settings.ClassPeriods[0].End);
});
Test("Workspace publishes state only after successful save", () => {
    var path = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(path); var vm = new WorkspaceViewModel(store); vm.Create("测试", Sample());
        var original = vm.Current; var candidate = vm.Document!.Clone(); candidate.Courses[0].Name = "待保存";
        File.AppendAllText(Path.Combine(path, "测试.json"), " "); Reject(() => vm.Save(candidate));
        Equal(original, vm.Current); Equal("数学", vm.Document.Courses[0].Name);
        vm.Refresh(); vm.Save(candidate); Equal("待保存", vm.Document.Courses[0].Name);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Sunday conversions preserve mixed weekday dates over year boundary", () => {
    foreach (var mode in new[] { "one", "two" }) foreach (var sunday in new[] { "previous", "next" }) {
        var d = StudentDocument.Create(new DateOnly(2026, 12, 28), mode); d.Settings.SundayBelongsTo = sunday;
        for (var day = 1; day <= 7; day++) d.Courses.Add(new Course { Name = "课程" + day, Schedules = [new ScheduleEntry { DayOfWeek = day, Weeks = [1, 2, 20], StartPeriod = 1 }] });
        var c = WeekCalendar.ChangeSunday(d, sunday == "next" ? "previous" : "next");
        Equal(Dates(d), Dates(c)); Equal(Dates(d), Dates(WeekCalendar.ChangeSunday(c, sunday)));
    }
});
Test("Locked target failure retains original bytes and leaves no temp files", () => {
    var path = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(path); var saved = store.Save("学生", Sample(), null);
        var file = Path.Combine(path, "学生.json"); var bytes = File.ReadAllBytes(file); var copy = saved.Document.Clone(); copy.Courses[0].Name = "新值";
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            bool failed = false; try { store.Save("学生", copy, saved.Fingerprint); } catch (IOException) { failed = true; }
            Equal(true, failed);
        }
        Equal(Convert.ToHexString(bytes), Convert.ToHexString(File.ReadAllBytes(file))); Equal(0, Directory.GetFiles(path, "*.tmp").Length);
        File.Delete(file); Reject(() => store.Save("学生", copy, saved.Fingerprint)); Equal(false, File.Exists(file));
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Break parameters accept 0 through 99 and reject outside range", () => {
    foreach (var gap in new[] { 0, 5, 15, 99 }) {
        var d = Sample(); d.Settings.TimeGeneration.OddBreakMinutes = gap; d.Settings.TimeGeneration.EvenBreakMinutes = gap;
        d.Settings.TimeGeneration.DurationMinutes = 1;
        TimetableValidator.Validate(d); var generated = ClassTimeGenerator.Generate(d.Settings);
        Equal(8 * 60 + 1 + gap, ClassTimeGenerator.Minutes(generated[1].Start));
    }
    foreach (var gap in new[] { -1, 100 }) {
        var d = Sample(); d.Settings.TimeGeneration.OddBreakMinutes = gap;
        Reject(() => TimetableValidator.Validate(d)); Reject(() => ClassTimeGenerator.Generate(d.Settings));
    }
});
Test("Preview suppresses gaps at session boundaries in both day lengths", () => {
    var d = Sample(); var vm = new SettingsViewModel(d);
    Equal("—", vm.Periods[3].Gap); Equal("—", vm.Periods[7].Gap); Equal("10", vm.Periods[0].Gap);
    vm.Count = "10"; Equal("—", vm.Periods[7].Gap); Equal("—", vm.Periods[9].Gap);
    Equal("10", vm.Periods[8].Gap);
});
Test("Numeric text admits only bounded ASCII digit strings", () => {
    foreach (var text in new[] { "", "0", "09", "99" }) Equal(true, NumericText.IsValid(text, 2));
    foreach (var text in new[] { "100", " 1", "１", "一", "1.0", "-1", "2\n", "1e2" }) Equal(false, NumericText.IsValid(text, 2));
    var vm = new SettingsViewModel(Sample()) { OddBreak = "", EvenBreak = "0" }; Reject(() => vm.Build());
    vm.OddBreak = "99"; Equal(99, vm.Build().Settings.TimeGeneration.OddBreakMinutes);
    vm.OddBreak = "000"; Reject(() => vm.Build());
    vm.OddBreak = "99"; vm.EvenBreak = "99"; var start = vm.Periods[1].Start;
    Reject(() => vm.Generate(false)); Equal(start, vm.Periods[1].Start);
});
Test("Empty students can change mode; existing courses lock old mode", () => {
    var empty = StudentDocument.Create(new DateOnly(2026, 8, 31), "one");
    var vm = new SettingsViewModel(empty) { Mode = "双节" }; Equal("two", vm.Build().Settings.ClassesUnit); Equal("one", empty.Settings.ClassesUnit);
    var path = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(path); var saved = store.Save("学生", empty, null);
        saved = store.Save("学生", vm.Build(), saved.Fingerprint); Equal("two", saved.Document.Settings.ClassesUnit);
        saved = store.Save("学生", Sample(), saved.Fingerprint);
        var removed = saved.Document.Clone(); removed.Courses.Clear(); removed.Settings.ClassesUnit = "one";
        Reject(() => store.Save("学生", removed, saved.Fingerprint));
        removed.Settings.ClassesUnit = "two"; saved = store.Save("学生", removed, saved.Fingerprint);
        removed.Settings.ClassesUnit = "one"; saved = store.Save("学生", removed, saved.Fingerprint); Equal("one", saved.Document.Settings.ClassesUnit);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Time codes roundtrip and expand in numerical week day period order", () => {
    var s = Sample("one").Settings;
    Equal("30305", CourseTimeCode.Encode(3, 3, 5, s));
    Equal(new ClassOccurrence(3, 3, 5, 5), CourseTimeCode.Decode("30305", s));
    s.ClassesUnit = "two"; Equal("3030506", CourseTimeCode.Encode(3, 3, 5, s));
    Equal(new ClassOccurrence(3, 3, 5, 6), CourseTimeCode.Decode("3030506", s));
    Equal("20070910", CourseTimeCode.Encode(20, 7, 9, new StudentSettings { TotalWeeks = 20, ClassesPerDay = 10, ClassesUnit = "two" }));
    var c = new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [10, 3], DayOfWeek = 7, StartPeriod = 3 }, new ScheduleEntry { Weeks = [3], DayOfWeek = 3, StartPeriod = 5 }] };
    Equal("3030506, 3070304, 10070304", CourseTimeCode.Format(c, s));
    foreach (var mode in new[] { "one", "two" }) {
        s.ClassesUnit = mode;
        foreach (var day in Enumerable.Range(1, 7)) foreach (var week in new[] { 1, 3, 20 }) {
            var code = CourseTimeCode.Encode(week, day, 5, s); var item = CourseTimeCode.Decode(code, s);
            Equal(week, item.Week); Equal(day, item.DayOfWeek); Equal(5, item.StartPeriod);
        }
    }
});
Test("Decoder rejects bad padding bounds modes and double pairs", () => {
    var s = Sample().Settings;
    foreach (var code in new[] { "", "30305", "03030506", "3030507", "3030405", "3030910", "3070001", "3080506", "0030506", "21030506", "３０３０５０６", "3030506 ", "99999999999999999999030506" }) Reject(() => CourseTimeCode.Decode(code, s));
    s.ClassesUnit = "invalid"; Reject(() => CourseTimeCode.Decode("3030506", s));
});
Test("New students get single mode complete defaults and Monday anchor", () => {
    var path = Path.Combine(Path.GetTempPath(), "student-defaults-" + Guid.NewGuid());
    try {
        var vm = new WorkspaceViewModel(new StudentFileStore(path)); vm.CreateStudent("蜡笔小新", new DateOnly(2026, 11, 1)); var d = vm.Document!;
        Equal("蜡笔小新", vm.Current!.Name); Equal("one", d.Settings.ClassesUnit); Equal(new DateOnly(2026, 10, 26), d.Settings.FirstWeekMonday);
        Equal(0, d.Courses.Count); Equal(8, d.Settings.ClassPeriods.Count); TimetableValidator.Validate(d);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Student selection reloads independent JSON settings and course modes", () => {
    var path = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
    try {
        var vm = new WorkspaceViewModel(new StudentFileStore(path));
        var a = StudentDocument.Create(new DateOnly(2026, 8, 31)); vm.Create("A", a);
        var b = StudentDocument.Create(new DateOnly(2026, 9, 7), "two"); b.Settings.TotalWeeks = 16; vm.Create("B", b);
        vm.Select("A"); Equal("one", vm.Document!.Settings.ClassesUnit); Equal(20, vm.Document.Settings.TotalWeeks);
        vm.Select("B"); Equal("two", vm.Document.Settings.ClassesUnit); Equal(16, vm.Document.Settings.TotalWeeks); Equal(new DateOnly(2026, 9, 7), vm.Document.Settings.FirstWeekMonday);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Weeks are ASCII-only and formatting compresses ranges", () => {
    Equal("1-5, 7-8, 10", WeekSelectionParser.Format([10, 7, 8, 1, 2, 3, 4, 5, 7]));
    foreach (var bad in new[] { "1，2", "1\t-2", "１-2", "1\n,2", "1—2" }) Reject(() => WeekSelectionParser.Parse(bad, 20));
    Equal(true, WeekSelectionParser.IsInputText("1-5, 7, 8")); Equal(false, WeekSelectionParser.IsInputText("1，2"));
});
Test("New course colors spread hues and preserve existing edits", () => {
    var cyan = CourseColors.Suggest(["#FF0000"], .5); var rgb = Convert.ToInt32(cyan[1..], 16);
    Equal(true, ((rgb >> 8) & 255) > ((rgb >> 16) & 255) && (rgb & 255) > ((rgb >> 16) & 255));
    var d = Sample(); var vm = new CourseViewModel(d, null); Equal(false, vm.Course.Color == d.Courses[0].Color);
    var existing = new CourseViewModel(d, d.Courses[0].Id); Equal(d.Courses[0].Color, existing.Course.Color);
    foreach (var x in new[] { 0.0, .25, .5, .99 }) Equal(true, System.Text.RegularExpressions.Regex.IsMatch(CourseColors.Suggest([], x), "^#[0-9A-F]{6}$"));
});
Test("Schedule presentation keeps groups and orders weeks then time", () => {
    var d = Sample(); var course = d.Courses[0];
    course.Schedules = [new ScheduleEntry { Weeks = [7, 9], DayOfWeek = 3, StartPeriod = 1, Location = "C" }, new ScheduleEntry { Weeks = [1, 2, 3], DayOfWeek = 5, StartPeriod = 3, Location = "B" }, new ScheduleEntry { Weeks = [1, 2], DayOfWeek = 3, StartPeriod = 1, Location = "A" }];
    var rows = SchedulePresentation.Rows(course, d.Settings);
    Equal("A,B,C", string.Join(',', rows.Select(r => r.Location))); Equal("1-2", rows[0].Weeks); Equal("30102", rows[0].Time);
    Equal("C", course.Schedules[0].Location);
});
Test("Time-only codes enforce the student mode and paired periods", () => {
    var s = Sample("one").Settings; Equal("401", CourseTimeCode.EncodeTime(4, 1, s)); Equal(new ClassTime(4, 1, 1), CourseTimeCode.DecodeTime("401", s));
    Reject(() => CourseTimeCode.DecodeTime("30102", s)); s.ClassesUnit = "two";
    Equal("30102", CourseTimeCode.EncodeTime(3, 1, s)); Equal(new ClassTime(3, 1, 2), CourseTimeCode.DecodeTime("30102", s));
    foreach (var text in new[] { "401", "3030506", "30103", "30203", "70910", "80102", "00102", "３０１０２", "30102 " }) Reject(() => CourseTimeCode.DecodeTime(text, s));
    s.ClassesPerDay = 10; Equal(new ClassTime(7, 9, 10), CourseTimeCode.DecodeTime("70910", s));
    var vm = new ScheduleViewModel(s, null) { Weeks = "1-5, 7, 8", Time = "30102", Location = "A101" };
    var slot = vm.Build(); Equal("1,2,3,4,5,7,8", string.Join(',', slot.Weeks)); Equal(3, slot.DayOfWeek); Equal(1, slot.StartPeriod);
});
Test("Workspace pages protect dirty drafts and rebuild from saved state", () => {
    var path = Path.Combine(Path.GetTempPath(), "ClassSchedule-pages-" + Guid.NewGuid());
    try {
        var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("测试", Sample());
        var pages = new StudentWorkspaceViewModel(workspace);
        pages.Edit(workspace.Document!.Courses[0].Id); Equal(false, pages.IsDirty);
        pages.Course!.Course.Name = "数学修改"; pages.ShowSettings();
        Equal(WorkspaceConfirmation.Leave, pages.Confirmation); Equal(WorkspacePage.Course, pages.Page);
        pages.CancelConfirmation(); Equal(WorkspacePage.Course, pages.Page);
        pages.ShowSettings(); pages.Save();
        Equal(WorkspacePage.Settings, pages.Page); Equal("数学修改", workspace.Document.Courses[0].Name);
        pages.Settings!.Duration = "40"; pages.ShowCourses(); pages.Discard();
        Equal(WorkspacePage.Courses, pages.Page); Equal(45, workspace.Document.Settings.TimeGeneration.DurationMinutes);
        pages.Edit(workspace.Document.Courses[0].Id); pages.Course!.Course.Name = ""; pages.ShowSettings();
        Reject(pages.Save); Equal(WorkspacePage.Course, pages.Page); Equal(WorkspaceConfirmation.None, pages.Confirmation);
        pages.Course.Course.Name = "数学再次修改"; pages.Save(); Equal(WorkspacePage.Courses, pages.Page);
        pages.ShowSettings(); pages.Settings!.FirstMonday = pages.Settings.FirstMonday!.Value.AddDays(7);
        pages.ShowCourses(); pages.Save(); Equal(WorkspaceConfirmation.Dates, pages.Confirmation);
        pages.CancelConfirmation(); pages.Settings.Duration = "41"; pages.Save();
        Equal(WorkspaceConfirmation.Dates, pages.Confirmation); pages.Confirm();
        Equal(WorkspacePage.Settings, pages.Page); Equal(41, workspace.Document.Settings.TimeGeneration.DurationMinutes);
        var closes = 0; pages.CloseRequested += () => closes++;
        pages.RequestClose(); Equal(1, closes);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Failed course saves preserve whitespace draft and pending navigation is cleared", () => {
    var path = Path.Combine(Path.GetTempPath(), "ClassSchedule-failed-page-" + Guid.NewGuid());
    try {
        var w = new WorkspaceViewModel(new StudentFileStore(path)); w.Create("测试", Sample());
        var pages = new StudentWorkspaceViewModel(w); pages.Edit(w.Document!.Courses[0].Id);
        pages.Course!.Course.Name = " 数学 "; File.AppendAllText(Path.Combine(path, "测试.json"), " ");
        pages.ShowSettings(); Reject(pages.Save);
        Equal(" 数学 ", pages.Course.Course.Name); Equal(true, pages.IsDirty); Equal(WorkspacePage.Course, pages.Page);
        Equal(WorkspaceConfirmation.None, pages.Confirmation);
        pages.ShowCourses(); Equal(WorkspaceConfirmation.Leave, pages.Confirmation); pages.Discard();
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Deleting all saved courses unlocks a fresh settings page", () => {
    var path = Path.Combine(Path.GetTempPath(), "ClassSchedule-empty-page-" + Guid.NewGuid());
    try {
        var w = new WorkspaceViewModel(new StudentFileStore(path)); w.Create("测试", Sample());
        var pages = new StudentWorkspaceViewModel(w); pages.ShowSettings(); Equal(false, pages.Settings!.CanChangeMode);
        pages.ShowCourses(); pages.RequestDelete(w.Document!.Courses[0].Id); pages.Confirm();
        var closeRequested = false;
        pages.CloseRequested += () => closeRequested = true;
        pages.ShowSettings(); Equal(true, pages.Settings!.CanChangeMode); pages.Settings.Mode = "单节"; pages.Save();
        Equal("one", w.Document.Settings.ClassesUnit); Equal(false, pages.IsDirty);
        Equal(true, closeRequested);
        pages = new StudentWorkspaceViewModel(w); pages.ShowSettings();
        pages.Settings!.TotalWeeks = "21"; pages.ResetSettings(); pages.CancelConfirmation(); Equal("21", pages.Settings.TotalWeeks);
        pages.ResetSettings(); pages.Discard(); Equal("20", pages.Settings!.TotalWeeks);
    } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
});
Test("Student rename preserves bytes and backup and rejects collisions or stale data", () => {
    var dir = Path.Combine(Path.GetTempPath(), "student-manage-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(dir); var original = store.Save("甲", Sample(), null);
        original = store.Save("甲", original.Document, original.Fingerprint);
        var bytes = File.ReadAllBytes(Path.Combine(dir, "甲.json")); var bak = File.ReadAllBytes(Path.Combine(dir, "甲.bak"));
        var caseStudent = store.Save("Alice", Sample(), null); caseStudent = store.Save("Alice", caseStudent.Document, caseStudent.Fingerprint);
        caseStudent = store.Rename(caseStudent, "ALICE"); Equal("ALICE", caseStudent.Name); Equal(true, File.Exists(Path.Combine(dir, "ALICE.bak")));
        var renamed = store.Rename(original, "乙");
        Equal("乙", renamed.Name); Equal(true, bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "乙.json"))));
        Equal(true, bak.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "乙.bak")))); Equal(false, File.Exists(Path.Combine(dir, "甲.json")));
        store.Save("丙", Sample(), null); Reject(() => store.Rename(renamed, "丙"));
        File.WriteAllText(Path.Combine(dir, "丁.bak"), "backup"); Reject(() => store.Rename(renamed, "丁"));
        File.AppendAllText(Path.Combine(dir, "乙.json"), " "); Reject(() => store.Delete(renamed)); Reject(() => store.Rename(renamed, "戊"));
        Equal(true, File.Exists(Path.Combine(dir, "乙.bak")));
    } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
});
Test("Student delete removes JSON and backup and preserves other students", () => {
    var dir = Path.Combine(Path.GetTempPath(), "student-delete-" + Guid.NewGuid());
    try {
        var store = new StudentFileStore(dir); var first = store.Save("甲", Sample(), null);
        first = store.Save("甲", first.Document, first.Fingerprint); store.Save("乙", Sample(), null);
        using (var locked = new FileStream(Path.Combine(dir, "甲.bak"), FileMode.Open, FileAccess.Read, FileShare.None)) {
            try { store.Delete(first); throw new Exception("Expected locked backup failure"); } catch (IOException) { }
            Equal(true, File.Exists(Path.Combine(dir, "甲.json")));
        }
        File.SetAttributes(Path.Combine(dir, "甲.json"), FileAttributes.ReadOnly);
        try { store.Delete(first); throw new Exception("Expected readonly failure"); } catch (UnauthorizedAccessException) { }
        Equal(true, File.Exists(Path.Combine(dir, "甲.bak"))); File.SetAttributes(Path.Combine(dir, "甲.json"), FileAttributes.Normal);
        store.Delete(first); Equal(false, File.Exists(Path.Combine(dir, "甲.json"))); Equal(false, File.Exists(Path.Combine(dir, "甲.bak")));
        Equal("乙", store.Scan().Students.Single().Name);
    } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
});

Console.WriteLine($"TOTAL {passed} passed");
return 0;
