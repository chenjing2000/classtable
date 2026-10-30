using System.Windows;
using ClassTimetable.Core;
using ClassTimetable.ViewModels;
using ClassTimetable.Views;

namespace ClassTimetable;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\ClassTimetable.Desktop.v1", out var first);
        var window = new MainWindow(new WorkspaceViewModel(new StudentFileStore(System.IO.Path.Combine(AppContext.BaseDirectory, "data"))));
        MainWindow = window;
        window.Show();
        if (!first)
        {
            window.IsEnabled = false;
            window.Messages.Enqueue("课程表已在运行，请返回已打开的窗口。本窗口将在5秒后关闭。");
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); Shutdown(); }; timer.Start();
        }
        else window.RefreshStudents();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
