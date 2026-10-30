namespace ClassTimetable.ViewModels;

public static class NumericText
{
    // Empty is a useful editing state; the save validator rejects missing numbers.
    public static bool IsValid(string text, int maxDigits) => text.Length <= maxDigits && text.All(c => c is >= '0' and <= '9');
}
