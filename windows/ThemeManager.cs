using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ErmiyaDesktop
{
    public static class ThemeManager
    {
        public static string CurrentTheme { get; private set; } = "System Settings";
        public static ElementTheme CurrentElementTheme { get; private set; } = ElementTheme.Default;

        public static void Initialize()
        {
            var saved = LoadSavedTheme();
            ApplyTheme(saved);
        }

        public static void ApplyTheme(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme)) theme = "System Settings";
            CurrentTheme = theme;
            SaveTheme(theme);

            ElementTheme elemTheme = ElementTheme.Default;
            Color? accentColor = null;

            switch (theme)
            {
                case "Dark":
                    elemTheme = ElementTheme.Dark;
                    accentColor = Color.FromArgb(255, 76, 175, 80); // Default clean green/accent or default WinUI
                    break;

                case "Light":
                    elemTheme = ElementTheme.Light;
                    break;

                case "Dark Teal":
                    elemTheme = ElementTheme.Dark;
                    accentColor = Color.FromArgb(255, 0, 188, 165); // Vibrant modern teal
                    break;

                case "Light Teal":
                    elemTheme = ElementTheme.Light;
                    accentColor = Color.FromArgb(255, 0, 137, 123); // Elegant deep teal for light backgrounds
                    break;

                default: // "System Settings"
                    elemTheme = ElementTheme.Default;
                    break;
            }

            CurrentElementTheme = elemTheme;

            if (accentColor.HasValue)
            {
                try
                {
                    var color = accentColor.Value;
                    Application.Current.Resources["SystemAccentColor"] = color;
                    Application.Current.Resources["AccentColor"] = color;
                    
                    var brush = new SolidColorBrush(color);
                    Application.Current.Resources["AccentFillColorDefaultBrush"] = brush;
                    Application.Current.Resources["AccentTextFillColorPrimaryBrush"] = brush;
                    Application.Current.Resources["AccentButtonBackground"] = brush;
                }
                catch { }
            }

            try
            {
                if (App.MainWindowInstance?.Content is FrameworkElement root)
                {
                    root.RequestedTheme = elemTheme;
                }
            }
            catch { }
        }

        public static void ApplyToElement(FrameworkElement? element)
        {
            if (element != null)
            {
                element.RequestedTheme = CurrentElementTheme;
            }
        }

        private static string LoadSavedTheme()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader");
                string path = Path.Combine(dir, "theme.cfg");
                if (File.Exists(path))
                {
                    var t = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(t)) return t;
                }
            }
            catch { }
            return "System Settings";
        }

        private static void SaveTheme(string theme)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "theme.cfg"), theme);
            }
            catch { }
        }
    }
}
