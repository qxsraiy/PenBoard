using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace PenBoard.Services;

public sealed class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/qxsraiy/PenBoard/releases/latest";

    public async Task CheckAndInstallAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PenBoard/" + CurrentVersion());
            http.Timeout = TimeSpan.FromSeconds(30);

            var json = await http.GetStringAsync(LatestReleaseUrl);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "";
            if (!Version.TryParse(tag, out var latest))
            {
                MessageBox.Show("无法解析 GitHub 上的版本号：" + tag, "检查更新");
                return;
            }

            var current = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));
            if (CompareVersionParts(Normalize(latest), current) <= 0)
            {
                MessageBox.Show($"当前已是最新版本：{current}", "检查更新");
                return;
            }

            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                MessageBox.Show("新版本没有找到 ARM64 MSI 安装包。", "检查更新");
                return;
            }

            var answer = MessageBox.Show(
                $"发现新版本 {latest}（当前 {current}）。\n\n现在下载并覆盖安装吗？",
                "检查更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            // Keep the package outside %LOCALAPPDATA%\PenBoard. The MSI's
            // CleanupOldZipInstall action deletes that directory before
            // InstallFiles, which previously deleted the update package in the
            // middle of the first install attempt.
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PenBoardUpdates");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"PenBoard-{latest}-arm64.msi");

            var progressWindow = new UpdateProgressWindow();
            progressWindow.Show();
            try
            {
                using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var destination = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    copied += read;
                    var percent = total > 0 ? copied * 100.0 / total : -1;
                    var text = total > 0
                        ? $"{copied / 1024.0 / 1024.0:0.0} MB / {total / 1024.0 / 1024.0:0.0} MB"
                        : $"{copied / 1024.0 / 1024.0:0.0} MB";
                    progressWindow.SetProgress(percent, text);
                }
            }
            finally
            {
                progressWindow.Close();
            }

            Logger.Log($"Update downloaded: {file}");
            MessageBox.Show("下载完成，点击确定后 PenBoard 会退出并开始安装。", "检查更新");

            // Start msiexec directly. The previous generated batch waited for
            // the PenBoard PID, but on the user's machine the first attempt
            // exited without ever starting the installer. Starting msiexec
            // directly makes the handoff observable and avoids cmd quoting.
            var logPath = Path.Combine(Path.GetTempPath(), "PenBoard-update.log");
            var arguments = $"/i \"{file}\" /passive /norestart /l*v \"{logPath}\"";
            var installer = Process.Start(new ProcessStartInfo("msiexec.exe", arguments)
            {
                UseShellExecute = true
            });
            if (installer is null)
            {
                MessageBox.Show("无法启动 Windows Installer。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Logger.Log($"Update installer started: pid={installer.Id}, log={logPath}");
            await Task.Delay(800);
            Logger.Log("Exiting PenBoard for update.");
            Environment.Exit(0);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Log("CheckAndInstall: " + ex);
            MessageBox.Show("检查更新失败：\n" + ex.Message, "检查更新", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static Version CurrentVersion()
        => Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    private static Version Normalize(Version version)
        => new(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build));

    /// <summary>
    /// Compare major first, then minor, then build. Each component is a single
    /// digit in PenBoard's version scheme (0.2.9 -> 0.3.0).
    /// </summary>
    private static int CompareVersionParts(Version left, Version right)
    {
        var result = left.Major.CompareTo(right.Major);
        if (result != 0)
        {
            return result;
        }
        result = left.Minor.CompareTo(right.Minor);
        if (result != 0)
        {
            return result;
        }
        return left.Build.CompareTo(right.Build);
    }
}
