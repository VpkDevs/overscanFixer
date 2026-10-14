using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OverscanFix;

public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseNotes,
    string HtmlUrl
);

public static class UpdateChecker
{
    private const string RepoOwner = "VpkDevs";
    private const string RepoName = "overscanFixer";
    private static readonly HttpClient HttpClient = new();

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OverscanFix-UpdateChecker/1.5");
        HttpClient.Timeout = TimeSpan.FromSeconds(6);
    }

    public static string GetCurrentVersionString()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.5.0";
    }

    public static async Task<UpdateCheckResult?> CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Info($"Update check returned status code: {response.StatusCode}");
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";

            string cleanTag = tagName.TrimStart('v', 'V');
            string currentVerStr = GetCurrentVersionString();

            if (Version.TryParse(cleanTag, out var latestVer) &&
                Version.TryParse(currentVerStr, out var currentVer))
            {
                bool isNewer = latestVer > currentVer;
                return new UpdateCheckResult(
                    IsUpdateAvailable: isNewer,
                    CurrentVersion: currentVerStr,
                    LatestVersion: cleanTag,
                    ReleaseName: string.IsNullOrWhiteSpace(name) ? tagName : name,
                    ReleaseNotes: body,
                    HtmlUrl: htmlUrl
                );
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Update check failed (non-blocking): {ex.Message}");
            return null;
        }
    }
}
