namespace BonakMarket.Core.Identity;

public static class NationalCodeValidator
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return new string(value.Trim().Select(c => c switch
        {
            >= '\u06F0' and <= '\u06F9' => (char)('0' + c - '\u06F0'),
            >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'),
            _ => c
        }).ToArray());
    }

    public static bool IsValid(string value)
    {
        value = Normalize(value);
        if (value == null) return true;
        if (value.Length != 10 || !value.All(char.IsAsciiDigit) || value.All(c => c == value[0])) return false;
        var remainder = Enumerable.Range(0, 9).Sum(i => (value[i] - '0') * (10 - i)) % 11;
        return value[9] - '0' == (remainder < 2 ? remainder : 11 - remainder);
    }
}
