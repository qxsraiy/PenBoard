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
            if (Normalize(latest) <= current)
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

            // Wait explicitly for this process to exit, then start msiexec.
            // A fixed timeout was not enough on slower ARM64 machines and
            // caused the first install attempt to fail.
            var pid = Environment.ProcessId;
            var logPath = Path.Combine(Path.GetTempPath(), "PenBoard-update.log");
            var batchPath = Path.Combine(Path.GetTempPath(), "PenBoard-update-launch.cmd");
            var batch = string.Join("\r\n", new[]
            {
                "@echo off",
                ":wait",
                $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul",
                "if not errorlevel 1 (",
                "  timeout /t 1 /nobreak >nul",
                "  goto wait",
                ")",
                $"msiexec.exe /i \"{file}\" /passive /norestart /l*v \"{logPath}\"",
                "exit /b"
            });
            File.WriteAllText(batchPath, batch, Encoding.Default);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c call \"{batchPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            Logger.Log($"Update installer waiting for PID {pid}; log={logPath}");
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
}
