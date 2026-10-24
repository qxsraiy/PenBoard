using System.Runtime.InteropServices;
using PenBoard.Interop;

namespace PenBoard.Services;

public sealed class TextInjector
{
    public bool TryInsert(string text, IntPtr targetHwnd)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (targetHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.GetForegroundWindow() != targetHwnd)
        {
            Logger.Log($"TextInjector: foreground changed (target=0x{targetHwnd.ToInt64():X})");
            return false;
        }

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2 + 4);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\r':
                    continue;
                case '\n':
                    AddVirtualKey(inputs, NativeMethods.VK_RETURN);
                    continue;
                case '\t':
                    AddVirtualKey(inputs, NativeMethods.VK_TAB);
                    continue;
                default:
                    AddUnicode(inputs, ch);
                    break;
            }
        }

        if (inputs.Count == 0)
        {
            return true;
        }

        var array = inputs.ToArray();
        var sent = NativeMethods.SendInput((uint)array.Length, array, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != array.Length)
        {
            Logger.Log($"TextInjector: SendInput sent {sent}/{array.Length}, error={Marshal.GetLastWin32Error()}");
            return false;
        }

        return true;
    }

    public bool TrySendBackspace(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero ||
            NativeMethods.GetForegroundWindow() != targetHwnd)
        {
            return false;
        }

        var inputs = new List<NativeMethods.INPUT>(2);
        AddVirtualKey(inputs, NativeMethods.VK_BACK);
        var array = inputs.ToArray();
        var sent = NativeMethods.SendInput((uint)array.Length, array, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != array.Length)
        {
            Logger.Log($"TextInjector.Backspace: SendInput sent {sent}/{array.Length}, error={Marshal.GetLastWin32Error()}");
            return false;
        }

        return true;
    }

    private static void AddUnicode(List<NativeMethods.INPUT> inputs, char ch)
    {
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE
                }
            }
        });
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                }
            }
        });
    }

    private static void AddVirtualKey(List<NativeMethods.INPUT> inputs, ushort vk)
    {
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0 }
            }
        });
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                }
            }
        });
    }
}
