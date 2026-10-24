using Microsoft.Win32;

namespace PenBoard.Services;

/// <summary>
/// Stops Windows from auto-invoking the touch keyboard/handwriting panel while
/// PenBoard automation is enabled. The original value is restored when the
/// automation switch is turned off or the tray app exits.
/// </summary>
public sealed class OskSuppression : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\TabletTip\1.7";
    private const string ValueName = "EnableDesktopModeAutoInvoke";

    private object? _originalValue;
    private bool _hadValue;
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

            _originalValue = key.GetValue(ValueName);
            _hadValue = _originalValue is not null;
            key.SetValue(ValueName, 0, RegistryValueKind.DWord);
            _applied = true;
            Logger.Log("OSK auto-invoke disabled (EnableDesktopModeAutoInvoke=0).");
        }
        catch (Exception ex)
        {
            Logger.Log("OskSuppression.Apply: " + ex);
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

            if (_hadValue)
            {
                key.SetValue(ValueName, _originalValue!, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
            Logger.Log("OSK auto-invoke restored.");
        }
        catch (Exception ex)
        {
            Logger.Log("OskSuppression.Restore: " + ex);
        }
        finally
        {
            _applied = false;
        }
    }

    public void Dispose() => Restore();
}
