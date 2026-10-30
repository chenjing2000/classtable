using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Xml.Linq;
using ClassTimetable.Core;
using ClassTimetable.ViewModels;
using ClassTimetable.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Load the real theme without running production startup or opening user data.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using (var theme = typeof(Program).Assembly.GetManifestResourceStream("AppTheme")!)
        {
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var resources = XDocument.Load(theme).Root!.Element(ns + "Application.Resources")!;
            var dictionary = new XElement(ns + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"), resources.Elements());
            app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
        }
        var path = Path.Combine(AppContext.BaseDirectory, "ui-check-" + Guid.NewGuid());
        try
        {
            var checks = 0;
            void Assert(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
            void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var today = DateOnly.FromDateTime(DateTime.Today); var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var document = StudentDocument.Create(monday, "two");
            document.Courses.Add(new Course { Name = "高等数学", Schedules = [new ScheduleEntry { Weeks = [1], DayOfWeek = 1, StartPeriod = 1, Location = "A101" }] });
            document.Courses[0].Schedules.Add(new ScheduleEntry { Weeks = [3, 5], DayOfWeek = 3, StartPeriod = 3, Location = "A102" });
            document.Courses[0].Schedules.Add(new ScheduleEntry { Weeks = [1, 2], DayOfWeek = 5, StartPeriod = 5, Location = "体育馆" });
            document.Courses.Add(new Course { Name = "英语", Color = "#FFDDB0", Schedules = [new ScheduleEntry { Weeks = [2], DayOfWeek = 2, StartPeriod = 7, Location = "B201" }] });
            var workspace = new WorkspaceViewModel(new StudentFileStore(path)); workspace.Create("界面检查", document);
            var main = new MainWindow(workspace); main.Show(); Flush();
            Assert(main.Width == 600 && main.Height == 600 && main.ActualWidth == 600 && main.MinWidth <= 600, "Main is 600 x 600 DIP");
            Assert(main.WindowStartupLocation == WindowStartupLocation.CenterScreen, "Main starts centered on screen");
            Assert(!Descendants(main).OfType<Button>().Any(b => b.ToolTip as string is "上一周" or "下一周") && Descendants(main).OfType<Button>().Any(b => b.Content as string == "回到本周"), "Main removes week arrow buttons and keeps today");
            var firstWeek = workspace.Week;
            var timeHeading = Descendants(main).OfType<TextBlock>().Single(t => t.Text == "时间 / 节次");
            Wheel(timeHeading, -120); Flush();
            Assert(workspace.Week == firstWeek + 1, "Timetable wheel still advances week after arrow removal");
            Descendants(main).OfType<Button>().Single(b => b.Content as string == "回到本周").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Assert(workspace.Week == firstWeek, "Today button returns to current week without arrow buttons");
            var studentCombo = Descendants(main).OfType<ComboBox>().Single();
            var toolbar = (StackPanel)VisualTreeHelper.GetParent(studentCombo);
            Assert(toolbar.Children[0] == studentCombo && toolbar.Children[1] is Button { Content: "＋", ToolTip: "新增学生" } && toolbar.Children[2] is Button { Content: "⟳" }, "Add student plus is between student selector and refresh");
            var newVm = new NewStudentViewModel();
            var newWindow = new NewStudentWindow(newVm, (_, _) => { }) { Owner = main }; newWindow.Show(); Flush();
            Assert(newVm.StudentName == "蜡笔小新" && newVm.Build(today).Settings.ClassesUnit == "one", "New student defaults to name and single mode");
            Assert(Descendants(newWindow).OfType<TextBox>().Count() == 1 && !Descendants(newWindow).Any(x => x is DataGrid or MondayPicker or TimeSelector), "New student contains only name input");
            Assert(Math.Abs(newWindow.Left + newWindow.ActualWidth / 2 - main.Left - main.ActualWidth / 2) < 2 && Math.Abs(newWindow.Top + newWindow.ActualHeight / 2 - main.Top - main.ActualHeight / 2) < 2, "New student centered on direct owner"); newWindow.Close();
            var vm = new SettingsViewModel(document);
            var settings = new SettingsWindow(vm, _ => { }) { Owner = main }; settings.Show(); Flush();
            Assert(settings.ActualWidth == 600 && settings.ActualHeight == 600, "Settings is 600 x 600 DIP");
            Assert(!vm.CanChangeMode && Descendants(settings).OfType<RadioButton>().Where(r => (string)r.Content is "单节" or "双节").All(r => !r.IsEnabled), "Existing courses lock mode in settings");
            Assert(Math.Abs(settings.Left + 300 - main.Left - main.ActualWidth / 2) < 2 && Math.Abs(settings.Top + 300 - main.Top - main.ActualHeight / 2) < 2, "Settings centered on owner");
            var boxes = Descendants(settings).OfType<TextBox>().ToList();
            var numeric = boxes.Where(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path is "TotalWeeks" or "Duration" or "OddBreak" or "EvenBreak").ToList();
            Assert(numeric.Count == 4, "Four numeric edit boxes");
            foreach (var box in numeric)
            {
                var name = BindingOperations.GetBinding(box, TextBox.TextProperty)!.Path.Path;
                var original = box.Text; box.Text = "１"; Flush();
                Assert(box.Text == original && !InputMethod.GetIsInputMethodEnabled(box) && !box.AllowDrop, name + " rejects Unicode digits, disables IME and drop");
                box.SelectAll();
                var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "1a"), false, DataFormats.UnicodeText) { RoutedEvent = DataObject.PastingEvent };
                box.RaiseEvent(paste); Assert(paste.CommandCancelled, name + " rejects invalid paste");
                var tooLong = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "1234"), false, DataFormats.UnicodeText) { RoutedEvent = DataObject.PastingEvent };
                box.RaiseEvent(tooLong); Assert(tooLong.CommandCancelled, name + " rejects overlong paste");
                box.ApplyTemplate(); var border = Descendants(box).OfType<Border>().First(); Assert(border.CornerRadius.TopLeft > 0, name + " rounded template");
            }
            var times = Descendants(settings).OfType<TimeSelector>().ToList();
            Assert(times.Count == 3, "Three split time selectors");
            var hourSets = new[] { "8,9,10,11", "13,14,15,16", "18,19" };
            for (var i = 0; i < times.Count; i++)
            {
                var combos = Descendants(times[i]).OfType<ComboBox>().ToList();
                Assert(string.Join(',', combos[0].Items.Cast<string>()) == hourSets[i] && string.Join(',', combos[1].Items.Cast<string>()) == "00,05,10,15,20,25,30,35,40,45,50,55" && combos.All(c => !c.IsEditable), "Time selector choices " + i);
            }
            var picker = Descendants(settings).OfType<MondayPicker>().Single();
            Assert(!Descendants(picker).OfType<TextBox>().Any(), "Date is a read-only label");
            var popup = picker.Children.OfType<Popup>().Single();
            var calendar = Descendants(popup.Child).OfType<Calendar>().Single();
            var icon = Descendants(picker).OfType<Button>().Single(); icon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Assert(popup.IsOpen && calendar.BlackoutDates.Contains(monday.AddDays(1).ToDateTime(TimeOnly.MinValue)), "Calendar opens and excludes non-Monday dates");
            var displayed = new DateTime(calendar.DisplayDate.Year, calendar.DisplayDate.Month, 1); var selected = vm.FirstMonday;
            var page = Descendants(settings).OfType<ScrollViewer>().Single(s => s.Name == "SettingsPage"); var offset = page.VerticalOffset;
            Wheel(calendar, 120); Flush(); Assert(calendar.DisplayDate == displayed.AddMonths(-1) && vm.FirstMonday == selected && page.VerticalOffset == offset, "Calendar wheel changes month without selection or page scroll");
            Wheel(calendar, -120); Flush(); Assert(calendar.DisplayDate == displayed, "Calendar wheel reverses by one month");
            Assert(Descendants(calendar).OfType<CalendarDayButton>().Where(b => b.IsInactive).All(b => !b.IsHitTestVisible && Descendants(b).OfType<Border>().First().Visibility == Visibility.Hidden), "Calendar hides adjacent-month dates");
            calendar.SelectedDate = monday.AddDays(7).ToDateTime(TimeOnly.MinValue); Flush();
            Assert(vm.FirstMonday == calendar.SelectedDate && !popup.IsOpen, "Calendar selection updates bound date and closes");
            var table = Descendants(settings).OfType<DataGrid>().Single();
            Assert(table.Columns.Count == 5 && table.Columns.All(c => c.Width.IsStar && c.Width.Value == 1) && table.RowHeight == table.ColumnHeaderHeight, "Preview columns equal and rows uniformly sized");
            Assert((string)table.Columns[^1].Header == "课后间隔" && vm.Periods[3].Gap == "—" && vm.Periods[7].Gap == "—", "Preview labels and session gaps");
            var firstTime = Descendants(times[0]).OfType<ComboBox>().ToList(); var minuteCombo = firstTime[1];
            minuteCombo.BringIntoView(); Flush(); minuteCombo.IsDropDownOpen = true; Flush();
            var minutePopup = (Popup)minuteCombo.Template.FindName("PART_Popup", minuteCombo);
            var menuScroll = Descendants(minutePopup.Child).OfType<ScrollViewer>().Single();
            var bar = Descendants(menuScroll).OfType<ScrollBar>().Single(b => b.Orientation == Orientation.Vertical);
            Assert(menuScroll.ActualHeight <= 112.1 && bar.ActualWidth == 11, "Time dropdown has four-row viewport and thin scrollbar");
            Assert(!Descendants(minuteCombo).OfType<TextBlock>().Any(t => t.Text == "⌄"), "Time dropdown has no arrow");
            var thumb = Descendants(bar).OfType<Thumb>().Single();
            thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            thumb.RaiseEvent(new DragDeltaEventArgs(0, 35) { RoutedEvent = Thumb.DragDeltaEvent });
            thumb.RaiseEvent(new DragCompletedEventArgs(0, 35, false) { RoutedEvent = Thumb.DragCompletedEvent }); Flush();
            Assert(menuScroll.VerticalOffset > 0 && vm.Morning == "08:00", "Thin scrollbar drag scrolls items without selecting a different minute");
            var oldTimeValue = vm.Morning; offset = page.VerticalOffset;
            Wheel(minutePopup.Child, -120); Flush();
            Assert(!minuteCombo.IsDropDownOpen && vm.Morning == oldTimeValue && page.VerticalOffset > offset, $"Dropdown wheel scrolls settings page and keeps value (offset {offset}->{page.VerticalOffset}, open={minuteCombo.IsDropDownOpen})");
            table.BringIntoView(); Flush(); offset = page.VerticalOffset;
            Wheel(table, 120); Flush(); Assert(page.VerticalOffset < offset, "Wheel over table scrolls settings page");
            var emptyDoc = StudentDocument.Create(monday); var emptyVm = new SettingsViewModel(emptyDoc);
            var emptyWindow = new SettingsWindow(emptyVm, _ => { }) { Owner = main }; emptyWindow.Show(); Flush();
            Assert(emptyVm.CanChangeMode && emptyVm.Mode == "单节", "Empty settings default to editable single mode");
            emptyVm.Duration = "40";
            var refresh = Descendants(emptyWindow).OfType<Button>().Single(b => b.ToolTip as string == "根据参数生成默认时间");
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Assert(emptyVm.Periods[0].End == "08:40" && emptyWindow.Messages.Current == "" && !Descendants(emptyWindow).OfType<TextBlock>().Any(t => t.Text.Contains("覆盖当前")), "Refresh immediately generates without confirmation or success message"); emptyWindow.Hide();
            var oldDocument = document.Clone(); oldDocument.Settings.TimeGeneration.MorningStart = "07:03";
            var oldVm = new SettingsViewModel(oldDocument);
            var oldWindow = new SettingsWindow(oldVm, _ => { }) { Owner = main }; oldWindow.Show(); Flush();
            var oldTime = Descendants(oldWindow).OfType<TimeSelector>().First(); var oldCombos = Descendants(oldTime).OfType<ComboBox>().ToList();
            Assert(oldVm.Build().Settings.TimeGeneration.MorningStart == "07:03", "Unsupported existing start preserved on opening");
            oldCombos[0].SelectedItem = "8"; Flush(); Assert(oldVm.Morning == "07:03", "Partial time selection preserves old value");
            oldCombos[1].SelectedItem = "05"; Flush(); Assert(oldVm.Morning == "08:05" && oldDocument.Settings.TimeGeneration.MorningStart == "07:03", "Complete selection updates only draft");
            oldWindow.Hide();
            var courseWindow = new CourseWindow(new CourseViewModel(document, document.Courses[0].Id), _ => { }) { Owner = main }; courseWindow.Show(); Flush();
            Assert(courseWindow.ActualWidth == 540 && courseWindow.ActualHeight == 550, "Course editor is compact 540 x 550");
            var courseTable = Descendants(courseWindow).OfType<DataGrid>().Single();
            var coursePage = Descendants(courseWindow).OfType<ScrollViewer>().First();
            var courseCancel = Descendants(courseWindow).OfType<Button>().Single(b => b.Content as string == "取消");
            var courseGap = courseCancel.TranslatePoint(new Point(), courseWindow).Y - courseTable.TranslatePoint(new Point(0, courseTable.ActualHeight), courseWindow).Y;
            Assert(coursePage.ScrollableHeight == 0 && courseGap is >= 0 and <= 24, $"Course table fits with compact footer gap ({courseGap:F1} DIP)");
            Assert(courseTable.Columns.All(c => c.Width.IsStar && c.Width.Value == 1), "Course table has equal width columns");
            Assert(courseTable.Columns.Count == 4 && (string)courseTable.Columns[1].Header == "时间" && !Descendants(courseWindow).OfType<Button>().Any(b => b.Content as string == "＋ 添加上课时段"), "Course table merges time and removes add button");
            Assert(!Descendants(courseWindow).OfType<ComboBox>().Any(), "Course color is a text box, not a dropdown");
            void CheckSlotDialog(DependencyObject source, bool expectNew)
            {
                Exception? failure = null; var seen = false;
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    var child = app.Windows.OfType<ScheduleWindow>().FirstOrDefault(w => w.IsVisible);
                    try { Assert(child is not null && ((ScheduleViewModel)child.DataContext).IsNew == expectNew, expectNew ? "Blank double click adds a slot despite old selection" : "Row double click edits actual slot"); seen = true; }
                    catch (Exception e) { failure = e; }
                    finally { child?.Close(); }
                }, DispatcherPriority.ApplicationIdle);
                DoubleClick(courseTable, source); Flush(); if (failure is not null) throw failure; Assert(seen, "Shared slot dialog returned without saving");
            }
            courseTable.SelectedIndex = 0; Flush();
            var selectedRow = Descendants(courseTable).OfType<DataGridRow>().First(r => r.IsSelected);
            var rowOverlay = Descendants(selectedRow).OfType<Border>().Single(b => b.Name == "RowSelection");
            Assert(rowOverlay.Visibility == Visibility.Visible && ReferenceEquals(rowOverlay.Background, app.FindResource("SelectionOverlay")) && rowOverlay.Effect is not null, "Selected schedule row uses shared translucent glow");
            Assert(Descendants(selectedRow).OfType<DataGridCell>().All(c => c.Foreground.ToString() == "#FF17223B" && c.BorderBrush.ToString() == "#FFE4EBF5"), "Selected cells keep dark text and subtle borders");
            CheckSlotDialog(courseTable, true);
            CheckSlotDialog(Descendants(courseTable).OfType<DataGridRow>().First(), false);
            var courseVm = (CourseViewModel)courseWindow.DataContext;
            for (var i = 0; i < 8; i++) courseVm.Course.Schedules.Add(new ScheduleEntry { Weeks = [1], DayOfWeek = 2, StartPeriod = 1 });
            courseVm.Refresh(); Flush();
            Assert(courseTable.ActualHeight >= courseTable.ColumnHeaderHeight + (courseTable.Items.Count + 1) * courseTable.RowHeight, "Many slots retain a blank row for double-click creation");
            CheckSlotDialog(courseTable, true);
            var slotWindow = new ScheduleWindow(new ScheduleViewModel(document.Settings, document.Courses[0].Schedules[0]), _ => { }) { Owner = courseWindow }; slotWindow.Show(); Flush();
            Assert(slotWindow.ActualWidth == 320 && slotWindow.ActualHeight == 340, "Schedule editor fits content at 320 x 340");
            var slotPage = Descendants(slotWindow).OfType<ScrollViewer>().First();
            Assert(slotPage.ScrollableHeight == 0 && slotPage.ScrollableWidth == 0, "Default slot layout needs no scrollbar");
            var slotLabels = Descendants(slotWindow).OfType<TextBlock>().Where(t => new[] { "上课周次", "周次筛选", "时间", "上课地点" }.Contains(t.Text)).ToList();
            Assert(slotLabels.Count == 4 && slotLabels.All(t => t.FontSize == 14), "Slot parameters use shared 14 DIP labels");
            var notes = Descendants(slotWindow).OfType<TextBlock>().Where(t => t.Text.StartsWith("周次：") || t.Text.StartsWith("时间：")).ToList();
            Assert(notes.Count == 2 && notes.All(t => t.FontSize == 12 && t.Foreground.ToString() == "#FF718096") && notes[1].Text == "时间：周几与节次，如 103, 20304。", "Notes use shared smaller muted text and exact time example");
            var pattern = Descendants(slotWindow).OfType<ComboBox>().Single();
            Assert(pattern.FontSize == 14 && (string)pattern.Items[0] == "自定义" && pattern.ActualHeight == Descendants(slotWindow).OfType<TextBox>().First().ActualHeight, $"Pattern uses custom label and matches input height: font={pattern.FontSize}, label={pattern.Items[0]}, combo={pattern.ActualHeight}, input={Descendants(slotWindow).OfType<TextBox>().First().ActualHeight}");
            var slotBoxes = Descendants(slotWindow).OfType<TextBox>().ToList();
            var weekBox = slotBoxes.Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == "Weeks");
            var timeBox = slotBoxes.Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == "Time");
            var locationBox = slotBoxes.Single(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == "Location");
            var slotCancel = Descendants(slotWindow).OfType<Button>().Single(b => b.Content as string == "取消");
            var slotGap = slotCancel.TranslatePoint(new Point(), slotWindow).Y - locationBox.TranslatePoint(new Point(0, locationBox.ActualHeight), slotWindow).Y;
            Assert(slotGap is >= 0 and <= 24 && notes[0].Text == "周次：1-5, 7, 8", $"Slot content fits with compact footer gap ({slotGap:F1} DIP)");
            var prior = weekBox.Text; weekBox.Text = "1，2"; Flush(); Assert(weekBox.Text == prior, "Week editor rejects non-ASCII comma");
            var pasteWeeks = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "1-5, 7, 8"), false, DataFormats.UnicodeText) { RoutedEvent = DataObject.PastingEvent }; weekBox.SelectAll(); weekBox.RaiseEvent(pasteWeeks);
            Assert(!pasteWeeks.CommandCancelled, "Week editor accepts ASCII ranges commas and spaces");
            var priorTime = timeBox.Text; timeBox.Text = "3-1"; Flush(); Assert(timeBox.Text == priorTime, "Time editor accepts digits only");
            pattern.IsDropDownOpen = true; Flush();
            var patternPopup = (Popup)pattern.Template.FindName("PART_Popup", pattern);
            var options = Descendants(patternPopup.Child).OfType<ComboBoxItem>().ToList();
            Assert(options.Count == 3 && options.All(o => o.FontSize == pattern.FontSize && Descendants(o).OfType<Border>().First().CornerRadius.TopLeft == 6), "All dropdown options use shared fonts and rounded highlight surface");
            pattern.IsDropDownOpen = false;
            var inputs = Descendants(slotWindow).OfType<TextBox>().Cast<FrameworkElement>().Append(pattern).ToList();
            Assert(inputs.All(i => Math.Abs(i.TranslatePoint(new Point(), slotWindow).X - inputs[0].TranslatePoint(new Point(), slotWindow).X) < 1), "Slot inputs align on one shared column");
            slotWindow.Hide(); courseWindow.Hide();
            Guid? opened = Guid.Empty; var calls = 0; Window? passedOwner = null;
            var manager = new CourseManagerWindow(workspace, (id, _, _, owner) => { opened = id; passedOwner = owner; calls++; }) { Owner = main }; manager.Show(); Flush();
            var managerTable = Descendants(manager).OfType<GroupedCourseTable>().Single();
            var groupGrid = Descendants(managerTable).OfType<Grid>().Single(g => g.Name == "CourseGroups");
            var headerGrid = Descendants(managerTable).OfType<Grid>().Single(g => g.Name == "CourseHeader");
            Assert(headerGrid.Children.OfType<Border>().Select(b => ((TextBlock)b.Child).Text).SequenceEqual(new[] { "课程名", "周次", "时间", "地点" }), "Manager has four columns");
            var nameCell = groupGrid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0 && Equals(b.Tag, document.Courses[0].Id));
            Assert(Grid.GetRowSpan(nameCell) == 3 && ((TextBlock)nameCell.Child).VerticalAlignment == VerticalAlignment.Center, "Course name is one vertically merged cell");
            Assert(groupGrid.Children.OfType<Border>().Where(b => Grid.GetColumn(b) == 2 && Equals(b.Tag, document.Courses[0].Id)).Select(b => ((TextBlock)b.Child).Text).SequenceEqual(new[] { "10102", "50506", "30304" }), "Group rows sort by week then time");
            Assert(Descendants(manager).OfType<Button>().Where(b => b.Content is string).Select(b => (string)b.Content).Order().SequenceEqual(new[] { "删除", "新建" }.Order()), "Manager has only create and delete buttons");
            DoubleClick(managerTable, nameCell); Assert(opened == document.Courses[0].Id && managerTable.SelectedCourseId == opened && passedOwner == manager, "Manager merged cell edits and selects whole course");
            Assert(nameCell.Background is RadialGradientBrush nameFill && nameFill.GradientStops[0].Color == (Color)ColorConverter.ConvertFromString(document.Courses[0].Color) && nameFill.GradientStops[^1].Color != nameFill.GradientStops[0].Color && nameCell.CornerRadius.TopLeft > 0, "Selected course name retains original center color with rounded fading edges");
            var selection = groupGrid.Children.OfType<Border>().Single(b => b.Name == "GroupSelection" && b.Visibility == Visibility.Visible);
            Assert(Grid.GetRowSpan(selection) == 3 && Grid.GetColumnSpan(selection) == 4 && !selection.IsHitTestVisible && selection.Effect is not null, "One translucent glowing overlay covers the selected course group");
            var ordinary = groupGrid.Children.OfType<Border>().Where(b => Grid.GetColumn(b) == 1 && b.Tag is Guid).ToList();
            Assert(ordinary[0].Background.ToString() != ordinary[^1].Background.ToString(), "Alternating backgrounds follow course groups");
            DoubleClick(managerTable, groupGrid); Assert(opened is null && passedOwner == manager, "Manager blank double click ignores old selection and creates");
            var before = calls; DoubleClick(managerTable, headerGrid); Assert(calls == before, "Manager header double click is ignored"); manager.Close();
            var scrollProbe = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 600, Height = 600 } };
            var scrollWindow = new Window { Width = 240, Height = 240, Content = scrollProbe, Owner = main }; scrollWindow.Show(); Flush();
            var probeBars = Descendants(scrollProbe).OfType<ScrollBar>().ToList();
            Assert(probeBars.Count == 2 && probeBars.All(b => b.Orientation == Orientation.Vertical ? b.ActualWidth == 11 : b.ActualHeight == 11), "Global scrollbars share 11 DIP thickness in both directions");
            var horizontal = probeBars.Single(b => b.Orientation == Orientation.Horizontal); var horizontalThumb = Descendants(horizontal).OfType<Thumb>().Single();
            horizontalThumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            horizontalThumb.RaiseEvent(new DragDeltaEventArgs(30, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            horizontalThumb.RaiseEvent(new DragCompletedEventArgs(30, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); Flush();
            Assert(scrollProbe.HorizontalOffset > 0, "Horizontal scrollbar thumb scrolls in the correct direction"); scrollWindow.Close();
            if (args.Contains("--preview"))
            {
                var preview = new CourseManagerWindow(workspace, (id, _, _, owner) =>
                {
                    var editor = new CourseWindow(new CourseViewModel(workspace.Document!, id), workspace.Save) { Owner = owner };
                    editor.ShowDialog();
                }) { Owner = main };
                preview.Show(); preview.Activate(); settings.Hide();
                // Only isolated test documents and windows participate in the preview.
                Console.WriteLine("PREVIEW_READY"); Dispatcher.Run();
            }
            Console.WriteLine($"UI CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Wheel(UIElement source, int delta) => source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
    private static void DoubleClick(Control table, DependencyObject source) => table.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = source });
}
