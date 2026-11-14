

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Essentieables;

public static class CountryResolver
{
    /// <summary>
    /// Maps BCP-47 locale codes (e.g. "en-US") to their associated country/region display name.
    /// </summary>
    private static readonly Dictionary<string, string> localeToCountry = new(StringComparer.OrdinalIgnoreCase)
{
    { "af-ZA", "South Africa" },
    { "am-ET", "Ethiopia" },
    { "ar-AE", "United Arab Emirates" },
    { "ar-BH", "Bahrain" },
    { "ar-DZ", "Algeria" },
    { "ar-EG", "Egypt" },
    { "ar-IQ", "Iraq" },
    { "ar-JO", "Jordan" },
    { "ar-KW", "Kuwait" },
    { "ar-LB", "Lebanon" },
    { "ar-LY", "Libya" },
    { "ar-MA", "Morocco" },
    { "ar-OM", "Oman" },
    { "ar-QA", "Qatar" },
    { "ar-SA", "Saudi Arabia" },
    { "ar-SD", "Sudan" },
    { "ar-SY", "Syria" },
    { "ar-TN", "Tunisia" },
    { "ar-YE", "Yemen" },
    { "az-AZ", "Azerbaijan" },
    { "be-BY", "Belarus" },
    { "bg-BG", "Bulgaria" },
    { "bn-BD", "Bangladesh" },
    { "bn-IN", "India" },
    { "bs-BA", "Bosnia and Herzegovina" },
    { "ca-ES", "Spain" },
    { "cs-CZ", "Czech Republic" },
    { "cy-GB", "United Kingdom" },
    { "da-DK", "Denmark" },
    { "de-AT", "Austria" },
    { "de-CH", "Switzerland" },
    { "de-DE", "Germany" },
    { "de-LI", "Liechtenstein" },
    { "de-LU", "Luxembourg" },
    { "el-CY", "Cyprus" },
    { "el-GR", "Greece" },
    { "en-AU", "Australia" },
    { "en-BZ", "Belize" },
    { "en-CA", "Canada" },
    { "en-CB", "Caribbean" },
    { "en-GB", "United Kingdom" },
    { "en-IE", "Ireland" },
    { "en-IN", "India" },
    { "en-JM", "Jamaica" },
    { "en-NZ", "New Zealand" },
    { "en-PH", "Philippines" },
    { "en-TT", "Trinidad and Tobago" },
    { "es-US", "United States" },
    { "en-US", "United States" },
    { "en-ZA", "South Africa" },
    { "en-ZW", "Zimbabwe" },
    { "es-AR", "Argentina" },
    { "es-BO", "Bolivia" },
    { "es-CL", "Chile" },
    { "es-CO", "Colombia" },
    { "es-CR", "Costa Rica" },
    { "es-DO", "Dominican Republic" },
    { "es-EC", "Ecuador" },
    { "es-ES", "Spain" },
    { "es-GT", "Guatemala" },
    { "es-HN", "Honduras" },
    { "es-MX", "Mexico" },
    { "es-NI", "Nicaragua" },
    { "es-PA", "Panama" },
    { "es-PE", "Peru" },
    { "es-PR", "Puerto Rico" },
    { "es-PY", "Paraguay" },
    { "es-SV", "El Salvador" },
    { "es-UY", "Uruguay" },
    { "es-VE", "Venezuela" },
    { "et-EE", "Estonia" },
    { "eu-ES", "Spain" },
    { "fa-IR", "Iran" },
    { "fi-FI", "Finland" },
    { "fo-FO", "Faroe Islands" },
    { "fr-BE", "Belgium" },
    { "fr-CA", "Canada" },
    { "fr-CH", "Switzerland" },
    { "fr-FR", "France" },
    { "fr-LU", "Luxembourg" },
    { "fr-MC", "Monaco" },
    { "gl-ES", "Spain" },
    { "gu-IN", "India" },
    { "he-IL", "Israel" },
    { "hi-IN", "India" },
    { "hr-BA", "Bosnia and Herzegovina" },
    { "hr-HR", "Croatia" },
    { "hu-HU", "Hungary" },
    { "hy-AM", "Armenia" },
    { "id-ID", "Indonesia" },
    { "is-IS", "Iceland" },
    { "it-CH", "Switzerland" },
    { "it-IT", "Italy" },
    { "ja-JP", "Japan" },
    { "ka-GE", "Georgia" },
    { "kk-KZ", "Kazakhstan" },
    { "kn-IN", "India" },
    { "ko-KR", "South Korea" },
    { "kok-IN", "India" },
    { "ky-KG", "Kyrgyzstan" },
    { "lt-LT", "Lithuania" },
    { "lv-LV", "Latvia" },
    { "mi-NZ", "New Zealand" },
    { "mk-MK", "North Macedonia" },
    { "ml-IN", "India" },
    { "mn-MN", "Mongolia" },
    { "mr-IN", "India" },
    { "ms-BN", "Brunei Darussalam" },
    { "ms-MY", "Malaysia" },
    { "mt-MT", "Malta" },
    { "nb-NO", "Norway" },
    { "nl-BE", "Belgium" },
    { "nl-NL", "Netherlands" },
    { "nn-NO", "Norway" },
    { "pa-IN", "India" },
    { "pl-PL", "Poland" },
    { "pt-BR", "Brazil" },
    { "pt-PT", "Portugal" },
    { "ro-RO", "Romania" },
    { "ru-RU", "Russia" },
    { "sa-IN", "India" },
    { "sk-SK", "Slovakia" },
    { "sl-SI", "Slovenia" },
    { "sq-AL", "Albania" },
    { "sr-Cyrl-BA", "Bosnia and Herzegovina" },
    { "sr-Cyrl-CS", "Serbia and Montenegro" },
    { "sr-Cyrl-ME", "Montenegro" },
    { "sr-Cyrl-RS", "Serbia" },
    { "sr-Latn-BA", "Bosnia and Herzegovina" },
    { "sr-Latn-CS", "Serbia and Montenegro" },
    { "sr-Latn-ME", "Montenegro" },
    { "sr-Latn-RS", "Serbia" },
    { "sv-FI", "Finland" },
    { "sv-SE", "Sweden" },
    { "sw-KE", "Kenya" },
    { "syr-SY", "Syria" },
    { "ta-IN", "India" },
    { "te-IN", "India" },
    { "th-TH", "Thailand" },
    { "tr-TR", "Turkey" },
    { "tt-RU", "Russia" },
    { "uk-UA", "Ukraine" },
    { "ur-PK", "Pakistan" },
    { "uz-Cyrl-UZ", "Uzbekistan" },
    { "uz-Latn-UZ", "Uzbekistan" },
    { "vi-VN", "Vietnam" },
    { "xh-ZA", "South Africa" },
    { "zh-CN", "China" },
    { "zh-HK", "Hong Kong" },
    { "zh-MO", "Macau" },
    { "zh-SG", "Singapore" },
    { "zh-TW", "Taiwan" },
    { "zu-ZA", "South Africa" }
};

