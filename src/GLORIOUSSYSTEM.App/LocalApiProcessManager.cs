using System.Diagnostics;
using System.Net;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Owns the local development API process used by the Windows app.
/// </summary>
public sealed class LocalApiProcessManager : IDisposable
{
    readonly string? _configuredProjectPath;
    readonly Uri _defaultBaseUri;
    readonly object _sync = new();
    readonly CancellationTokenSource _lifetime = new();
    Process? _ownedProcess;

    public LocalApiProcessManager(string? projectPath, string? baseUrl)
    {
        _configuredProjectPath = projectPath;
        _defaultBaseUri = CreateBaseUri(baseUrl);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token,
            cancellationToken);
        var ct = linkedCancellation.Token;
        var baseUri = GetConfiguredBaseUri();

        if (baseUri == null || !IsLoopback(baseUri.Host))
        {
            Debug.WriteLine("Local API auto-start skipped because the configured API URL is not local.");
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        using var healthClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromMilliseconds(750)
        };

        if (await IsHealthyAsync(healthClient, ct))
            return;

        lock (_sync)
        {
            if (_ownedProcess is { HasExited: false })
                return;
        }

        var projectPath = ResolveProjectPath();
        if (projectPath == null)
        {
            Debug.WriteLine("Local API auto-start skipped because GLORIOUSSYSTEM.Api.csproj was not found.");
            return;
        }

        Process? process;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(projectPath)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-restore");
            startInfo.ArgumentList.Add("--no-launch-profile");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["ASPNETCORE_URLS"] = baseUri.ToString().TrimEnd('/');

            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"Local API auto-start failed: {ex.Message}");
            return;
        }

        if (process == null)
            return;

        lock (_sync)
        {
            if (ct.IsCancellationRequested)
            {
                TryStop(process);
                return;
            }

            _ownedProcess = process;
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (await IsHealthyAsync(healthClient, ct))
                return;

            if (ct.IsCancellationRequested)
                break;

            if (process.HasExited)
                break;

            try
            {
                await Task.Delay(250, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Debug.WriteLine("Local API did not become healthy within 10 seconds.");
        Stop();
    }

    public void Stop()
    {
        _lifetime.Cancel();

        Process? process;
        lock (_sync)
        {
            process = _ownedProcess;
            _ownedProcess = null;
        }

        if (process != null)
            TryStop(process);
    }

    public void Dispose()
    {
        Stop();
        _lifetime.Dispose();
    }

    Uri? GetConfiguredBaseUri()
    {
        var configuredUrl = Preferences.Get("Api_BaseUrl", _defaultBaseUri.ToString());
        return Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? CreateBaseUri(uri.ToString())
            : null;
    }

    string? ResolveProjectPath()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_configuredProjectPath))
        {
            var configuredPath = _configuredProjectPath.Trim();
            if (Path.IsPathRooted(configuredPath))
                candidates.Add(configuredPath);
            else
            {
                AddRelativeCandidates(candidates, configuredPath, AppContext.BaseDirectory);
                AddRelativeCandidates(candidates, configuredPath, Environment.CurrentDirectory);
            }
        }

        AddRepositoryCandidates(candidates, AppContext.BaseDirectory);
        AddRepositoryCandidates(candidates, Environment.CurrentDirectory);

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    static void AddRelativeCandidates(ICollection<string> candidates, string relativePath, string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            candidates.Add(Path.Combine(directory.FullName, relativePath));
            directory = directory.Parent;
        }
    }

    static void AddRepositoryCandidates(ICollection<string> candidates, string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            candidates.Add(Path.Combine(
                directory.FullName,
                "src",
                "GLORIOUSSYSTEM.Api",
                "GLORIOUSSYSTEM.Api.csproj"));
            directory = directory.Parent;
        }
    }

    static async Task<bool> IsHealthyAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    static Uri CreateBaseUri(string? value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new Uri(uri.ToString().TrimEnd('/') + "/");
        }

        return new Uri("http://127.0.0.1:5053/");
    }

    static bool IsLoopback(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
