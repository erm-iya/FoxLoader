using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace ErmiyaDesktop.Localization
{
    public class LanguageManager
    {
        private static LanguageManager? _instance;
        public static LanguageManager Instance => _instance ??= new LanguageManager();

        public event EventHandler? LanguageChanged;

        private Dictionary<string, string> _strings = new();
        private Dictionary<string, string> _fallbackStrings = new();
        private string _currentLanguage = "en";

        public string CurrentLanguage => _currentLanguage;

        public FlowDirection CurrentFlowDirection => 
            (_currentLanguage == "fa" || _currentLanguage == "ku") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        private LanguageManager()
        {
            LoadFallbackEnglish();
            string savedLang = LoadSavedLanguagePreference();
            SetLanguage(savedLang, notify: false);
        }

        public void SetLanguage(string langCode, bool notify = true)
        {
            if (string.IsNullOrWhiteSpace(langCode)) langCode = "en";
            _currentLanguage = langCode.ToLowerInvariant();

            LoadLanguageStrings(_currentLanguage);
            SaveLanguagePreference(_currentLanguage);

            if (notify)
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Get(string key, string? defaultVal = null)
        {
            if (_strings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
            if (_fallbackStrings.TryGetValue(key, out var fallback) && !string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }
            return defaultVal ?? key;
        }

        private void LoadLanguageStrings(string lang)
        {
            _strings.Clear();
            string path = Path.Combine(AppContext.BaseDirectory, "Localization", $"{lang}.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, $"{lang}.json");
            }

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        _strings = dict;
                    }
                }
                catch { }
            }
        }

        private void LoadFallbackEnglish()
        {
            _fallbackStrings.Clear();
            string path = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        _fallbackStrings = dict;
                    }
                }
                catch { }
            }
        }

        private string LoadSavedLanguagePreference()
        {
            try
            {
                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader", "language.cfg");
                if (File.Exists(configPath))
                {
                    string lang = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(lang)) return lang;
                }
            }
            catch { }
            return "en";
        }

        private void SaveLanguagePreference(string lang)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "language.cfg"), lang);
            }
            catch { }
        }
    }
}
