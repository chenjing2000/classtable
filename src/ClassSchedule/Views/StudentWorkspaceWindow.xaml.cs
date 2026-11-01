using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

public partial class StudentWorkspaceWindow : DialogWindow
{
    public StudentWorkspaceViewModel State { get; }
    private object? renderedDraft;
    private WorkspacePage? renderedPage;
    private bool allowClose;
    protected StudentWorkspaceWindow(WorkspaceViewModel workspace, WorkspacePage page = WorkspacePage.Courses, Guid? id = null, int? day = null, int period = 1)
        : base($"{(page == WorkspacePage.Settings ? "总体设置" : "课程管理")}（{workspace.Current?.Name}）", page == WorkspacePage.Settings ? 600 : 520, page == WorkspacePage.Settings ? 600 : 540, false)
    {
        InitializeComponent();
        PreviewMouseWheel += (_, e) => { if (PageContent.Content is SettingsView settings) settings.HandlePageWheel(e); };
        var surface = (UIElement)Content; Content = Layout; Body(surface, false);
        MinWidth = 480; MinHeight = page == WorkspacePage.Settings ? 480 : 420; ShowInTaskbar = false;
        Footer.Orientation = Orientation.Vertical; Footer.HorizontalAlignment = HorizontalAlignment.Stretch;
        State = new(workspace);
        if (page == WorkspacePage.Settings) State.ShowSettings();
        else if (page == WorkspacePage.Course) State.Edit(id, day, period, workspace.Week);
        State.PropertyChanged += PageStateChanged;
        State.CloseRequested += CloseRequested;
        Closing += (_, e) =>
        {
            if (allowClose) return;
            CommitCells();
            if (State.Confirmation != WorkspaceConfirmation.None) { e.Cancel = true; State.CancelConfirmation(); }
            else if (State.IsDirty) { e.Cancel = true; Run(State.RequestClose); }
        };
        Closed += (_, _) => { State.PropertyChanged -= PageStateChanged; State.CloseRequested -= CloseRequested; };
        KeyDown += (_, e) =>
        {
            if (e.Handled || e.Key != Key.Escape) return;
            if (State.Confirmation != WorkspaceConfirmation.None) State.CancelConfirmation();
            else Run(State.RequestClose);
            e.Handled = true;
        };
        Render();
    }
    private void CloseRequested() { allowClose = true; Close(); }
    private void PageStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Render();
    private void CommitCells() { if (PageContent.Content is SettingsView view) view.CommitCells(); }
    private void Run(Action action) { CommitCells(); Attempt(action); }
    private void Back_Click(object sender, RoutedEventArgs e) => Run(State.ShowCourses);
    private void Render()
    {
        var draft = (object?)State.Course ?? State.Settings ?? (object)State.Document;
        if (!ReferenceEquals(renderedDraft, draft) || renderedPage != State.Page)
        {
            renderedDraft = draft; renderedPage = State.Page; Footer.Children.Clear();
            BackButton.Visibility = State.Page == WorkspacePage.Course ? Visibility.Visible : Visibility.Collapsed;
            var actions = Ui.Row(); actions.HorizontalAlignment = HorizontalAlignment.Right;
            var bar = new DockPanel(); DockPanel.SetDock(actions, Dock.Right); bar.Children.Add(actions); Footer.Children.Add(bar);
            if (State.Page == WorkspacePage.Courses)
            {
                Heading.Text = "全部课程";
                var list = new CourseListView(State.Document, id => Run(() => State.Edit(id)), State.SelectedCourseId); PageContent.Content = list;
                actions.Children.Add(Ui.Button("新建", () => Run(() => State.Edit(null))));
                actions.Children.Add(Ui.Button("删除", () =>
                {
                    if (list.SelectedCourseId is Guid id) State.RequestDelete(id); else Messages.Enqueue("请先选择一门课程。");
                }));
                actions.Children.Add(Ui.Button("确定", () => Run(State.RequestClose)));
            }
            else if (State.Page == WorkspacePage.Course)
            {
                Heading.Text = State.Course!.IsNew ? "新建课程" : "编辑课程";
                PageContent.Content = new CourseEditorView(State.Course);
                if (!State.Course.IsNew) { var delete = Ui.Button("删除课程", () => State.RequestDelete(State.Course.Course.Id)); delete.HorizontalAlignment = HorizontalAlignment.Left; bar.Children.Add(delete); }
                actions.Children.Add(Ui.Button("取消", () => Run(State.ShowCourses)));
                var save = Ui.Button("保存", () => Run(State.Save)); save.IsDefault = true; actions.Children.Add(save);
            }
            else
            {
                Heading.Text = "总体设置";
                PageContent.Content = new SettingsView(State.Settings!, Messages.Enqueue);
                var defaults = Ui.Button("恢复默认课时", () => Run(State.RequestDefaults)); defaults.HorizontalAlignment = HorizontalAlignment.Left; bar.Children.Add(defaults);
                actions.Children.Add(Ui.Button("取消修改", State.CancelSettings));
                var save = Ui.Button("保存", () => Run(State.Save)); save.IsDefault = true; actions.Children.Add(save);
            }
        }
        var enabled = State.Confirmation == WorkspaceConfirmation.None;
        PageContent.IsEnabled = enabled; PageHeading.IsEnabled = enabled; Footer.IsEnabled = enabled;
        ClearConfirmation();
        if (State.Confirmation == WorkspaceConfirmation.Leave)
            SetConfirmation("有未保存的修改，如何处理？", ("保存", () => Run(State.Save)), ("放弃修改", State.Discard), ("继续编辑", State.CancelConfirmation));
        else if (State.Confirmation == WorkspaceConfirmation.Dates)
            SetConfirmation("修改学期起点会改变所有课程的实际日期。是否保存？", ("保存新日期", () => Run(State.Confirm)), ("继续编辑", State.CancelConfirmation));
        else if (State.Confirmation == WorkspaceConfirmation.Delete)
            SetConfirmation("是否删除这门课程及全部上课时段？", ("是", () => Run(State.Confirm)), ("否", State.CancelConfirmation));
        else if (State.Confirmation == WorkspaceConfirmation.ResetTimes)
            SetConfirmation("仅恢复课时参数和实际课时，是否继续？", ("恢复默认", () => Run(State.Confirm)), ("取消", State.CancelConfirmation));
    }
}
