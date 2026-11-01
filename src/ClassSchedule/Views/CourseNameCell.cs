using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClassSchedule.Views;

// Draw only a narrow rim; the name and most of the course color remain untouched.
internal sealed class CourseNameCell : Border
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Background is not SolidColorBrush fill) return;
        const double rim = 3.5;
        for (var step = 0; step <= 7; step++)
        {
            var inset = step * .5;
            var bounds = new Rect(inset, inset, Math.Max(0, ActualWidth - inset * 2), Math.Max(0, ActualHeight - inset * 2));
            var amount = inset / rim;
            var color = Color.FromRgb((byte)Math.Round(255 + (fill.Color.R - 255) * amount), (byte)Math.Round(255 + (fill.Color.G - 255) * amount), (byte)Math.Round(255 + (fill.Color.B - 255) * amount));
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, bounds, Math.Max(0, CornerRadius.TopLeft - inset), Math.Max(0, CornerRadius.TopLeft - inset));
        }
    }
}
