using ClassSchedule.ViewModels;
namespace ClassSchedule.Views;
public sealed class GeneralSettingsWindow(WorkspaceViewModel workspace)
    : StudentWorkspaceWindow(workspace, WorkspacePage.Settings);
