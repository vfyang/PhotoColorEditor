using System.Globalization;

namespace PhotoColorEditor;

internal static class AppLanguage
{
    public const string System = "system";
    public const string Simplified = "zh-Hans";
    public const string Traditional = "zh-Hant";
    public const string English = "en";

    public static string Normalize(string? language) => language is Simplified or Traditional or English ? language : System;

    public static string Resolve(string? preference, CultureInfo? displayCulture = null)
    {
        string selected = Normalize(preference);
        if (selected != System) return selected;
        string name = (displayCulture ?? CultureInfo.CurrentUICulture).Name;
        if (name != "zh" && !name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)) return English;
        if (name.Contains("-Hans", StringComparison.OrdinalIgnoreCase)) return Simplified;
        return name.Contains("-Hant", StringComparison.OrdinalIgnoreCase) || name is "zh-TW" or "zh-HK" or "zh-MO" or "zh-CHT"
            ? Traditional : Simplified;
    }
}
