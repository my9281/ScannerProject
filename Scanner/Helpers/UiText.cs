using System;
using System.Linq;
using System.Windows;

namespace Scanner.Helpers
{
    public static class UiText
    {
        public static string Get(string key)
        {
            return Application.Current?.TryFindResource(key)?.ToString() ?? key;
        }

        public static void ChangeLanguage(string language)
        {
            if (language != "en-US" && language != "es-ES") language = "zh-CN";
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var replacement = new ResourceDictionary
            {
                Source = new Uri("/Scanner;component/Languages/Language." + language + ".xaml", UriKind.Relative)
            };
            var previous = dictionaries.FirstOrDefault(item => item.Source != null && item.Source.OriginalString.Contains("Languages/Language."));
            if (previous == null) dictionaries.Add(replacement);
            else dictionaries[dictionaries.IndexOf(previous)] = replacement;
        }
    }
}