    /// <summary>
    /// Looks up the display name of the country/region associated with the specified locale code.
    /// </summary>
    /// <param name="language">
    /// The BCP-47 locale code ("en-US", case-insensitive, "_" accepted). A language without region ("fr") resolves to its
    /// main country ("fr-FR"). Null or empty: the current culture.
    /// </param>
    /// <returns>The associated country/region name, or <see langword="null"/> if the locale is not recognized.</returns>
    public static string? Resolve(string? language)
    {
        language = string.IsNullOrWhiteSpace(language)
            ? CultureInfo.CurrentCulture.Name
            : language.Trim().Replace('_', '-');
        if (language.Length == 0)
            return null;

        if (localeToCountry.TryGetValue(language, out var country))
            return country;

        // "fr" → "fr-FR" from the culture data (absent in invariant globalization mode), then "xx-XX".
        try
        {
            var specific = CultureInfo.CreateSpecificCulture(language).Name;
            if (localeToCountry.TryGetValue(specific, out country))
                return country;
        }
        catch (CultureNotFoundException)
        {
        }

        var neutral = language.Split('-')[0];
        if (localeToCountry.TryGetValue($"{neutral}-{neutral.ToUpperInvariant()}", out country))
            return country;

        foreach (var (locale, name) in localeToCountry)
        {
            if (locale.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }
}
