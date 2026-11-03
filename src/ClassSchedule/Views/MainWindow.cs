using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public sealed class MainWindow : DialogWindow
{
    private readonly WorkspaceViewModel vm;
    private readonly ComboBox students = new() { Width = 88, MinWidth = 0, MaxWidth = 88 };
    private readonly TextBlock weekLabel = Ui.Text("第 1 周", 17);
    private readonly TextBlock status = Ui.Text("");
    private readonly Grid table = new();
    private readonly List<(Border Cell, DateOnly Date, bool Header)> datedCells = [];
    private readonly Brush headerBackground = new SolidColorBrush(Color.FromRgb(238, 245, 255));
    private readonly Brush todayHeaderBackground = new SolidColorBrush(Color.FromRgb(255, 231, 190));
    private readonly Brush todayBackground = new SolidColorBrush(Color.FromRgb(255, 242, 216));
    private readonly DispatcherTimer dateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Func<DateOnly> currentDate;
    private DateOnly? highlightedDate;
    private readonly Button more = Ui.Button("", () => { }, "设置");
    private readonly MenuItem startupItem = new() { Header = "开机启动", IsCheckable = true, IsChecked = true };
    private StartupSettings? startup;
    private bool rendering;
    private (string? Name, string? Fingerprint, int Week)? renderedTable;
    private DateTime lastWheel;
    private int wheelDelta;
    public MainWindow(WorkspaceViewModel viewModel) : this(viewModel, () => DateOnly.FromDateTime(DateTime.Now)) { }
    internal MainWindow(WorkspaceViewModel viewModel, Func<DateOnly> currentDate) : base("课程表", 600, 600, false)
    {
        this.currentDate = currentDate;
        vm = viewModel; DataContext = vm; MinWidth = 520; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        // Match the popup's 4 DIP padding plus the option's 8 DIP padding.
        students.Template = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
         <Grid>
          <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" Background="White" BorderBrush="#C9D8ED">
           <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="b" CornerRadius="14" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" Background="{TemplateBinding Background}" Padding="12,1"><ContentPresenter VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#E8F0FF"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
           <TextBlock Text="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType=ComboBox}}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="NoWrap" ToolTip="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType=ComboBox}}"/>
          </ToggleButton>
          <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border Background="White" BorderBrush="#C9D8ED" BorderThickness="1" CornerRadius="10" Padding="4" Width="{TemplateBinding ActualWidth}"><ScrollViewer MaxHeight="300" HorizontalScrollBarVisibility="Disabled"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup>
         </Grid>
         <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter Property="BorderBrush" Value="#2563EB"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
        students.ItemTemplate = (DataTemplate)XamlReader.Parse("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="{Binding}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="NoWrap" ToolTip="{Binding}"/></DataTemplate>
        """);
        more.Content = new Image { Source = (ImageSource)Application.Current.FindResource("SettingsGear"), Width = 18, Height = 18 };
        more.Width = 40; more.Padding = new Thickness(8);
        students.ToolTip = "选择学生"; System.Windows.Automation.AutomationProperties.SetName(students, "选择学生");
        bool RejectEmptyList()
        {
            if (students.HasItems) return false;
            students.IsDropDownOpen = false; Messages.Enqueue("当前列表中没有成员。"); return true;
        }
        students.PreviewMouseLeftButtonDown += (_, e) => { if (RejectEmptyList()) e.Handled = true; };
        students.PreviewKeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.F4 or System.Windows.Input.Key.Down && RejectEmptyList()) e.Handled = true; };
        students.DropDownOpened += (_, _) => RejectEmptyList();
        students.SelectionChanged += (_, _) =>
        {
            if (rendering || students.SelectedItem is not string name || name == vm.Current?.Name) return;
            Attempt(() => vm.Select(name)); Render();
        };
        var menu = new ContextMenu();
        var separatorStyle = (Style)FindResource(typeof(Separator));
        menu.Items.Add(startupItem); menu.Items.Add(new Separator { Style = separatorStyle });
        startupItem.Click += (_, _) =>
        {
            if (startup is null) return;
            if (!startup.SetStartWithWindows(startupItem.IsChecked)) Messages.Enqueue("开机启动设置失败：" + startup.LastError);
            startupItem.IsChecked = startup.StartWithWindows;
        };
        MenuItem Item(string text, Action action)
        {
            var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); return item;
        }
        var settingsItem = Item("总体设置", Settings); var manageItem = Item("课程管理", ManageCourses);
        menu.Items.Add(new Separator { Style = separatorStyle }); Item("学生管理", ManageStudents); Item("更新学生信息", RefreshStudents);
        more.ContextMenu = menu;
        more.Click += (_, _) => { manageItem.IsEnabled = settingsItem.IsEnabled = vm.Current is not null; menu.PlacementTarget = more; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.Children.Add(students); Grid.SetColumn(weekLabel, 1); weekLabel.HorizontalAlignment = HorizontalAlignment.Center; toolbar.Children.Add(weekLabel);
        var today = Ui.Button("本周", () => { vm.GoToday(); Render(); }, "回到本周"); Grid.SetColumn(today, 2); toolbar.Children.Add(today); Grid.SetColumn(more, 3); toolbar.Children.Add(more);
        var body = new DockPanel(); DockPanel.SetDock(toolbar, Dock.Top); body.Children.Add(toolbar);
        DockPanel.SetDock(status, Dock.Top); body.Children.Add(status);
        var board = Ui.Card(table); board.Padding = new Thickness(0);
        // Border.CornerRadius does not clip its child; keep the grid inside the inner edge.
        table.SizeChanged += (_, e) => table.Clip = new RectangleGeometry(new Rect(e.NewSize), 11, 11);
        board.PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true; wheelDelta += e.Delta;
            if (Math.Abs(wheelDelta) < 120) return;
            if ((DateTime.UtcNow - lastWheel).TotalMilliseconds < 250) { wheelDelta = 0; return; }
            var direction = wheelDelta > 0 ? -1 : 1; wheelDelta = 0; lastWheel = DateTime.UtcNow; Move(direction);
        };
        body.Children.Add(board); Body(body, false); Render();
        dateTimer.Tick += CheckDate; dateTimer.Start();
        Activated += CheckDate;
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) RefreshDateHighlight(); };
        Closed += (_, _) => { dateTimer.Stop(); dateTimer.Tick -= CheckDate; Activated -= CheckDate; };
    }
    private void CheckDate(object? sender, EventArgs e) => RefreshDateHighlight();
    internal void RefreshDateHighlight()
    {
        var today = currentDate();
        if (highlightedDate == today) return;
        highlightedDate = today;
        foreach (var (cell, date, header) in datedCells)
            cell.Background = date == today ? (header ? todayHeaderBackground : todayBackground) : (header ? headerBackground : Brushes.White);
    }
    internal void ConfigureStartup(StartupSettings settings) { startup = settings; startupItem.IsChecked = settings.StartWithWindows; }
    public void RefreshStudents()
    {
        Attempt(() => { foreach (var error in vm.Refresh()) Messages.Enqueue(error); }); Render();
    }
    private void Move(int delta) { vm.Move(delta); Render(); }
    private void ManageStudents()
    {
        var dialog = new StudentManagementWindow(vm) { Owner = this };
        dialog.ShowDialog(); Render();
    }
    private void OpenWorkspace(WorkspacePage page, Guid? id = null, int? day = null, int period = 1)
    {
        if (vm.Current is null) { Messages.Enqueue("请先新增或选择学生。"); return; }
        StudentWorkspaceWindow dialog = page == WorkspacePage.Settings ? new GeneralSettingsWindow(vm) : new CourseManagementWindow(vm, page, id, day, period);
        dialog.Owner = this;
        dialog.ShowDialog(); Render();
    }
    private void Settings() => OpenWorkspace(WorkspacePage.Settings);
    private void Edit(Guid? id, int? day = null, int period = 1) => OpenWorkspace(WorkspacePage.Course, id, day, period);
    private void ManageCourses() => OpenWorkspace(WorkspacePage.Courses);
    private void Render()
    {
        rendering = true;
        if (students.ItemsSource is not IEnumerable<string> names || !names.SequenceEqual(vm.Names)) students.ItemsSource = vm.Names;
        students.SelectedItem = vm.Current?.Name;
        weekLabel.Text = vm.Title; status.Text = vm.TermStatus;
        status.Visibility = status.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        rendering = false;
        var key = (vm.Current?.Name, vm.Current?.Fingerprint, vm.Week);
        if (renderedTable == key) { RefreshDateHighlight(); return; }
        table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
        datedCells.Clear(); highlightedDate = null;
        if (vm.Document is null)
        {
            table.Children.Add(Ui.Column(Ui.Text("欢迎使用课程表", 22), Ui.Note("从设置齿轮菜单进入学生管理新增学生，然后通过总体设置调整学期与课程模式。"))); renderedTable = key; RefreshDateHighlight(); return;
        }
        var s = vm.Document.Settings;
        table.ColumnDefinitions.Add(new() { Width = new GridLength(78) });
        for (var col = 0; col < 7; col++) table.ColumnDefinitions.Add(new());
        table.RowDefinitions.Add(new() { Height = new GridLength(45) });
        for (var row = 0; row < s.ClassesPerDay; row++) table.RowDefinitions.Add(new());
        var days = s.SundayBelongsTo == "next" ? new[] { 7, 1, 2, 3, 4, 5, 6 } : [1, 2, 3, 4, 5, 6, 7];
        Put(Centered("时间 / 节次"), 0, 0, true);
        for (var col = 0; col < 7; col++)
        {
            var day = days[col]; var date = WeekCalendar.Date(s, vm.Week, day);
            var header = Put(Centered($"周{"一二三四五六日"[day - 1]}\n{date:MM/dd}"), 0, col + 1, true);
            datedCells.Add((header, date, true));
            for (var row = 1; row <= s.ClassesPerDay; row++)
            {
                var period = row;
                var cell = Put(new Grid { Background = Brushes.Transparent }, row, col + 1);
                datedCells.Add((cell, date, false));
                cell.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { Edit(null, day, period); e.Handled = true; } };
            }
        }
        foreach (var p in s.ClassPeriods)
        {
            var periodLabel = Centered($"第 {p.Period} 节\n{p.Start}–{p.End}"); periodLabel.FontSize = 11;
            Put(periodLabel, p.Period, 0, true);
        }
        foreach (var course in vm.Document.Courses)
            foreach (var slot in course.Schedules.Where(slot => slot.Weeks.Contains(vm.Week)))
            {
                var label = Centered(course.Name + (slot.Location.Length > 0 ? "\n" + slot.Location : ""));
                label.FontWeight = FontWeights.SemiBold; label.TextTrimming = TextTrimming.CharacterEllipsis;
                var block = new Border { Background = (Brush)new BrushConverter().ConvertFromString(course.Color)!, CornerRadius = new CornerRadius(10), Margin = new Thickness(4), Padding = new Thickness(5), Child = label, ClipToBounds = true, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = course.Name + "\n" + slot.Location };
                Grid.SetColumn(block, Array.IndexOf(days, slot.DayOfWeek) + 1); Grid.SetRow(block, slot.StartPeriod); Grid.SetRowSpan(block, s.ClassesUnit == "two" ? 2 : 1);
                block.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { Edit(course.Id); e.Handled = true; } }; table.Children.Add(block);
            }
        renderedTable = key;
        RefreshDateHighlight();
    }
    private static TextBlock Centered(string text) => new() { Text = text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), FontSize = 12 };
    private Border Put(UIElement element, int row, int col, bool shaded = false)
    {
        var border = new Border { Child = element, BorderBrush = new SolidColorBrush(Color.FromRgb(222, 232, 246)), BorderThickness = new Thickness(0, 0, 1, 1), Background = shaded ? headerBackground : Brushes.White };
        Grid.SetRow(border, row); Grid.SetColumn(border, col); table.Children.Add(border); return border;
    }
}
