using System.Runtime.InteropServices;

namespace LocalRemoteView.Host;

internal static class NativeInput
{
    private const uint Mouse = 0, Keyboard = 1, MouseMoveFlag = 0x0001, Absolute = 0x8000, VirtualDesk = 0x4000, WheelFlag = 0x0800;
    public static bool Move(float x, float y)
    {
        var desktop = SystemInformation.VirtualScreen;
        var px = desktop.Left + (int)Math.Round(Math.Clamp(x, 0, 1) * Math.Max(0, desktop.Width - 1));
        var py = desktop.Top + (int)Math.Round(Math.Clamp(y, 0, 1) * Math.Max(0, desktop.Height - 1));
        return SetCursorPos(px, py);
    }
    public static bool Button(int button, bool down)
    {
        uint flag = button switch { 1 => down ? 0x0002u : 0x0004u, 2 => down ? 0x0008u : 0x0010u, 3 => down ? 0x0020u : 0x0040u, _ => 0 };
        return flag != 0 && SendMouse(0, 0, 0, flag);
    }
    public static bool Wheel(int delta) => SendMouse(0, 0, delta, WheelFlag);
    public static bool Key(ushort vk, bool down)
    {
        var input = new INPUT { type = Keyboard, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = down ? 0u : 0x0002u } } };
        return SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
    }
    private static bool SendMouse(int dx, int dy, int data, uint flags)
    {
        var input = new INPUT { type = Mouse, U = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } } };
        return SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
    }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy, mouseData; public uint dwFlags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCursorPos(int x, int y);
}
