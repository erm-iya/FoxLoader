using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErmiyaDesktop
{
    public class DuplicateCheckResult
    {
        public bool IsDuplicate { get; set; }
        public string ExistingFilePath { get; set; } = string.Empty;
        public string DownloadedDate { get; set; } = string.Empty;
        public bool ExistsOnDisk { get; set; }
    }

    public class PreviousVersionResult
    {
        public bool HasPreviousVersion { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string PreviousVersion { get; set; } = string.Empty;
        public string PreviousFilePath { get; set; } = string.Empty;
        public string DownloadedDate { get; set; } = string.Empty;
        public bool ExistsOnDisk { get; set; }
    }

    public class MultiPartInfo
    {
        public bool IsMultiPart { get; set; }
        public string BaseName { get; set; } = string.Empty;
        public int CurrentPart { get; set; }
        public string Extension { get; set; } = string.Empty;
        public List<PartItem> AvailableParts { get; set; } = new();
    }

    public class PartItem
    {
        public int PartNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long? SizeBytes { get; set; }
        public string FormattedSize { get; set; } = "Checking...";
        public bool IsSelected { get; set; } = true;
    }

    public class SeriesInfo
    {
        public bool IsSeries { get; set; }
        public string SeriesTitle { get; set; } = string.Empty;
        public int Season { get; set; }
        public int Episode { get; set; }
        public string Quality { get; set; } = string.Empty;
        public string Codec { get; set; } = string.Empty;
        public string SuggestedFolder { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }

    public class SmartAnalysisResult
    {
        public DuplicateCheckResult Duplicate { get; set; } = new();
        public PreviousVersionResult PreviousVersion { get; set; } = new();
        public MultiPartInfo MultiPart { get; set; } = new();
        public SeriesInfo Series { get; set; } = new();
    }

    public static class SmartDownloadIntelligence
    {
        private static readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        static SmartDownloadIntelligence()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FoxLoader/1.0");
        }

        public static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
            }
            catch
            {
                int qIdx = url.IndexOf('?');
                return qIdx > 0 ? url[..qIdx] : url;
            }
        }

        public static async Task<SmartAnalysisResult> AnalyzeAsync(string url, string fileName, List<JobStatus>? existingJobs)
        {
            var result = new SmartAnalysisResult();
            if (string.IsNullOrWhiteSpace(url)) return result;

            string cleanUrl = SanitizeUrl(url);
            string baseFileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : Path.GetFileName(cleanUrl);

            // 1. Duplicate & Previous Version Detection
            if (existingJobs != null && existingJobs.Count > 0)
            {
                CheckDuplicatesAndVersions(cleanUrl, baseFileName, existingJobs, result);
            }

            // 2. Multi-Part Archive Detection & Online Probing
            result.MultiPart = await DetectMultiPartArchiveAsync(url, baseFileName);

            // 3. TV Series Season & Episode Detection
            result.Series = DetectSeries(url, baseFileName);

            return result;
        }

        private static void CheckDuplicatesAndVersions(string cleanUrl, string fileName, List<JobStatus> existingJobs, SmartAnalysisResult result)
        {
            var (appName, appVer) = ExtractAppNameAndVersion(fileName);

            foreach (var job in existingJobs)
            {
                string jobCleanUrl = SanitizeUrl(job.Url);
                string jobFileName = job.FileName ?? string.Empty;
                string jobSavePath = job.SavePath ?? string.Empty;
                string fullPath = Path.Combine(jobSavePath, jobFileName);
                bool fileExists = File.Exists(fullPath);

                // Exact duplicate check
                if (string.Equals(cleanUrl, jobCleanUrl, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(fileName, jobFileName, StringComparison.OrdinalIgnoreCase) && fileExists))
                {
                    result.Duplicate.IsDuplicate = true;
                    result.Duplicate.ExistingFilePath = fullPath;
                    result.Duplicate.ExistsOnDisk = fileExists;
                    result.Duplicate.DownloadedDate = fileExists ? File.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd HH:mm") : "Previous Download";
                    return; // Highest priority match
                }

                // Previous version check
                if (!string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(appVer))
                {
                    var (pastName, pastVer) = ExtractAppNameAndVersion(jobFileName);
                    if (!string.IsNullOrEmpty(pastName) && !string.IsNullOrEmpty(pastVer) &&
                        string.Equals(appName, pastName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(appVer, pastVer, StringComparison.OrdinalIgnoreCase))
                    {
                        result.PreviousVersion.HasPreviousVersion = true;
                        result.PreviousVersion.AppName = appName;
                        result.PreviousVersion.CurrentVersion = appVer;
                        result.PreviousVersion.PreviousVersion = pastVer;
                        result.PreviousVersion.PreviousFilePath = fullPath;
                        result.PreviousVersion.ExistsOnDisk = fileExists;
                        result.PreviousVersion.DownloadedDate = fileExists ? File.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd") : "Earlier version";
                    }
                }
            }
        }

        public static (string Name, string Version) ExtractAppNameAndVersion(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return (string.Empty, string.Empty);

            // Strip extension and multi-part suffix
            string nameWithoutExt = Regex.Replace(fileName, @"(?i)(\.part\d+|\.z\d+|\.r\d+)?\.(rar|zip|7z|exe|msi|tar|gz|apk)$", "");

            // Look for version pattern like v1.2.3, 6.43.12, 2026.v27.10
            var verMatch = Regex.Match(nameWithoutExt, @"(?i)[._ -]v?(?<ver>\d+(\.\d+){1,4}(?:[._ -]?(?:beta|alpha|rc\d*|\d+))?)");
            if (verMatch.Success)
            {
                string ver = verMatch.Groups["ver"].Value;
                string appName = nameWithoutExt[..verMatch.Index]
                    .Replace('.', ' ')
                    .Replace('_', ' ')
                    .Trim();

                return (appName, ver);
            }

            return (string.Empty, string.Empty);
        }

        private static async Task<MultiPartInfo> DetectMultiPartArchiveAsync(string url, string fileName)
        {
            var info = new MultiPartInfo();

            // Match patterns like: name.part2.rar or name.part02.rar
            var match = Regex.Match(fileName, @"(?i)(?<prefix>.+?)[._ -]part(?<num>\d+)(?<ext>\.(?:rar|zip|7z|tar|gz))$");
            if (!match.Success)
            {
                // Match patterns like: name.r01, name.z01, name.001
                match = Regex.Match(fileName, @"(?i)(?<prefix>.+?)\.(?<type>[rz])(?<num>\d{2,3})$");
            }

            if (!match.Success) return info;

            info.IsMultiPart = true;
            info.BaseName = match.Groups["prefix"].Value;
            info.Extension = match.Groups["ext"].Success ? match.Groups["ext"].Value : ".rar";
            int currentPart = int.TryParse(match.Groups["num"].Value, out int p) ? p : 1;
            info.CurrentPart = currentPart;

            int padLength = match.Groups["num"].Value.Length;

            // Probe available parts on server (starting from part 1 up to 16 parts)
            var tasks = new List<Task<PartItem?>>();
            for (int i = 1; i <= 12; i++)
            {
                int partNum = i;
                tasks.Add(ProbePartAsync(url, fileName, match, partNum, padLength));
            }

            var parts = await Task.WhenAll(tasks);
            foreach (var part in parts.Where(p => p != null).OrderBy(p => p!.PartNumber))
            {
                info.AvailableParts.Add(part!);
            }

            return info;
        }

        private static async Task<PartItem?> ProbePartAsync(string originalUrl, string originalFileName, Match match, int partNum, int padLength)
        {
            string numStr = partNum.ToString().PadLeft(padLength, '0');
            string oldNumPattern = match.Groups["num"].Value;

            string targetFileName;
            string targetUrl;

            if (match.Groups["ext"].Success)
            {
                targetFileName = Regex.Replace(originalFileName, @"(?i)part\d+", $"part{numStr}");
                targetUrl = Regex.Replace(originalUrl, @"(?i)part\d+", $"part{numStr}");
            }
            else
            {
                string type = match.Groups["type"].Value;
                targetFileName = Regex.Replace(originalFileName, @"(?i)\." + type + @"\d{2,3}$", $".{type}{numStr}");
                targetUrl = Regex.Replace(originalUrl, @"(?i)\." + type + @"\d{2,3}", $".{type}{numStr}");
            }

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, targetUrl);
                var resp = await _httpClient.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    long? size = resp.Content.Headers.ContentLength;
                    return new PartItem
                    {
                        PartNumber = partNum,
                        FileName = targetFileName,
                        Url = targetUrl,
                        SizeBytes = size,
                        FormattedSize = size.HasValue ? FormatBytes(size.Value) : "Available",
                        IsSelected = true
                    };
                }
            }
            catch { }

            return null;
        }

        private static SeriesInfo DetectSeries(string url, string fileName)
        {
            var info = new SeriesInfo { OriginalUrl = url, OriginalFileName = fileName };

            // Pattern: Show.Name.S05E05.Quality.mkv or Show.Name.5x05...
            var match = Regex.Match(fileName, @"(?i)(?<show>.+?)[._ -]+(?:S(?<season>\d{1,2})E(?<episode>\d{1,2})|(?<season>\d{1,2})x(?<episode>\d{1,2}))(?<rest>.*)");
            if (!match.Success)
            {
                // Check in URL path if filename didn't match (e.g. /Series/Lucifer/S05/...)
                match = Regex.Match(url, @"(?i)/(?<show>[^/]+)/S(?<season>\d{1,2})/(?:.+?)E(?<episode>\d{1,2})");
            }

            if (!match.Success) return info;

            info.IsSeries = true;
            string rawShow = match.Groups["show"].Value.Replace('.', ' ').Replace('_', ' ').Trim();
            info.SeriesTitle = rawShow;

            int.TryParse(match.Groups["season"].Value, out int season);
            int.TryParse(match.Groups["episode"].Value, out int episode);
            info.Season = season > 0 ? season : 1;
            info.Episode = episode > 0 ? episode : 1;

            // Extract Quality
            var qMatch = Regex.Match(fileName + " " + url, @"(?i)(2160p|4k|1080p|720p|480p)");
            info.Quality = qMatch.Success ? qMatch.Value.ToUpper() : "HD";

            // Extract Codec / Source
            var codecMatch = Regex.Match(fileName, @"(?i)(WEB-DL|BluRay|HDTV|x264|x265|HEVC|10bit)");
            info.Codec = codecMatch.Success ? codecMatch.Value : "WEB-DL";

            info.SuggestedFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "FoxLoader", "Video", info.SeriesTitle, $"S{info.Season:D2}"
            );

            return info;
        }

        public static List<(string Url, string FileName)> GenerateEpisodeBatch(SeriesInfo series, int startEp, int endEp)
        {
            var list = new List<(string Url, string FileName)>();
            if (!series.IsSeries || startEp > endEp) return list;

            string sTag = $"S{series.Season:D2}";
            string currentEpTag = $"E{series.Episode:D2}";

            for (int ep = startEp; ep <= endEp; ep++)
            {
                string targetEpTag = $"E{ep:D2}";
                string targetFileName = Regex.Replace(series.OriginalFileName, @"(?i)E\d{1,2}", targetEpTag);
                string targetUrl = Regex.Replace(series.OriginalUrl, @"(?i)E\d{1,2}", targetEpTag);

                list.Add((targetUrl, targetFileName));
            }

            return list;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
