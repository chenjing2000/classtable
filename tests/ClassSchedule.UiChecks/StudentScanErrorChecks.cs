using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClassSchedule.Core;
using ClassSchedule.ViewModels;
using ClassSchedule.Views;

internal static class StudentScanErrorChecks
{
    public static int Run(Application app)
    {
        var root = Path.Combine(Path.GetTempPath(), "student-scan-errors-" + Guid.NewGuid()); var checks = 0;
        void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        try
        {
            foreach (var operation in new[] { "新增", "更新", "删除" })
            {
                var directory = Path.Combine(root, operation);
                var workspace = new WorkspaceViewModel(new StudentFileStore(directory));
                workspace.CreateStudent("损坏", new DateOnly(2026, 11, 2));
                workspace.CreateStudent("正常", new DateOnly(2026, 11, 2));
                var window = new StudentManagementWindow(workspace); window.Show(); Flush();
                Program.Descendants(window).OfType<ListBox>().Single(b => b.Name == "StudentList").SelectedItem = "正常";
                var damaged = Path.Combine(directory, "损坏.json"); File.WriteAllText(damaged, "invalid-json");
                if (operation != "删除") Program.Descendants(window).OfType<TextBox>().Single(b => b.Name == "StudentName").Text = "修改后";
                void Click(string label) => Program.Descendants(window).OfType<Button>().Single(b => b.IsVisible && b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Click(operation); if (operation == "删除") Click("是"); Flush();
                Check(window.Messages.Current.Contains("损坏.json"), operation + " reports unrelated corrupt student in its own bottom information bar");
                Check(operation == "删除" ? !File.Exists(Path.Combine(directory, "正常.json")) : File.Exists(Path.Combine(directory, "修改后.json")), operation + " keeps successful operation committed");
                Check(window.IsVisible && !window.Model.IsDirty && !workspace.Names.Contains("损坏"), operation + " publishes the refreshed list without a false save failure");
                Check(File.ReadAllText(damaged) == "invalid-json", operation + " leaves corrupt source untouched");
                window.Close();
            }
            Console.WriteLine($"STUDENT SCAN ERROR CHECKS PASSED: {checks}"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
