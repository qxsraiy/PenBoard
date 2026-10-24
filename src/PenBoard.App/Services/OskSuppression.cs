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
    private const string KeyPath = @"Software\Microsoft\TabletTip\1.7";

    private static readonly (string Name, int Value)[] Values =
    {
        ("EnableDesktopModeAutoInvoke", 0),
        ("EnableAutoInvoke", 0),
        ("EnableTouchKeyboardAutoShow", 0),
        ("DisableTouchKeyboardAutoShow", 1),
        ("ShowTouchKeyboardOnFocus", 0),
        ("EnableHandwritingPanel", 0)
    };

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
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(KeyPath, true);
            if (key is null)
            {
                return;
            }

            _originals.Clear();
            _missing.Clear();
            foreach (var (name, value) in Values)
            {
                var original = key.GetValue(name);
                if (original is null)
                {
                    _missing.Add(name);
                }
                else
                {
                    _originals[name] = original;
                }
                key.SetValue(name, value, RegistryValueKind.DWord);
            }

            _applied = true;
            Logger.Log("OSK registry suppression applied: " + string.Join(", ",
                Values.Select(v => v.Name + "=" + v.Value)));
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
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (key is null)
            {
                return;
            }

            foreach (var (name, _) in Values)
            {
                if (_originals.TryGetValue(name, out var original))
                {
                    key.SetValue(name, original!, RegistryValueKind.DWord);
                }
                else if (_missing.Contains(name))
                {
                    key.DeleteValue(name, false);
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
