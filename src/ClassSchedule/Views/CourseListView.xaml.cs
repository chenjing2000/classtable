using System.Windows.Controls;
using ClassSchedule.Core;
namespace ClassSchedule.Views;
public partial class CourseListView : UserControl
{
    public CourseListView(StudentDocument document, Action<Guid?> edit, Guid? selected)
    {
        InitializeComponent(); Table.Load(document); Table.SelectCourse(selected); Table.Activated += edit;
    }
    public Guid? SelectedCourseId => Table.SelectedCourseId;
}
