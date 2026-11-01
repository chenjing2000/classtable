using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public sealed class GroupedCourseTable : UserControl
{
    private readonly Grid rows = CreateGrid();
    private readonly ScrollViewer scroll;
    private readonly List<Border> cells = [];
    private readonly Dictionary<Guid, Border> selections = [];
    private List<Guid> courseOrder = [];
    private static readonly Brush Line = new SolidColorBrush(Color.FromRgb(220, 230, 244));
    public Guid? SelectedCourseId { get; private set; }
    public event Action<Guid?>? Activated;

    public GroupedCourseTable()
    {
        Focusable = true; rows.Name = "CourseGroups"; rows.Background = Brushes.White;
        var header = CreateGrid(); header.Name = "CourseHeader"; header.Height = 34;
        header.Margin = new Thickness(0, 0, 11, 0);
        var headings = new[] { "课程名", "周次", "时间", "地点" };
        for (var i = 0; i < headings.Length; i++)
        {
            var cell = Cell(headings[i], new SolidColorBrush(Color.FromRgb(238, 245, 255))); cell.Tag = null;
            ((TextBlock)cell.Child).FontWeight = FontWeights.SemiBold; Grid.SetColumn(cell, i); header.Children.Add(cell);
        }
        scroll = new ScrollViewer { Content = rows, Background = Brushes.White, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var layout = new DockPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header); layout.Children.Add(scroll);
        Content = new Border { Child = layout, BorderBrush = Line, BorderThickness = new Thickness(1), Margin = new Thickness(4, 8, 4, 8) };
        System.Windows.Automation.AutomationProperties.SetName(this, "课程分组表");
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!InBody(e.OriginalSource as DependencyObject)) return;
            Focus(); Select(HitCourse(e.OriginalSource as DependencyObject));
        };
        MouseDoubleClick += (_, e) =>
        {
            if (!InBody(e.OriginalSource as DependencyObject)) return;
            var id = HitCourse(e.OriginalSource as DependencyObject); Select(id); Activated?.Invoke(id); e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && SelectedCourseId is not null) { Activated?.Invoke(SelectedCourseId); e.Handled = true; }
            else if (e.Key is Key.Up or Key.Down && courseOrder.Count > 0)
            {
                var index = SelectedCourseId is Guid id ? courseOrder.IndexOf(id) : -1;
                index = Math.Clamp(index + (e.Key == Key.Up ? -1 : 1), 0, courseOrder.Count - 1);
                Select(courseOrder[index]); cells.First(b => Equals(b.Tag, SelectedCourseId)).BringIntoView(); e.Handled = true;
            }
        };
    }

    public void Load(StudentDocument? document)
    {
        rows.Children.Clear(); rows.RowDefinitions.Clear(); cells.Clear(); selections.Clear();
        courseOrder = document?.Courses.Select(c => c.Id).ToList() ?? [];
        var groupIndex = 0;
        if (document is not null)
            foreach (var course in document.Courses)
            {
                var background = groupIndex++ % 2 == 0 ? Brushes.White : (Brush)FindResource("AlternatingCourseBackground");
                var entries = SchedulePresentation.Rows(course, document.Settings); var first = rows.RowDefinitions.Count;
                for (var i = 0; i < entries.Count; i++) rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 36 });
                var color = (Color)ColorConverter.ConvertFromString(course.Color);
                var name = Cell(course.Name, new SolidColorBrush(color), true); name.CornerRadius = new CornerRadius(9); name.BorderThickness = new Thickness(0);
                Add(name, first, 0, course.Id); Grid.SetRowSpan(name, entries.Count);
                for (var i = 0; i < entries.Count; i++)
                {
                    Add(Cell(entries[i].Weeks, background), first + i, 1, course.Id);
                    Add(Cell(entries[i].Time, background), first + i, 2, course.Id);
                    Add(Cell(entries[i].Location, background), first + i, 3, course.Id);
                }
                var overlay = new Border { Name = "GroupSelection", IsHitTestVisible = false, Visibility = Visibility.Collapsed, CornerRadius = new CornerRadius(4), Margin = new Thickness(2),
                    Background = (Brush)FindResource("SelectionOverlay"), Effect = (System.Windows.Media.Effects.Effect)FindResource("SelectionGlow") };
                Grid.SetRow(overlay, first); Grid.SetRowSpan(overlay, entries.Count); Grid.SetColumnSpan(overlay, 4); Panel.SetZIndex(overlay, 1);
                rows.Children.Add(overlay); selections[course.Id] = overlay;
            }
        Select(SelectedCourseId is Guid selected && courseOrder.Contains(selected) ? selected : null);
    }

    private bool InBody(DependencyObject? source) => Ui.Ancestor<ScrollBar>(source) is null && Ui.Ancestor<ScrollViewer>(source) == scroll;
    private static Guid? HitCourse(DependencyObject? source) => Ui.Ancestor<Border>(source)?.Tag is Guid id ? id : null;
    public void SelectCourse(Guid? id) => Select(courseOrder.Contains(id ?? Guid.Empty) ? id : null);
    private void Select(Guid? id)
    {
        SelectedCourseId = id;
        foreach (var (courseId, overlay) in selections) overlay.Visibility = courseId == id ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Add(Border cell, int row, int column, Guid id)
    {
        cell.Tag = id; cells.Add(cell);
        Grid.SetRow(cell, row); Grid.SetColumn(cell, column); rows.Children.Add(cell);
    }
    private static Grid CreateGrid()
    {
        var grid = new Grid();
        foreach (var width in new[] { 120, 110, 70 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }
    private static Border Cell(string value, Brush background, bool courseName = false)
    {
        Border cell = courseName ? new CourseNameCell() : new Border();
        cell.Background = background; cell.BorderBrush = Line; cell.BorderThickness = new Thickness(0, 0, 1, 1); cell.Padding = new Thickness(6); cell.ToolTip = value;
        cell.Child = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
        return cell;
    }
}
