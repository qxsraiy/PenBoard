using System.Diagnostics;
using Microsoft.Win32;

namespace PenBoard.Services;

/// <summary>
/// Suppresses the Windows touch keyboard / handwriting panel while PenBoard
/// automation is enabled. Several Windows builds use different values, so all
/// known auto-invoke switches are applied and restored together.
/// </summary>
public sealed class OskSuppression : IDisposable
{
    private const string TabletTip = @"Software\Microsoft\TabletTip\1.7";
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private static readonly (string Path, string Name, int Value)[] Values =
    {
        (TabletTip, "EnableDesktopModeAutoInvoke", 0),
        (TabletTip, "EnableAutoInvoke", 0),
        (TabletTip, "EnableTouchKeyboardAutoShow", 0),
        (TabletTip, "DisableTouchKeyboardAutoShow", 1),
        (TabletTip, "ShowTouchKeyboardOnFocus", 0),
        (TabletTip, "EnableHandwritingPanel", 0),
        (TabletTip, "EnableTouchKeyboard", 0),
        (TabletTip, "TouchKeyboardAutoInvoke", 0),
        (TabletTip, "AutoShowTouchKeyboard", 0),
        (ExplorerAdvanced, "EnableTouchKeyboard", 0)
    };

    public static void RestoreSystemDefaults()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TabletTip, true)
                            ?? Registry.CurrentUser.CreateSubKey(TabletTip, true);
            if (key is not null)
            {
                key.SetValue("EnableDesktopModeAutoInvoke", 1, RegistryValueKind.DWord);
                foreach (var name in new[]
                         {
                             "EnableAutoInvoke",
                             "EnableTouchKeyboardAutoShow",
                             "DisableTouchKeyboardAutoShow",
                             "ShowTouchKeyboardOnFocus",
                             "EnableHandwritingPanel",
                             "EnableTouchKeyboard",
                             "TouchKeyboardAutoInvoke",
                             "AutoShowTouchKeyboard"
                         })
                {
                    key.DeleteValue(name, false);
                }
            }

            using var explorer = Registry.CurrentUser.OpenSubKey(ExplorerAdvanced, true);
            explorer?.DeleteValue("EnableTouchKeyboard", false);
            Logger.Log("System touch keyboard defaults restored.");
        }
        catch (Exception ex)
        {
            Logger.Log("RestoreSystemDefaults: " + ex);
        }
    }

    private readonly Dictionary<string, object?> _originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);
    private bool _applied;

    public void Apply()
    {
        if (_applied)
        {
            return;
        }

        try
        {
            _originals.Clear();
            _missing.Clear();
            foreach (var group in Values.GroupBy(v => v.Path))
            {
                using var key = Registry.CurrentUser.OpenSubKey(group.Key, true)
                                ?? Registry.CurrentUser.CreateSubKey(group.Key, true);
                if (key is null)
                {
                    continue;
                }

                foreach (var (_, name, value) in group)
                {
                    var original = key.GetValue(name);
                    var id = group.Key + "|" + name;
                    if (original is null)
                    {
                        _missing.Add(id);
                    }
                    else
                    {
                        _originals[id] = original;
                    }
                    key.SetValue(name, value, RegistryValueKind.DWord);
                }
            }

            _applied = true;
            Logger.Log("OSK registry suppression applied: " + string.Join(", ",
                Values.Select(v => v.Path + "\\" + v.Name + "=" + v.Value)));
            KillOskProcesses();
        }
        catch (Exception ex)
        {
            Logger.Log("OskSuppression.Apply: " + ex);
        }
    }

    public void KillOskProcesses()
    {
        foreach (var name in new[] { "TabTip", "TextInputHost" })
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        process.Kill();
                        Logger.Debug($"OSK process killed: {name} ({process.Id})");
                    }
                    catch
                    {
                        // The process may already be exiting.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("KillOskProcesses " + name + ": " + ex.Message);
            }
        }
    }

    public void Restore()
    {
        if (!_applied)
        {
            return;
        }

        try
        {
            foreach (var group in Values.GroupBy(v => v.Path))
            {
                using var key = Registry.CurrentUser.OpenSubKey(group.Key, true);
                if (key is null)
                {
                    continue;
                }

                foreach (var (_, name, _) in group)
                {
                    var id = group.Key + "|" + name;
                    if (_originals.TryGetValue(id, out var original))
                    {
                        key.SetValue(name, original!, RegistryValueKind.DWord);
                    }
                    else if (_missing.Contains(id))
                    {
                        key.DeleteValue(name, false);
                    }
                }
            }
            Logger.Log("OSK registry suppression restored.");
        }
        catch (Exception ex)
        {
            Logger.Log("OskSuppression.Restore: " + ex);
        }
        finally
        {
            _applied = false;
            _originals.Clear();
            _missing.Clear();
        }
    }

    public void Dispose() => Restore();
}
