using ClassSchedule.ViewModels;
namespace ClassSchedule.Views;
public sealed class CourseManagementWindow(WorkspaceViewModel workspace, WorkspacePage page = WorkspacePage.Courses, Guid? id = null, int? day = null, int period = 1)
    : StudentWorkspaceWindow(workspace, page, id, day, period);
