using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--activate-existing", var secondaryKey])
        {
            using var secondary = new ClassSchedule.SingleInstance(secondaryKey);
            return !secondary.IsPrimary && secondary.ActivateExisting() ? 0 : 2;
        }
        // Load the real theme without running production startup or opening user data.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/ClassSchedule;component/Resources/Theme.xaml") };
        if (args.Contains("--floating-confirmations")) return FloatingConfirmationChecks.Run(app);
        if (args.Contains("--today-toast")) return TodayToastChecks.Run(app);
        if (args.Contains("--student-scan-errors")) return StudentScanErrorChecks.Run(app);
        if (args.Contains("--reminder-popup-layout")) return ReminderPopupChecks.Run(app, true);
        if (args.Contains("--reminder-popup")) return ReminderPopupChecks.Run(app);
        if (args.Contains("--tray-startup")) return TrayStartupChecks.Run(app, args.Contains("--tray-smoke"));
        if (args.Contains("--button-feedback")) return ButtonFeedbackChecks.Run(app);
        if (args.Contains("--layout-stability")) return LayoutStabilityChecks.Run(app);
        if (args.Contains("--compact-slots")) return CompactSlotChecks.Run(app);
        if (args.Contains("--duration-selector")) return DurationAndSelectorChecks.Run(app);
        if (args.Contains("--course-reminders")) return CourseReminderChecks.Run(app, args.Contains("--preview"), args.Contains("--sound-smoke"));
        if (args.Contains("--confirmations")) return ConfirmationChecks.Run(app);
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
            var instanceKey = "ClassSchedule.check." + Guid.NewGuid().ToString("N");
            using var primaryInstance = new ClassSchedule.SingleInstance(instanceKey); primaryInstance.Attach(main);
            void ActivateFromChild()
            {
                var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(Program).Assembly.Location);
                start.ArgumentList.Add("--activate-existing"); start.ArgumentList.Add(instanceKey);
                using var child = System.Diagnostics.Process.Start(start)!;
                Assert(child.WaitForExit(10000) && child.ExitCode == 0, "Second process signals existing instance without opening a window"); Flush();
            }
            main.WindowState = WindowState.Minimized; Flush(); ActivateFromChild();
            Assert(main.WindowState == WindowState.Normal && main.IsVisible, "Activation restores minimized main window");
            var modal = new Window { Owner = main, Width = 300, Height = 300, ShowInTaskbar = false }; var modalActivated = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(() => {
                var nestedModal = new Window { Owner = modal, Width = 250, Height = 250, ShowInTaskbar = false };
                Dispatcher.CurrentDispatcher.BeginInvoke(() => { try { ActivateFromChild(); modalActivated = nestedModal.IsActive; } finally { nestedModal.Close(); } }, DispatcherPriority.ApplicationIdle);
                try { nestedModal.ShowDialog(); } finally { modal.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            modal.ShowDialog();
            Assert(modalActivated && !main.Topmost, "Activation targets deepest of two owned modals and does not make window permanently topmost");
            Assert(main.Width == 600 && main.Height == 600 && main.ActualWidth == 600 && main.MinWidth <= 600, "Main is 600 x 600 DIP");
            Assert(main.WindowStartupLocation == WindowStartupLocation.CenterScreen, "Main starts centered on screen");
            Assert(!Descendants(main).OfType<Button>().Any(b => b.ToolTip as string is "上一周" or "下一周") && Descendants(main).OfType<Button>().Any(b => b.Content as string == "本周"), "Main removes week arrow buttons and keeps today");
            var firstWeek = workspace.Week;
            var timeHeading = Descendants(main).OfType<TextBlock>().Single(t => t.Text == "时间 / 节次");
            Wheel(timeHeading, -120); Flush();
            Assert(workspace.Week == firstWeek + 1, "Timetable wheel still advances week after arrow removal");
            Descendants(main).OfType<Button>().Single(b => b.Content as string == "本周").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Assert(workspace.Week == firstWeek, "Today button returns to current week without arrow buttons");
            Assert(Descendants(main).OfType<Button>().Count() == 2 && Descendants(main).OfType<ComboBox>().Count() == 1, "Main has only student, today and more controls");
            var more = Descendants(main).OfType<Button>().Single(b => b.ToolTip as string == "设置");
            var studentSelector = Descendants(main).OfType<ComboBox>().Single();
            studentSelector.IsDropDownOpen = true; Flush();
            var studentPopup = (Popup)studentSelector.Template.FindName("PART_Popup", studentSelector);
            Assert(studentSelector.ActualWidth == 88 && ((FrameworkElement)studentPopup.Child).ActualWidth == 88, "Student display and dropdown outer widths are both 88 DIP");
            studentSelector.IsDropDownOpen = false;
            Assert(more.ContextMenu!.Items.OfType<MenuItem>().Select(i => (string)i.Header).SequenceEqual(new[] { "开机启动", "总体设置", "课程管理", "学生管理", "更新学生信息" }), "Gear menu has the approved labels and order");
            var studentManager = new StudentManagementWindow(workspace) { Owner = main }; studentManager.Show(); Flush();
            void ClickStudent(string label) { Descendants(studentManager).OfType<Button>().Single(b => b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
            var studentName = Descendants(studentManager).OfType<TextBox>().Single();
            var studentList = Descendants(studentManager).OfType<ListBox>().Single();
            var firstStudentRow = (ListBoxItem)studentList.ItemContainerGenerator.ContainerFromIndex(0);
            Assert(firstStudentRow.ActualHeight == 36 && firstStudentRow.FontSize == 14 && studentList.AlternationCount == 2, "Student rows use course table height, font and alternation");
            studentList.SelectedIndex = 0; Flush(); firstStudentRow.ApplyTemplate(); var studentSelection = (Border)firstStudentRow.Template.FindName("Selection", firstStudentRow);
            Assert(studentSelection.Visibility == Visibility.Visible && studentSelection.Background == app.FindResource("SelectionOverlay") && studentSelection.Effect == app.FindResource("SelectionGlow"), "Student selection uses shared translucent glow");
            Assert(studentManager.Width == 320 && studentManager.Height == 340 && studentManager.Icon is not null && main.Icon is not null, "Student management is an owned 320 x 340 window with Bee icon");
            studentName.Text = "界面检查"; ClickStudent("新增");
            Assert(studentName.Text == "界面检查" && workspace.Names.Count == 1, "Duplicate student preserves name and saved data");
            studentName.Text = "新增检查"; ClickStudent("新增");
            Assert(workspace.Current!.Name == "新增检查" && workspace.Document!.Settings.ClassesUnit == "one", "Student create selects single-mode student");
            studentName.Text = "更新检查"; ClickStudent("更新");
            Assert(workspace.Current!.Name == "更新检查" && workspace.Names.Contains("更新检查") && !workspace.Names.Contains("新增检查"), "Update renames selected student and current selection");
            studentName.Text = "未更新姓名"; Descendants(studentManager).OfType<ListBox>().Single().SelectedItem = "界面检查"; Flush();
            Assert(!studentName.IsEnabled && studentManager.Model.Selected!.Name == "更新检查", "Changing selection protects unsaved student name");
            ClickStudent("继续编辑"); Assert(studentName.Text == "未更新姓名", "Continue preserves student name draft"); studentName.Text = "更新检查";
            ClickStudent("删除");
            Assert(Descendants(studentManager).OfType<TextBlock>().Any(t => t.Text == "是否删除‘更新检查’的全部课程信息？") && !studentName.IsEnabled && Keyboard.FocusedElement is Button { Content: "否" }, "Delete confirmation freezes form and focuses No");
            ClickStudent("否"); Assert(workspace.Names.Contains("更新检查") && studentName.IsEnabled, "No cancels deletion without changing student");
            ClickStudent("删除");
            studentManager.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(studentManager), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent }); Flush();
            Assert(studentName.IsEnabled && workspace.Names.Contains("更新检查"), "Escape cancels student deletion without changing data");
            ClickStudent("删除"); ClickStudent("是");
            Assert(!workspace.Names.Contains("更新检查") && workspace.Current!.Name == "界面检查", "Yes deletes selected student and chooses remaining student");
            studentList.SelectedItem = "界面检查"; Flush(); ClickStudent("删除"); ClickStudent("是");
            Assert(workspace.Current is null && workspace.Names.Count == 0, "Deleting last student leaves an empty workspace");
            studentName.Text = "界面检查"; ClickStudent("新增");
            Assert(workspace.Current!.Name == "界面检查", "Empty workspace can create a student again");
            workspace.Save(document); studentManager.Close(); main.RefreshStudents(); Flush();
            var stalePath = Path.Combine(path, "stale"); var staleWorkspace = new WorkspaceViewModel(new StudentFileStore(stalePath)); staleWorkspace.Create("临时", document);
            File.Delete(Path.Combine(stalePath, "临时.json"));
            var staleManager = new StudentManagementWindow(staleWorkspace) { Owner = main }; staleManager.Show(); Flush(); Descendants(staleManager).OfType<ListBox>().Single().SelectedItem = "临时"; Flush();
            Assert(staleManager.Messages.Current.Length > 0 && staleManager.IsVisible, "Selecting missing student reports bottom error instead of throwing"); staleManager.Close();
            StudentWorkspaceWindow SettingsHost(StudentDocument doc)
            {
                var w = new WorkspaceViewModel(new StudentFileStore(Path.Combine(path, Guid.NewGuid().ToString()))); w.Create("界面检查", doc);
                return new GeneralSettingsWindow(w) { Owner = main };
            }
            var settings = SettingsHost(document); settings.Show(); Flush(); var vm = settings.State.Settings!;
            Assert(((TextBlock)settings.FindName("Heading")).HorizontalAlignment == HorizontalAlignment.Center, "Settings title is centered");
            Assert(settings.ActualWidth == 600 && settings.ActualHeight == 600, "Settings is 600 x 600 DIP");
            Assert(!vm.CanChangeMode && Descendants(settings).OfType<RadioButton>().Where(r => (string)r.Content is "单节" or "双节").All(r => !r.IsEnabled), "Existing courses lock mode in settings");
            Assert(Math.Abs(settings.Left + 300 - main.Left - main.ActualWidth / 2) < 2 && Math.Abs(settings.Top + 300 - main.Top - main.ActualHeight / 2) < 2, "Settings centered on owner");
            var basicGroup = Descendants(settings).OfType<Border>().Single(b => b.Name == "BasicInformation");
            var parameterGroup = Descendants(settings).OfType<Border>().Single(b => b.Name == "ClassParameters");
            Assert(Grid.GetRow(basicGroup) == Grid.GetRow(parameterGroup) && Grid.GetColumn(parameterGroup) == 2, "Settings groups are side by side at default width");
            var radios = Descendants(basicGroup).OfType<RadioButton>().ToList();
            Assert(radios.Count == 6 && radios.Where((_, i) => i % 2 == 0).Select(r => r.TranslatePoint(new Point(), settings).X).Distinct().Count() == 1 && radios.Where((_, i) => i % 2 == 1).Select(r => r.TranslatePoint(new Point(), settings).X).Distinct().Count() == 1, "Both radio option columns align across all rows");
            var longestLabel = Descendants(basicGroup).OfType<TextBlock>().Single(t => t.Text == "本学期总周数");
            Assert(longestLabel.ActualHeight < 30, $"Long basic label stays on one line ({longestLabel.ActualHeight:F1} DIP)");
            Assert(Descendants(parameterGroup).OfType<ComboBox>().All(c => c.Width == 43), "Time dropdowns are integer 43 DIP wide");
            Assert(!Descendants(parameterGroup).OfType<TextBlock>().Any(t => t.Text.Contains("（分钟）")) && Descendants(parameterGroup).OfType<TextBlock>().Count(t => t.Text == "分") == 6, "Duration and breaks show units after edit boxes");
            settings.Width = 480; Flush(); Assert(Grid.GetRow(parameterGroup) == 1 && Grid.GetColumn(parameterGroup) == 0, "Narrow settings window stacks both groups");
            settings.Width = 600; Flush();
            var boxes = Descendants(settings).OfType<TextBox>().ToList();
            var numeric = boxes.Where(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path is "TotalWeeks" or "Duration" or "OddBreak" or "EvenBreak").ToList();
            Assert(numeric.Count == 4, "Four numeric edit boxes");
            Assert(numeric.Where(b => BindingOperations.GetBinding(b, TextBox.TextProperty)!.Path.Path != "TotalWeeks").All(b => b.ActualWidth == 64), "Duration and break edit boxes actually measure 64 DIP");
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
            Assert(minuteCombo.IsDropDownOpen && vm.Morning == oldTimeValue && page.VerticalOffset == offset && menuScroll.VerticalOffset > 0, $"Dropdown wheel scrolls options, stays open and keeps page and value (page {offset}->{page.VerticalOffset}, popup={menuScroll.VerticalOffset})");
            menuScroll.ScrollToBottom(); Flush(); var bottom = menuScroll.VerticalOffset;
            Wheel(minutePopup.Child, -120); Flush();
            Assert(minuteCombo.IsDropDownOpen && menuScroll.VerticalOffset == bottom && page.VerticalOffset == offset, "Popup bottom wheel stays inside options");
            table.BringIntoView(); Flush(); var outsideOffset = page.VerticalOffset;
            Wheel(table, 120); Flush();
            Assert(!minuteCombo.IsDropDownOpen && page.VerticalOffset < outsideOffset, "Outside wheel closes time popup and scrolls page in same event");
            minuteCombo.IsDropDownOpen = true; page.ScrollToBottom(); Flush(); var footerOffset = page.VerticalOffset;
            Wheel(Descendants(settings).OfType<Button>().Single(b => b.Content as string == "保存"), 120); Flush();
            Assert(!minuteCombo.IsDropDownOpen && page.VerticalOffset < footerOffset, "Footer wheel closes popup and scrolls settings page");
            page.ScrollToTop(); Flush(); Wheel((TextBlock)settings.FindName("Heading"), -120); Flush();
            Assert(page.VerticalOffset > 0, "Title wheel scrolls settings page");
            foreach (var choice in times.SelectMany(t => Descendants(t).OfType<ComboBox>())) {
                choice.IsDropDownOpen = true; Flush(); var choicePopup = (Popup)choice.Template.FindName("PART_Popup", choice);
                var value = choice.SelectedItem; var pageOffset = page.VerticalOffset;
                Wheel(choicePopup.Child, -120); Flush();
                Assert(choice.IsDropDownOpen && Equals(choice.SelectedItem, value) && page.VerticalOffset == pageOffset, "Each of six time dropdowns owns its wheel");
                choice.IsDropDownOpen = false;
            }
            table.BringIntoView(); Flush(); offset = page.VerticalOffset;
            Wheel(table, 120); Flush(); Assert(page.VerticalOffset < offset, "Wheel over table scrolls settings page");
            var emptyDoc = StudentDocument.Create(monday);
            var emptyWindow = SettingsHost(emptyDoc); emptyWindow.Show(); Flush(); var emptyVm = emptyWindow.State.Settings!;
            Assert(emptyVm.CanChangeMode && emptyVm.Mode == "单节", "Empty settings default to editable single mode");
            emptyVm.Duration = "40";
            var refresh = Descendants(emptyWindow).OfType<Button>().Single(b => b.ToolTip as string == "根据参数生成默认时间");
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
            Assert(emptyVm.Periods[0].End == "08:40" && emptyWindow.Messages.Current == "" && !Descendants(emptyWindow).OfType<TextBlock>().Any(t => t.Text.Contains("覆盖当前")), "Refresh immediately generates without confirmation or success message"); emptyWindow.Hide();
            var oldDocument = document.Clone(); oldDocument.Settings.TimeGeneration.MorningStart = "07:03";
            var oldWindow = SettingsHost(oldDocument); oldWindow.Show(); Flush(); var oldVm = oldWindow.State.Settings!;
            var oldTime = Descendants(oldWindow).OfType<TimeSelector>().First(); var oldCombos = Descendants(oldTime).OfType<ComboBox>().ToList();
            Assert(oldVm.Build().Settings.TimeGeneration.MorningStart == "07:03", "Unsupported existing start preserved on opening");
            oldCombos[0].SelectedItem = "8"; Flush(); Assert(oldVm.Morning == "07:03", "Partial time selection preserves old value");
            oldCombos[1].SelectedItem = "05"; Flush(); Assert(oldVm.Morning == "08:05" && oldDocument.Settings.TimeGeneration.MorningStart == "07:03", "Complete selection updates only draft");
            oldWindow.Hide();
            var courseWindow = new CourseManagementWindow(workspace, WorkspacePage.Course, document.Courses[0].Id) { Owner = main }; courseWindow.Show(); Flush();
            Assert(courseWindow.ActualWidth == 520 && courseWindow.ActualHeight == 540, "Course editing stays in the 520 DIP course window");
            var courseTable = Descendants(courseWindow).OfType<DataGrid>().Single();
            var coursePage = Descendants(courseWindow).OfType<ScrollViewer>().First();
            var courseCancel = Descendants(courseWindow).OfType<Button>().Single(b => b.Content as string == "取消");
            var courseGap = courseCancel.TranslatePoint(new Point(), courseWindow).Y - courseTable.TranslatePoint(new Point(0, courseTable.ActualHeight), courseWindow).Y;
            Assert(coursePage.ScrollableHeight == 0 && courseGap is >= 0 and <= 24, $"Course table fits with compact footer gap ({courseGap:F1} DIP)");
            Assert(courseTable.Columns.All(c => c.Width.IsStar && c.Width.Value == 1), "Course table has equal width columns");
            Assert(courseTable.Columns.Count == 4 && (string)courseTable.Columns[1].Header == "时间" && !Descendants(courseWindow).OfType<Button>().Any(b => b.Content as string == "＋ 添加上课时段"), "Course table merges time and removes add button");
            Assert(!Descendants(courseWindow).OfType<TextBox>().Any(b => BindingOperations.GetBinding(b, TextBox.TextProperty)?.Path.Path == "Course.Color"), "Course color is not editable");
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
            var courseVm = courseWindow.State.Course!;
            for (var i = 0; i < 8; i++) courseVm.Course.Schedules.Add(new ScheduleEntry { Weeks = [1], DayOfWeek = 2, StartPeriod = 1 });
            courseVm.Refresh(); Flush();
            Assert(Descendants(courseWindow).OfType<Button>().Any(b => b.Content as string == "＋" && b.IsEnabled), "Many slots retain an explicit add button");
            CheckSlotDialog(courseTable, true);
            var slotWindow = new ScheduleWindow(new ScheduleViewModel(document.Settings, document.Courses[0].Schedules[0]), _ => { }) { Owner = courseWindow }; slotWindow.Show(); Flush();
            Assert(slotWindow.ActualWidth == 320 && slotWindow.ActualHeight == 390, "Schedule editor fits content at 320 x 390");
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
            StudentWorkspaceWindow manager = new CourseManagementWindow(workspace) { Owner = main }; manager.Show(); Flush();
            var managerTable = Descendants(manager).OfType<GroupedCourseTable>().Single();
            var groupGrid = Descendants(managerTable).OfType<Grid>().Single(g => g.Name == "CourseGroups");
            var headerGrid = Descendants(managerTable).OfType<Grid>().Single(g => g.Name == "CourseHeader");
            Assert(headerGrid.Children.OfType<Border>().Select(b => ((TextBlock)b.Child).Text).SequenceEqual(new[] { "课程名", "周次", "时间", "地点" }), "Manager has four columns");
            var nameCell = groupGrid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0 && Equals(b.Tag, document.Courses[0].Id));
            Assert(manager.ActualWidth == 520 && manager.Title.StartsWith("课程管理") && !Descendants(manager).OfType<Button>().Any(b => b.Content as string == "总体设置"), "Course management is a separate narrow window without settings navigation");
            Assert(groupGrid.ColumnDefinitions.Take(3).Select(c => c.Width.Value).SequenceEqual(new[] { 120.0, 110.0, 70.0 }) && groupGrid.ColumnDefinitions.Take(3).All(c => c.Width.IsAbsolute), "Management columns use fixed integer widths 120 110 70");
            nameCell.UpdateLayout(); var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(nameCell.ActualWidth), (int)Math.Ceiling(nameCell.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(nameCell);
            var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(5, 18, 1, 1), pixel, 4, 0);
            var originalColor = (Color)ColorConverter.ConvertFromString(document.Courses[0].Color);
            Assert(pixel[2] == originalColor.R && pixel[1] == originalColor.G && pixel[0] == originalColor.B, "Course name is solid original color beyond the narrow edge rim");
            Assert(Grid.GetRowSpan(nameCell) == 3 && ((TextBlock)nameCell.Child).VerticalAlignment == VerticalAlignment.Center, "Course name is one vertically merged cell");
            var before = app.Windows.Count; DoubleClick(managerTable, nameCell); Flush();
            Assert(manager.State.Page == WorkspacePage.Course && Descendants(manager).OfType<CourseEditorView>().Any() && app.Windows.Count == before, "Course double click switches page without another window");
            manager.State.Course!.Course.Name = "修改"; manager.State.ShowSettings(); Flush();
            Assert(manager.State.Confirmation == WorkspaceConfirmation.Leave && !Descendants(manager).OfType<CourseEditorView>().Single().IsEnabled, "Dirty navigation freezes editor and asks inline");
            manager.State.CancelConfirmation(); manager.State.Course.Course.Name = "高等数学"; manager.State.ShowCourses(); Flush();
            managerTable = Descendants(manager).OfType<GroupedCourseTable>().Single();
            groupGrid = Descendants(managerTable).OfType<Grid>().Single(g => g.Name == "CourseGroups");
            DoubleClick(managerTable, groupGrid); Flush(); Assert(manager.State.Course!.IsNew, "Blank double click creates course in same window");
            manager.State.ShowCourses();
            manager.State.Edit(document.Courses[0].Id); manager.State.Course!.ReminderCount = "4";
            manager.State.Save(); Flush(); Assert(manager.State.Page == WorkspacePage.Courses, "Course save returns to list without closing workspace");
            manager.State.ShowSettings(); manager.State.Settings!.Duration = "41"; manager.State.Save(); Flush();
            Assert(!manager.IsVisible && workspace.Document!.Settings.TimeGeneration.DurationMinutes == 41, "Settings save closes after committing");
            manager = new GeneralSettingsWindow(workspace) { Owner = main }; manager.Show(); Flush();
            manager.State.Settings!.Duration = "42"; manager.Close(); Flush();
            Assert(manager.IsVisible && manager.State.Confirmation == WorkspaceConfirmation.Leave, "Workspace close protects unsaved settings");
            manager.State.CancelConfirmation(); manager.State.CancelSettings(); Flush();
            Assert(!manager.IsVisible && workspace.Document!.Courses[0].ReminderCount == 4, "Close retains earlier page saves");
            var scrollProbe = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 600, Height = 600 } };
            var scrollWindow = new Window { Width = 240, Height = 240, Content = scrollProbe, Owner = main }; scrollWindow.Show(); Flush();
            var probeBars = Descendants(scrollProbe).OfType<ScrollBar>().ToList();
            Assert(probeBars.Count == 2 && probeBars.All(b => b.Orientation == Orientation.Vertical ? b.ActualWidth == 11 : b.ActualHeight == 11), "Global scrollbars share 11 DIP thickness in both directions");
            var horizontal = probeBars.Single(b => b.Orientation == Orientation.Horizontal); var horizontalThumb = Descendants(horizontal).OfType<Thumb>().Single();
            horizontalThumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            horizontalThumb.RaiseEvent(new DragDeltaEventArgs(30, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            horizontalThumb.RaiseEvent(new DragCompletedEventArgs(30, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); Flush();
            Assert(scrollProbe.HorizontalOffset > 0, "Horizontal scrollbar thumb scrolls in the correct direction"); scrollWindow.Close();
            if (args.Contains("--settings-preview"))
            {
                settings.Hide(); main.Hide();
                var standalone = new GeneralSettingsWindow(workspace) { ShowInTaskbar = true }; standalone.Show();
                Console.WriteLine("SETTINGS_PREVIEW_READY"); Dispatcher.Run();
            }
            if (args.Contains("--preview"))
            {
                settings.Hide(); main.Activate();
                Console.WriteLine("PREVIEW_READY"); Dispatcher.Run();
            }
            Console.WriteLine($"UI CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Wheel(UIElement source, int delta) => source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
    internal static void DoubleClick(Control table, DependencyObject source) => table.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = source });
}
