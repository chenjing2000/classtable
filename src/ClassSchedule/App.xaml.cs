using System.Windows;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

namespace ClassSchedule;

public partial class App : Application
{
    private SingleInstance? instance;
    private ReminderService? reminders;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new SingleInstance();
        if (!instance.IsPrimary) { instance.ActivateExisting(); Shutdown(); return; }
        base.OnStartup(e);
        var workspace = new WorkspaceViewModel(new StudentFileStore(System.IO.Path.Combine(AppContext.BaseDirectory, "data")));
        var window = new MainWindow(workspace);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        instance.Attach(window);
        window.Show();
        reminders = new ReminderService(workspace, window, window.Messages.Enqueue);
        window.RefreshStudents();
    }
    protected override void OnExit(ExitEventArgs e) { reminders?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
