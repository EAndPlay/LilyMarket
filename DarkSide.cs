using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace LilyMarket;

public partial class DarkSide
{
    private static Rectangle Rect;
    //public static IntPtr ClipCursorPointer;

    [Flags]
    public enum SetWindowPosFlags
    {
        NOSIZE = 0x0001,
        NOMOVE = 0x0002,
        NOZORDER = 0x0004,
        NOREDRAW = 0x0008,
        NOACTIVATE = 0x0010,
        DRAWFRAME = 0x0020,
        FRAMECHANGED = 0x0020,
        SHOWWINDOW = 0x0040,
        HIDEWINDOW = 0x0080,
        NOCOPYBITS = 0x0100,
        NOOWNERZORDER = 0x0200,
        NOREPOSITION = 0x0200,
        NOSENDCHANGING = 0x0400,
        DEFERERASE = 0x2000,
        ASYNCWINDOWPOS = 0x4000
    }

    [Flags]
    public enum SetWindowPosShowFlags
    {
        HWND_TOP = 0,
        HWND_BOTTOM = 1,
        HWND_TOPMOST = -1,
        HWND_NOTOPMOST = -2
    }

    private enum InternalMouseEventFlags
    {
        MOUSEEVENTF_MOVE = 1,
        DOWN = 2,
        UP = 4,
        MOUSEEVENTF_ABSOLUTE = 0x8000
    }

    [Flags]
    public enum MouseEventFlags
    {
        Move = 0x8001,
        LeftButtonDown = 2,
        LeftButtonUp = 4,
        RightButtonDown = 8,
        RightButtonUp = 0x10,
    }

    [Flags]
    public enum SystemMetricsIndex
    {
        SM_CXSCREEN,
        SM_CYSCREEN
    }

    [Flags]
    public enum MouseButton
    {
        Control = 0x0008,
        LeftButton = 0x0001,
        MiddleButton = 0x0010,
        RightButton = 0x0002,
        Shift = 0x0004,
        XButton = 0x0020,
        XButton2 = 0x0040
    }

    public enum SystemMessage : uint
    {
        WindowMove = 0x3, // low - x, high - y
        WindowClose = 0x10,

        //WindowQuit = 0x12, // low - exit code | useless message anyway
        WindowShow = 0x18,
        SetCursor = 0x20, // low - position, high - event which triggered (ingoing)
        KeyboardKeyDown = 0x100,
        KeyboardKeyUp = 0x101,
        MouseMove = 0x200,
        MouseLeftButtonDown = 0x201,
        MouseLeftButtonUp = 0x202,
        MouseLeftButtonDoubleClick = 0x203,
        MouseRightButtonDown = 0x204,
        MouseRightButtonUp = 0x205,
        MouseRightButtonDoubleClick = 0x206,
        MouseMiddleButtonDown = 0x207,
        MouseMiddleButtonUp = 0x208,
        MouseMiddleButtonDoubleClick = 0x209,
        MouseNonClientMove = 0xA0,
        MouseNonClientLeftButtonDown = 0xA1,
        MouseNonClientLeftButtonUp = 0xA2,
        MouseNonClientLeftButtonDoubleClick = 0xA3,
        MouseNonClientRightButtonDown = 0xA4,
        MouseNonClientRightButtonUp = 0xA5,
        MouseNonClientRightButtonDoubleClick = 0xA6,
        MouseNonClientMiddleButtonDown = 0xA7,
        MouseNonClientMiddleButtonUp = 0xA8,
        MouseNonClientMiddleButtonDoubleClick = 0xA9,
        MouseWheel = 0x20E,
        MouseCaptureChanged = 0x215,
        MouseNonClientWindowHover = 0x2A0,
        MouseWindowHover = 0x2A1,
        MouseNonClientWindowLeave = 0x2A2,
        MouseWindowLeave = 0x2A3,
        ClipboardClear = 0x303,
        ClipboardCopy = 0x301,
        ClipboardCut = 0x300, // wtf it does
        ClipboardPaste = 0x302,
        ClipboardUpdate = 0x31D
    }

    public static void SendLeftButtonKey(IntPtr windowHandle, bool keyDown)
    {
        var mouseButton = MouseButton.LeftButton;
        var systemMessage = keyDown ? SystemMessage.MouseLeftButtonDown : SystemMessage.MouseLeftButtonUp;

        if (!keyDown)
            mouseButton &= (MouseButton)(0x7FFFFFFF ^ (int)MouseButton.LeftButton);
        //SendMessage(windowHandle, SystemMessage.MouseMove, 0, lParam);
        SendMessage(windowHandle, systemMessage, (int)mouseButton, 0);
    }

