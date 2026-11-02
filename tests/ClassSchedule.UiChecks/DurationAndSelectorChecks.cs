using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ClassSchedule;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class DurationAndSelectorChecks
{
    public static int Run(Application app)
    {
        var path = Path.Combine(Path.GetTempPath(), "duration-selector-" + Guid.NewGuid()); var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected invalid duration"); }
        try
        {
            var document = StudentDocument.Create(new DateOnly(2026, 11, 2));
            document.Courses.Add(new Course { Name = "数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1 }] });
            Check(document.Courses[0].ReminderDurationMinutes == 1, "Reminder duration defaults to one minute");
            var json = JsonNode.Parse(DocumentJson.Write(document))!; json["courses"]![0]!.AsObject().Remove("reminderDurationMinutes");
            var loaded = DocumentJson.Read(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())); TimetableValidator.Validate(loaded);
            Check(loaded.Courses[0].ReminderDurationMinutes == 1, "Existing v2 without duration reads as one minute");
            var courseVm = new CourseViewModel(document, document.Courses[0].Id) { ReminderDurationMinutes = "2" };
            var candidate = courseVm.Build(); Check(candidate.Courses[0].ReminderDurationMinutes == 2 && document.Courses[0].ReminderDurationMinutes == 1, "Duration saves into candidate without changing draft source");
            courseVm.ReminderDurationMinutes = "0"; Reject(() => courseVm.Build());
            courseVm.ReminderDurationMinutes = "4"; Reject(() => courseVm.Build());
            courseVm.ReminderDurationMinutes = ""; Reject(() => courseVm.Build()); Check(true, "Zero, excessive and empty duration are rejected");
            courseVm.ReminderDurationMinutes = "3"; Check(courseVm.Build().Courses[0].ReminderDurationMinutes == 3, "Duration equal to interval is accepted");
            var session = new ReminderSession(); var begins = new DateTime(2026, 11, 2, 7, 40, 0); session.Synchronize("检查学生", candidate, begins); session.Tick(begins);
            var revised = candidate.Clone(); revised.Courses[0].ReminderDurationMinutes = 1; session.Synchronize("检查学生", revised, begins.AddMinutes(1));
            Check(session.Tick(begins.AddMinutes(1)).Count == 0 && session.Tick(begins.AddMinutes(3)).Single() is { Number: 2, DurationMinutes: 1 }, "Editing only duration preserves reminder progress");

            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); var main = new MainWindow(workspace); main.Show(); Flush();
            var selector = Program.Descendants(main).OfType<ComboBox>().Single();
            var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
            selector.RaiseEvent(click); Flush();
            Check(click.Handled && !selector.IsDropDownOpen && main.Messages.Current == "当前列表中没有成员。", "Empty selector click shows only bottom information");
            selector.IsDropDownOpen = true; Flush(); Check(!selector.IsDropDownOpen, "Empty selector never retains an empty popup");
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(main), Environment.TickCount, Key.Down) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            selector.RaiseEvent(key); Flush(); Check(key.Handled && !selector.IsDropDownOpen, "Empty selector keyboard opening is also suppressed");
            workspace.Create("姓名检查", candidate); main.RefreshStudents(); Flush(); selector.IsDropDownOpen = true; Flush();
            var popup = (Popup)selector.Template.FindName("PART_Popup", selector);
            Check(selector.IsDropDownOpen && popup.IsOpen && selector.ActualWidth == 88, "Nonempty selector still opens at 88 DIP");
            var display = Program.Descendants(selector).OfType<TextBlock>().First(t => t.Text == "姓名检查");
            var item = (ComboBoxItem)selector.ItemContainerGenerator.ContainerFromIndex(0); item.ApplyTemplate(); Flush();
            var optionText = Program.Descendants(item).OfType<TextBlock>().Single(t => t.Text == "姓名检查");
            var displayInset = display.TranslatePoint(new Point(), selector).X;
            var optionInset = optionText.TranslatePoint(new Point(), popup.Child).X;
            Check(displayInset > 10 && Math.Abs(displayInset - optionInset) < 1, $"Display and dropdown horizontal insets match ({displayInset:F1}/{optionInset:F1})");
            selector.IsDropDownOpen = false;
            var editor = new CourseManagementWindow(workspace, WorkspacePage.Course, document.Courses[0].Id) { Owner = main }; editor.Show(); Flush();
            var duration = Program.Descendants(editor).OfType<TextBox>().Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == "ReminderDurationMinutes");
            Check(duration.Text == "2", "Course editor shows persisted reminder duration");
            var durationRow = (StackPanel)duration.Parent;
            var unit = durationRow.Children.OfType<TextBlock>().Last();
            Check(durationRow.Children.OfType<TextBlock>().Select(t => t.Text).SequenceEqual(new[] { "次，间隔", "分钟，时长", "分钟" }) && unit.TranslatePoint(new Point(unit.ActualWidth, 0), durationRow).X <= durationRow.ActualWidth, "Duration and units fit the same reminder row");
            var typing = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, duration, "a")) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
            duration.RaiseEvent(typing); Check(typing.Handled, "Duration rejects nonnumeric typing");
            duration.Text = "4"; Program.Descendants(editor).OfType<Button>().Single(b => b.Content as string == "保存").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(editor.IsVisible && editor.Messages.Current.Length > 0 && duration.Text == "4", "Invalid duration remains editable with bottom error");
            duration.Text = "1"; Program.Descendants(editor).OfType<Button>().Single(b => b.Content as string == "保存").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Check(workspace.Document!.Courses[0].ReminderDurationMinutes == 1 && editor.State.Page == WorkspacePage.Courses, "Valid duration persists through course Save");
            editor.Close(); workspace.Save(candidate);

            var now = new DateTime(2026, 11, 2, 7, 40, 0);
            using var reminders = new ReminderService(workspace, main, _ => { }, () => now, false);
            reminders.Advance(now.AddMinutes(1).AddSeconds(59)); Check(reminders.Alert is not null, "Two-minute alert remains past one minute");
            reminders.Advance(now.AddMinutes(2)); Check(reminders.Alert is null, "Alert expires at configured two minutes");
            reminders.Advance(now.AddMinutes(3)); Check(reminders.Alert is not null, "Repeat interval remains three minutes");
            reminders.Alert!.Close(); reminders.Advance(now.AddMinutes(6)); Check(reminders.Alert is not null, "Manual close still preserves later repetitions");
            Console.WriteLine($"DURATION/SELECTOR CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
