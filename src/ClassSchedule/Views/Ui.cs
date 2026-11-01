using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Input;
using ClassSchedule.ViewModels;

namespace ClassSchedule.Views;

internal static class Ui
{
    public static T? Ancestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T found) return found;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
    public static TextBlock Text(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Label(string text) => StyledText(text, "ParameterLabel");
    public static TextBlock Note(string text) => StyledText(text, "CommentLabel");
    private static TextBlock StyledText(string text, string style)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(FrameworkElement.StyleProperty, style); return block;
    }
    public static Button Button(string text, Action action, string? tip = null)
    {
        var button = new Button { Content = text, ToolTip = tip ?? text };
        System.Windows.Automation.AutomationProperties.SetName(button, tip ?? text);
        button.Click += (_, _) => action(); return button;
    }
    public static TextBox Input(string property, double width = double.NaN)
    {
        var text = new TextBox { MinWidth = 90, Width = width };
        text.SetBinding(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return text;
    }
    public static TextBox NumberInput(string property, int maxDigits, Action<string> rejected)
    {
        var box = RestrictedInput(property, text => NumericText.IsValid(text, maxDigits), $"只接受最多{maxDigits}位数字（0–9）。", rejected, maxDigits);
        box.Width = 100; box.HorizontalAlignment = HorizontalAlignment.Left; return box;
    }
    public static TextBox RestrictedInput(string property, Func<string, bool> valid, string message, Action<string> rejected, int maxLength = 0)
    {
        var box = Input(property); box.MaxLength = maxLength;
        InputMethod.SetIsInputMethodEnabled(box, false); box.AllowDrop = false;
        string Proposed(string value) => box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, value);
        void Reject() => rejected(message);
        box.PreviewTextInput += (_, e) => { if (!valid(Proposed(e.Text))) { e.Handled = true; Reject(); } };
        box.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space && !valid(Proposed(" "))) { e.Handled = true; Reject(); } };
        box.PreviewDrop += (_, e) => { e.Handled = true; Reject(); };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string value || !valid(Proposed(value))) { e.CancelCommand(); Reject(); }
        });
        var previous = ""; var restoring = false;
        box.TextChanged += (_, _) =>
        {
            if (restoring) return;
            if (valid(box.Text)) { previous = box.Text; return; }
            restoring = true; box.Text = previous; box.CaretIndex = box.Text.Length; restoring = false; Reject();
        };
        return box;
    }
    public static ComboBox Select(string property, IEnumerable<string> options)
    {
        var box = new ComboBox { ItemsSource = options, MinWidth = 110 };
        box.SetBinding(ComboBox.SelectedItemProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return box;
    }
    public static StackPanel Choices(string property, params string[] options)
    {
        var row = Row(); var group = Guid.NewGuid().ToString();
        foreach (var option in options)
        {
            var radio = new RadioButton { Content = option, GroupName = group, Width = 78, Margin = new Thickness(0, 10, 0, 10), VerticalContentAlignment = VerticalAlignment.Center };
            radio.SetBinding(RadioButton.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay, Converter = new ChoiceConverter(), ConverterParameter = option }); row.Children.Add(radio);
        }
        return row;
    }
    public static StackPanel Row(params UIElement[] items)
    { var panel = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var item in items) panel.Children.Add(item); return panel; }
    public static StackPanel Column(params UIElement[] items)
    { var panel = new StackPanel(); foreach (var item in items) panel.Children.Add(item); return panel; }
    public static Border Card(UIElement child) => new() { Child = child, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(220, 230, 244)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), Margin = new Thickness(3, 5, 3, 5) };
    public static Grid Form(params (string Label, FrameworkElement Control)[] rows) => Form(135, rows);
    public static Grid Form(double labelWidth, params (string Label, FrameworkElement Control)[] rows)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(labelWidth) }); grid.ColumnDefinitions.Add(new());
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var label = Label(rows[i].Label); Grid.SetRow(label, i); grid.Children.Add(label);
            Grid.SetRow(rows[i].Control, i); Grid.SetColumn(rows[i].Control, 1); grid.Children.Add(rows[i].Control);
        }
        return grid;
    }
    public static void TextColumn(DataGrid table, string title, string path, double weight = 1, bool readOnly = true)
    {
        var display = new Style(typeof(TextBlock)); display.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center)); display.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)); display.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        var editing = new Style(typeof(TextBox), (Style)Application.Current.FindResource(typeof(TextBox))); editing.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center)); editing.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0))); editing.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0.0)); editing.Setters.Add(new Setter(FrameworkElement.HeightProperty, double.NaN)); editing.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2))); editing.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        table.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(weight, DataGridLengthUnitType.Star), IsReadOnly = readOnly, ElementStyle = display, EditingElementStyle = editing });
    }
}

internal sealed class ChoiceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value, parameter);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? parameter : Binding.DoNothing;
}
