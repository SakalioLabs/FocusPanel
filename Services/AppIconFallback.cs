using System.Globalization;
using System.Text;

namespace FocusPanel.Services;

public static class AppIconFallback
{
    public static string GetText(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "A";

        TextElementEnumerator elements =
            StringInfo.GetTextElementEnumerator(displayName.Trim());
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (!ContainsLetterOrDigit(element))
                continue;

            return element.ToUpper(CultureInfo.CurrentUICulture);
        }

        return "A";
    }

    private static bool ContainsLetterOrDigit(string text)
    {
        foreach (Rune character in text.EnumerateRunes())
        {
            UnicodeCategory category =
                Rune.GetUnicodeCategory(character);
            if (category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber)
            {
                return true;
            }
        }

        return false;
    }
}
