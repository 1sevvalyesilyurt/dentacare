using System.Diagnostics;
using System.Net;

namespace DentalClinic.PlaywrightTests;

/// <summary>
/// NUnit SetUpFixture — starts the ASP.NET Core app once for the entire test
/// assembly in a "Playwright" environment backed by a fresh SQLite database.
/// </summary>
[SetUpFixture]
public class ServerFixture
{
    public const string BaseUrl = "http://localhost:5052";

    private static Process? _process;

    [OneTimeSetUp]
    public async Task StartServerAsync()
    {
        var webProjectDir = ResolveWebProjectDir();
        var dbPath = Path.Combine(webProjectDir, "playwright-test.db");

        // Delete stale test DB so every run starts clean
        if (File.Exists(dbPath))        File.Delete(dbPath);
        if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
        if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{webProjectDir}\" --no-launch-profile")
        {
            UseShellExecute         = false,
            RedirectStandardOutput  = true,
            RedirectStandardError   = true,
            WorkingDirectory        = webProjectDir,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"]                   = "Playwright";
        psi.Environment["ASPNETCORE_URLS"]                          = BaseUrl;
        psi.Environment["SeedSettings__DefaultSecretaryPassword"]   = "Admin@123";
        psi.Environment["SeedSettings__PlaywrightDoctorPassword"]   = "Doctor@123!";
        psi.Environment["SeedSettings__PlaywrightCustomerPassword"] = "Customer@123!";

        _process = Process.Start(psi)!;

        // Stream output to NUnit console for debugging
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) TestContext.Progress.WriteLine("[APP] " + e.Data); };
        _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) TestContext.Progress.WriteLine("[ERR] " + e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForReadyAsync(BaseUrl, timeoutSeconds: 90);
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        try
        {
            _process?.Kill(entireProcessTree: true);
            _process?.WaitForExit(5_000);
        }
        catch { /* process may have already exited */ }
        finally
        {
            _process?.Dispose();
        }
    }

    private static async Task WaitForReadyAsync(string url, int timeoutSeconds)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync(url);
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                {
                    TestContext.Progress.WriteLine($"[ServerFixture] App ready at {url}");
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // server not up yet
            }
            await Task.Delay(1_000);
        }
        throw new TimeoutException($"App at {url} did not become ready within {timeoutSeconds}s.");
    }

    private static string ResolveWebProjectDir()
    {
        // Assembly location: .../DentalClinic.PlaywrightTests/bin/Debug/net9.0/
        var baseDir     = AppContext.BaseDirectory;
        var solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var webDir      = Path.Combine(solutionDir, "DentalClinic.Web");

        if (!Directory.Exists(webDir))
            throw new DirectoryNotFoundException(
                $"Web project directory not found at expected path: {webDir}");

        return webDir;
    }
}
