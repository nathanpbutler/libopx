using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace libopx.Tests;

internal static class SampleFiles
{
    private static readonly HttpClient _http = new HttpClient();
    private static readonly object _lock = new();
    private static bool _initialized;

    private static string BaseVersion => Environment.GetEnvironmentVariable("OPX_SAMPLES_VERSION") ?? "v1.0.0";
    private static string BaseUrl => $"https://github.com/nathanpbutler/libopx/releases/download/{BaseVersion}";

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            Directory.CreateDirectory(Directory.GetCurrentDirectory());
            _initialized = true;
        }
    }

    public static async Task<bool> EnsureAsync(string fileName)
    {
        EnsureInitialized();
        if (File.Exists(fileName)) return true;

        var url = $"{BaseUrl}/{fileName}";
        try
        {
            // Up to 3 attempts with small backoff
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var resp = await _http.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        await Task.Delay(250 * attempt);
                        continue;
                    }
                    await using var src = await resp.Content.ReadAsStreamAsync();
                    await using var dst = File.Create(fileName);
                    await src.CopyToAsync(dst);
                    return true;
                }
                catch
                {
                    await Task.Delay(250 * attempt);
                }
            }
        }
        catch
        {
            // ignored
        }
        return File.Exists(fileName);
    }
}