    public static void SendMouseKey(IntPtr windowHandle, MouseButton mouseButton, bool keyDown, int x = -1, int y = -1)
    {
        //SendMessage((int) HookModel.WindowHandle, SystemMessage.MouseLeftButtonDown, KeyDownMessage.LeftButton, MAKELPARAM(345, 165));
        var lParam = 0;
        if (y != -1) lParam = y << 16;
        if (x != -1) lParam |= x & 0xFFFF;

        // SystemMessage GetSystemMessage(MouseButton mouseButton, bool keyDown) => mouseButton switch
        // {
        //     MouseButton.LeftButton => keyDown ? SystemMessage.MouseLeftButtonDown : SystemMessage.MouseLeftButtonUp,
        //     MouseButton.RightButton => keyDown ? SystemMessage.MouseRightButtonDown : SystemMessage.MouseRightButtonUp,
        //     MouseButton.MiddleButton => keyDown ? SystemMessage.MouseMiddleButtonDown : SystemMessage.MouseMiddleButtonUp,
        //     _ => throw new NotImplementedException()
        // };

        var systemMessage = mouseButton switch
        {
            MouseButton.LeftButton => keyDown ? SystemMessage.MouseLeftButtonDown : SystemMessage.MouseLeftButtonUp,
            MouseButton.RightButton => keyDown ? SystemMessage.MouseRightButtonDown : SystemMessage.MouseRightButtonUp,
            MouseButton.MiddleButton => keyDown
                ? SystemMessage.MouseMiddleButtonDown
                : SystemMessage.MouseMiddleButtonUp,
            _ => throw new ArgumentException("Unknown system message for mouse")
        }; //GetSystemMessage(mouseButton, keyDown);

        if (!keyDown)
            mouseButton &= (MouseButton)(0x7FFFFFFF ^ (int)MouseButton.LeftButton);
        //SendMessage(windowHandle, SystemMessage.MouseMove, 0, lParam);
        SendMessage(windowHandle, systemMessage, (int)mouseButton, lParam);
    }
    
    [DllImport("user32.dll")]  
    public static extern bool ClipCursor([In] IntPtr lpRect); 
    
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    internal static partial void ShowWindow(IntPtr hwnd, int iCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hHandle);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("User32.Dll")]
    internal static extern long SetCursorPos(int x, int y);
    // public static void SetCursorPos(int x, int y)
    // {
    //     ClipCursor(IntPtr.Zero);
    //     var rect = new Rectangle(x, y, x, y);  
    //     Marshal.StructureToPtr(rect, ClipCursorPointer, false);
    //     ClipCursor(ClipCursorPointer);
    // }

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(SystemMetricsIndex index);

    [DllImport("user32.dll")]
    internal static extern void mouse_event(MouseEventFlags dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    internal static extern void SendMessage(IntPtr hWnd, SystemMessage msg, int wParam, int lParam);

    [LibraryImport("user32.dll")]
    internal static partial void SetForegroundWindow(IntPtr hWnd);
    
    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWindowPos(IntPtr hWnd, SetWindowPosShowFlags hWndInsertAfter, int x, int y, int cx,
        int cy, SetWindowPosFlags wFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    internal static extern void GetWindowRect(IntPtr hWnd, out Rectangle rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [LibraryImport("gdi32.dll")]
    private static partial void BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
        int nXSrc, int nYSrc, uint dwRop);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    private static partial void DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern void GetClientRect(IntPtr hWnd, out Rectangle lpRect);

    private const int SRCCOPY = 0x00CC0020;
    
    private static readonly IntPtr DesktopHandle = GetDesktopWindow();
    private static readonly IntPtr WindowDC = GetWindowDC(DesktopHandle);
    public static Bitmap CaptureScreenshot(int x, int y, int width, int height)
    {
        var hMemDC = CreateCompatibleDC(WindowDC);
        var hBitmap = CreateCompatibleBitmap(WindowDC, width, height);
        var hOld = SelectObject(hMemDC, hBitmap);
        BitBlt(hMemDC, 0, 0, width, height, WindowDC, x, y, SRCCOPY);
        var bmp = Image.FromHbitmap(hBitmap);
        //SelectObject(hMemDC, hOld);
        DeleteObject(hOld);
        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        //ReleaseDC(DesktopHandle, hDC);

        return bmp;
        // using var ms = new MemoryStream();
        // bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        // var imageBytes = ms.ToArray();
        // return imageBytes;
    }

    public static (int, int) GetScreenResolution()
    {
        Rectangle rect;
        GetClientRect(DesktopHandle, out rect);
        return (rect.Right, rect.Bottom);
    }
    
    public static Bitmap CaptureScreenshot(IntPtr hWnd)
    {
        var hDC = GetWindowDC(hWnd);
        
        var hMemDC = CreateCompatibleDC(hDC);
        GetClientRect(hWnd, out Rect);
        var width = Rect.Right;
        var height = Rect.Bottom;
        var hBitmap = CreateCompatibleBitmap(hDC, width, height);
        var hOld = SelectObject(hMemDC, hBitmap);
        BitBlt(hMemDC, 0, 0, width, height, hDC, 0, 0, SRCCOPY);
        var bmp = Image.FromHbitmap(hBitmap);
        SelectObject(hMemDC, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        ReleaseDC(hWnd, hDC);

        return bmp;
    }
}