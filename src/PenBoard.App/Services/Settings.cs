using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenBoard.Services;

public sealed class Settings
{
    public string InkColorHex { get; set; } = "#FF111111";
    public double InkWidth { get; set; } = 4;
    public int RecognitionDelayMs { get; set; } = 800;
    public int CandidateDelayMs { get; set; } = 1500;
    public bool AutoSavePng { get; set; } = true;
    public bool AutoSavePdf { get; set; } = true;
    public double PdfDpi { get; set; } = 144;
    public bool DebugHotkey { get; set; } = true;
    public bool DebugLogging { get; set; }
    public string RecognitionLanguage { get; set; } = "zh";
    public bool AutoReplaceTouchKeyboard { get; set; }
    public string? SaveDirectoryOverride { get; set; }

    [JsonIgnore]
    public string SaveDirectory =>
        string.IsNullOrWhiteSpace(SaveDirectoryOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PenBoard")
            : SaveDirectoryOverride!;

    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PenBoard");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Settings.Load: " + ex);
        }

        var settings = new Settings();
        settings.Save();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Logger.Log("Settings.Save: " + ex);
        }
    }

    private void Normalize()
    {
        RecognitionDelayMs = Math.Clamp(RecognitionDelayMs, 250, 5000);
        CandidateDelayMs = Math.Clamp(CandidateDelayMs, 500, 5000);
        InkWidth = Math.Clamp(InkWidth, 1, 32);
        PdfDpi = Math.Clamp(PdfDpi, 72, 600);
        if (string.IsNullOrWhiteSpace(InkColorHex))
        {
            InkColorHex = "#FF111111";
        }

        RecognitionLanguage = string.Equals(RecognitionLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "zh";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
