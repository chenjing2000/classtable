using System.Text;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;

internal static class ReminderChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
        var start = new DateTime(2026, 11, 2, 8, 0, 0);
        StudentDocument Document()
        {
            var d = StudentDocument.Create(DateOnly.FromDateTime(start));
            d.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1, 2], DayOfWeek = 1, StartPeriod = 1 }] }); return d;
        }
        try
        {
            var d = Document(); TimetableValidator.Validate(d);
            Check(d.SchemaVersion == 2 && d.Courses[0].ReminderCount == 3 && d.Courses[0].ReminderIntervalMinutes == 3 && d.Courses[0].Schedules[0].ReminderMinutes == 30, "V2 reminder defaults");
            var json = Encoding.UTF8.GetString(DocumentJson.Write(d));
            Reject(() => DocumentJson.Read(Encoding.UTF8.GetBytes(json.Replace("\"name\": \"数学\"", "\"note\": \"old\", \"name\": \"数学\""))));
            var old = d.Clone(); old.SchemaVersion = 1; Reject(() => TimetableValidator.Validate(old));
            Check(!json.Contains("\"note\""), "Destructive format removes note and rejects old fields/version");
            var session = new ReminderSession(); session.Synchronize("甲", d, start.AddMinutes(-40));
            Check(session.Tick(start.AddMinutes(-31)).Count == 0 && session.Tick(start.AddMinutes(-30)).Single().Number == 1, "First alert follows lead time");
            Check(session.Tick(start.AddMinutes(-29)).Count == 0 && session.Tick(start.AddMinutes(-27)).Single().Number == 2 && session.Tick(start.AddMinutes(-24)).Single().Number == 3 && session.Tick(start.AddMinutes(-21)).Count == 0, "Three starts separated by interval, no extra repeat");
            session = new(); session.Synchronize("甲", d, start.AddMinutes(-20));
            Check(session.Tick(start.AddMinutes(-20)).Count == 1 && session.Tick(start.AddMinutes(-17)).Count == 1 && session.Tick(start.AddMinutes(-14)).Count == 1, "Late startup before class begins restarts full series immediately");
            session = new(); session.Synchronize("甲", d, start);
            Check(session.Tick(start).Count == 0 && session.Tick(start.AddDays(7).AddMinutes(-30)).Count == 1, "Past/current class skipped while future weekly occurrence remains");
            session = new(); d.Courses[0].Schedules[0].ReminderMinutes = 0; session.Synchronize("甲", d, start.AddMinutes(-1));
            Check(session.Tick(start).Count == 1 && session.Tick(start.AddMinutes(3)).Count == 1, "Zero lead alerts at start, active series continues after start");
            session = new(); d = Document(); session.Synchronize("甲", d, start.AddMinutes(-20)); session.Tick(start.AddMinutes(-20));
            session.Synchronize("甲", d.Clone(), start.AddMinutes(-19));
            Check(session.Tick(start.AddMinutes(-19)).Count == 0 && session.Tick(start.AddMinutes(-17)).Single().Number == 2, "Same-student save preserves existing reminder progress");
            session.Synchronize("乙", null, start.AddMinutes(-16)); Check(session.Tick(start.AddMinutes(-14)).Count == 0, "Student switch clears former student's pending alerts");
            session.Synchronize("甲", d, start.AddMinutes(-13)); Check(session.Tick(start.AddMinutes(-13)).Single().Number == 1, "Switching to future class starts a fresh full series");
            var vm = new ScheduleViewModel(d.Settings, null); Check(vm.ReminderMinutes == "30" && vm.Build().ReminderMinutes == 30, "Slot editor carries reminder minutes into draft");
            vm.ReminderMinutes = "-1"; Reject(() => vm.Build());
            var invalid = d.Clone(); invalid.Courses[0].ReminderIntervalMinutes = 0; Reject(() => TimetableValidator.Validate(invalid));
            Check(true, "Invalid reminder settings rejected");
            var changed = Document(); changed.Courses[0].ReminderCount = 4; changed.Courses[0].ReminderIntervalMinutes = 7; changed.Courses[0].Schedules[0].ReminderMinutes = 45;
            var converted = WeekCalendar.ChangeSunday(changed.Clone(), "next");
            Check(converted.Courses[0].ReminderCount == 4 && converted.Courses[0].ReminderIntervalMinutes == 7 && converted.Courses[0].Schedules[0].ReminderMinutes == 45, "Nondefault reminder fields survive clone and Sunday conversion");
            var midnight = Document(); midnight.Settings.SundayBelongsTo = "next";
            midnight.Settings.ClassPeriods[0].Start = "00:10"; midnight.Settings.ClassPeriods[0].End = "00:55";
            midnight.Courses[0].Schedules[0].DayOfWeek = 7; midnight.Courses[0].Schedules[0].Weeks = [1]; TimetableValidator.Validate(midnight);
            session = new(); var saturday = new DateTime(2026, 10, 31, 23, 30, 0); session.Synchronize("甲", midnight, saturday);
            Check(session.Tick(saturday.AddMinutes(10)).Single().ClassStart == new DateTime(2026, 11, 1, 0, 10, 0), "Lead time crosses midnight with next-week Sunday mapping");
            var concurrent = Document(); concurrent.Courses.Add(new Course { Name = "英语", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 2, ReminderMinutes = 85 }] }); TimetableValidator.Validate(concurrent);
            session = new(); session.Synchronize("甲", concurrent, start.AddMinutes(-40));
            Check(session.Tick(start.AddMinutes(-30)).Count == 2, "Simultaneous reminders for distinct class starts all fire");
            session = new(); d = Document(); session.Synchronize("甲", d, start.AddMinutes(-31));
            var extraNotice = session.Tick(start.AddMinutes(-30)).Single();
            Check(session.AddReminder(extraNotice), "Extra reminder is attached to a pending occurrence");
            session.Synchronize("甲", d.Clone(), start.AddMinutes(-29));
            Check(session.Tick(start.AddMinutes(-27)).Single().Total == 4 && d.Courses[0].ReminderCount == 3, "Same-plan save preserves runtime extra count without changing document");
            session.Synchronize("乙", null, start.AddMinutes(-26));
            Check(!session.AddReminder(extraNotice), "Stale reminder cannot extend another student session");
            Console.WriteLine($"REMINDER CHECKS PASSED: {count}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
