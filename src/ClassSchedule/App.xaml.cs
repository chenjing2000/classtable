using System.Windows;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

namespace ClassSchedule;

public partial class App : Application
{
    private SingleInstance? instance;
    private ReminderService? reminders;
    private TrayService? tray;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new SingleInstance();
        if (!instance.IsPrimary) { instance.ActivateExisting(); Shutdown(); return; }
        base.OnStartup(e);
        var workspace = new WorkspaceViewModel(new StudentFileStore(System.IO.Path.Combine(AppContext.BaseDirectory, "data")));
        var window = new MainWindow(workspace);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
#if DEBUG
        var settings = new StartupSettings(applyStartup: _ => { });
#else
        var settings = new StartupSettings();
#endif
        var loaded = settings.Load();
        window.ConfigureStartup(settings);
        ExitCoordinator? exit = null;
        tray = new TrayService(window, () => exit!.Request());
        exit = new ExitCoordinator(window, tray.Restore, () => { tray.Dispose(); Shutdown(); });
        instance.Attach(window, tray.Restore);
        tray.ShowInitialWindow(e.Args.Contains("--startup"));
        // Initialize reminders after background startup has hidden the owner, so a due alert is not hidden with it.
        window.Dispatcher.BeginInvoke(() =>
        {
            if (Dispatcher.HasShutdownStarted) return;
            reminders = new ReminderService(workspace, window, window.Messages.Enqueue);
            window.RefreshStudents();
            if (!loaded) window.Messages.Enqueue("开机启动设置失败：" + settings.LastError);
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }
    protected override void OnExit(ExitEventArgs e) { tray?.Dispose(); reminders?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
