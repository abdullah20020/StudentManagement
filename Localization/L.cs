using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace StudentManagement.Localization
{
    /// <summary>
    /// UI text in English and Arabic. English text is the key; the Arabic translation lives in
    /// Localization/ar.json. Anything without a translation falls back to English.
    ///
    /// Use it as <c>T("Students")</c> or, with placeholders, <c>T("Grade {0} recorded.", letter)</c>.
    /// The language comes from the request's UI culture (set by the language switcher).
    /// </summary>
    public static class L
    {
        /// <summary>The university's name: change it here (and its translation in ar.json).</summary>
        public const string UniversityName = "Emerald University";

        public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        public static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");

        private static readonly Dictionary<string, string> ArabicText = Load();

        /// <summary>English keys looked up in Arabic with no translation (shown on /Language/Missing in development).</summary>
        public static readonly ConcurrentDictionary<string, byte> Missing = new();

        public static bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

        public static string Dir => IsArabic ? "rtl" : "ltr";

        public static string Lang => IsArabic ? "ar" : "en";

        /// <summary>A "go forward" arrow that points the right way for the reading direction.</summary>
        public static string Arrow => IsArabic ? "←" : "→";

        public static string T(string key)
        {
            if (!IsArabic || string.IsNullOrEmpty(key)) return key;
            if (ArabicText.TryGetValue(key, out var ar)) return ar;
            Missing.TryAdd(key, 0);
            return key;
        }

        public static string T(string key, params object?[] args) =>
            string.Format(CultureInfo.CurrentCulture, T(key), args);

        public static string Day(DayOfWeek day) => T(day.ToString());

        public static string DayShort(DayOfWeek day) => T(day.ToString()[..3]);

        /// <summary>A date with month names in the current language (digits stay Western).</summary>
        public static string Date(DateTime date, string format = "dd MMM yyyy") =>
            date.ToString(format, IsArabic ? Arabic : English);

        private static Dictionary<string, string> Load()
        {
            using var stream = typeof(L).Assembly.GetManifestResourceStream("StudentManagement.Localization.ar.json");
            if (stream == null) return new();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }
    }
}
