using System;
using System.IO;
using System.Text.Json;

namespace ErmiyaDesktop
{
    public static class FoxLoaderConfig
    {
        private static ushort _port = 2764;

        public static ushort Port
        {
            get => _port;
            set
            {
                if (value >= 1024) _port = value;
            }
        }

        public static string BaseUrl => $"http://127.0.0.1:{Port}";

        static FoxLoaderConfig()
        {
            LoadConfig();
        }

        public static void LoadConfig()
        {
            try
            {
                string[] candidatePaths = {
                    Path.Combine(AppContext.BaseDirectory, "settings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "settings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "settings.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader", "settings.json")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        var content = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("port", out var portEl) && portEl.TryGetUInt16(out var p))
                        {
                            if (p >= 1024)
                            {
                                _port = p;
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default port on read failure
            }
        }
    }
}
