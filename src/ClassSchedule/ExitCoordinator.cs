using System.Windows;
using ClassSchedule.Views;

namespace ClassSchedule;

internal sealed class ExitCoordinator(Window main, Action restore, Action shutdown)
{
    public bool IsPending { get; private set; }
    public void Request()
    {
        if (IsPending) { restore(); return; }
        IsPending = true; restore(); Continue();
    }
    private void Continue()
    {
        var editor = OwnedEditors(main).LastOrDefault();
        if (editor is null) { shutdown(); return; }
        editor.Activate();
        editor.RequestApplicationExit(Continue);
    }
    private static IEnumerable<DialogWindow> OwnedEditors(Window owner)
    {
        foreach (Window child in owner.OwnedWindows)
        {
            if (child is DialogWindow dialog && child is not ReminderWindow) yield return dialog;
            foreach (var nested in OwnedEditors(child)) yield return nested;
        }
    }
}
