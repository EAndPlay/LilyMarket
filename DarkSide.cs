using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using Buffer = System.Buffer;
using ImageLockMode = System.Drawing.Imaging.ImageLockMode;

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
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    public enum SystemMessage : uint
    {
        WindowMove = 0x3, // low - x, high - y
        WindowClose = 0x10,
        WindowActivate = 0x6,
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

    public static void SendRawKeyboardKey(IntPtr windowHandle, VirtualKey key, bool keyDown)
    {
        PostMessage(windowHandle, keyDown ? SystemMessage.KeyboardKeyDown : SystemMessage.KeyboardKeyUp, (uint)key, 0);
    }
    
    public static void SendKeyboardKey(IntPtr windowHandle, VirtualKey key, bool keyDown)
    {
        var scanCode = MapVirtualKey(key, 0) << 16;
        PostMessage(windowHandle, keyDown ? SystemMessage.KeyboardKeyDown : SystemMessage.KeyboardKeyUp, (uint)key,
            scanCode | 1);
    }

    public static void SendKeyboardKeyDown(IntPtr windowHandle, VirtualKey key) =>
        SendKeyboardKey(windowHandle, key, true);

    public static void SendKeyboardKeyUp(IntPtr windowHandle, VirtualKey key) =>
        SendKeyboardKey(windowHandle, key, false);

    public static void SendKeyboardKeyPress(IntPtr windowHandle, VirtualKey key)
    {
        var task = Task.Run(async () =>
        {
            SendKeyboardKeyDown(windowHandle, key);
            await Task.Delay(64);
            SendKeyboardKeyUp(windowHandle, key);
        });
        task.Wait();
    }
    
    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GlobalLock(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    public static partial void GlobalUnlock(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    public static partial void GlobalFree(IntPtr hMem);
    
    [LibraryImport("user32.dll")]
    public static partial void EmptyClipboard();
    
    [LibraryImport("user32.dll")]
    public static partial IntPtr GetOpenClipboardWindow();
    
    [LibraryImport("user32.dll")]
    public static partial void OpenClipboard(IntPtr windowHandle);
    
    [LibraryImport("user32.dll")]
    public static partial void CloseClipboard();

    [DllImport("user32.dll")]
    public static extern unsafe int SetClipboardData(uint format, IntPtr dataHandle);
    
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(VirtualKey virtualKey, int mapType);
    
    [DllImport("user32.dll")]
    public static extern void PostMessage(IntPtr windowHandle, SystemMessage message, uint wParam, uint lParam);
    
    [DllImport("user32.dll")]  
    public static extern bool ClipCursor(IntPtr lpRect); 
    
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
    internal static extern IntPtr GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct WinApiBitmapInfo
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }
    
    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out WinApiBitmapInfo lpvObject);

    [LibraryImport("gdi32.dll")]
    internal static partial void BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
        int nXSrc, int nYSrc, uint dwRop);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    internal static partial void DeleteDC(IntPtr hdc);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        // Для 32bpp палитра не нужна, но добавляем padding для совместимости
        public uint bmiColors; // 4 байта padding
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
    
    [LibraryImport("gdi32.dll")]
    internal static partial void GetDIBits(
        nint hdc, 
        nint hbmp, 
        int uStartScan, 
        int cScanLines, 
        nint lpvBits, 
        ref BITMAPINFO lpbmi, 
        uint uUsage
    );

    [DllImport("user32.dll")]
    internal static extern void GetClientRect(nint hWnd, out Rectangle lpRect);

    private const int SRCCOPY = 0x00CC0020;
    private const int ScreenshotWidth = 493; //915
    private const int ScreenshotHeight = 335; // 500 // 615
    
    private static nint DesktopHandle;
    private static nint WindowDC;
    private static nint CompatibleDC;
    private static nint CompatibleBitmap;
    private static nint PixelsBuffer;
    
    private static nint CompatibleDC1;
    private static nint CompatibleBitmap1;
    private static nint PixelsBuffer1;
    private static BITMAPINFO BitmapInfo;
    
    static DarkSide()
    {
        DesktopHandle = GetDesktopWindow();
        WindowDC = GetWindowDC(DesktopHandle);
        CompatibleDC = CreateCompatibleDC(WindowDC);
        CompatibleBitmap = CreateCompatibleBitmap(WindowDC, ScreenshotWidth, ScreenshotHeight);
        SelectObject(CompatibleDC, CompatibleBitmap);
        CompatibleDC1 = CreateCompatibleDC(WindowDC);
        CompatibleBitmap1 = CreateCompatibleBitmap(WindowDC, ScreenshotWidth, ScreenshotHeight);
        SelectObject(CompatibleDC1, CompatibleBitmap1);
        BITMAPINFOHEADER bitmapinfoheader = new()
        {
            biWidth = ScreenshotWidth, biHeight = -ScreenshotHeight,
            biBitCount = 32, biPlanes = 1,
            biSize = 40
        };
        BitmapInfo = new()
        {
            bmiHeader = bitmapinfoheader
        };
        PixelsBuffer = Marshal.AllocHGlobal(ScreenshotWidth * ScreenshotHeight * 4);
        PixelsBuffer1 = Marshal.AllocHGlobal(ScreenshotWidth * ScreenshotHeight * 4);
    }

    internal static void DisposeBitBlt()
    {
        DeleteObject(CompatibleDC);
        DeleteObject(CompatibleBitmap);
        ReleaseDC(DesktopHandle, WindowDC);
    }

    public static Bitmap CaptureRegion(int x, int y, int width, int height)
    {
        const System.Drawing.Imaging.PixelFormat pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;

        BitBlt(CompatibleDC, 0, 0, width, height, WindowDC, x, y, SRCCOPY);
        GetDIBits(CompatibleDC, CompatibleBitmap, 0, ScreenshotHeight, PixelsBuffer, ref BitmapInfo, 0);

        var bitmap = new Bitmap(width, height);
        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);
        width *= 4;

        const int hbitmapStride = ScreenshotWidth * 4;
        var bitmapScan = bitmapData.Scan0;
        Parallel.For(0, height, (row, _) =>
        {
            var source = PixelsBuffer + row * hbitmapStride;
            var target = bitmapScan + row * width;

            unsafe
            {
                Buffer.MemoryCopy((void*)source, (void*)target, width, width);
            }
        });
        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }
    
    public static Bitmap CaptureRegion1(int x, int y, int width, int height)
    {
        const System.Drawing.Imaging.PixelFormat pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;

        BitBlt(CompatibleDC1, 0, 0, width, height, WindowDC, x, y, SRCCOPY);
        GetDIBits(CompatibleDC1, CompatibleBitmap1, 0, ScreenshotHeight, PixelsBuffer1, ref BitmapInfo, 0);

        var bitmap = new Bitmap(width, height);
        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);
        width *= 4;

        const int hbitmapStride = ScreenshotWidth * 4;
        var bitmapScan = bitmapData.Scan0;
        Parallel.For(0, height, (row, _) =>
        {
            var source = PixelsBuffer1 + row * hbitmapStride;
            var target = bitmapScan + row * width;

            unsafe
            {
                Buffer.MemoryCopy((void*)source, (void*)target, width, width);
            }
        });
        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }
    public static byte CapturePixelRed(int x, int y)
    {
        BitBlt(CompatibleDC, 0, 0, 1, 1, WindowDC, x, y, SRCCOPY);
        GetDIBits(CompatibleDC, CompatibleBitmap, 0, ScreenshotHeight, PixelsBuffer, ref BitmapInfo, 0);

        unsafe
        {
            return *((byte*)PixelsBuffer + 1);
        }
    }

    public static (int, int) GetScreenResolution()
    {
        Rectangle rect;
        GetClientRect(DesktopHandle, out rect);
        return (rect.Right, rect.Bottom);
    }
    
    // private static Factory1 _factory;
    // private static Adapter1 _adapter;
    // private static Output _output;
    // private static Output1 _output1;
    // private static Device _device;
    // private static OutputDuplication _duplication;
    // private static Texture2D _stagingTexture;
    // private static int _width;
    // private static int _height;

    // public static void InitDirectX()
    // {
    //     _factory = new Factory1();
    //     _adapter = _factory.GetAdapter1(0);
    //     _device = new Device(_adapter);
    //     _output = _adapter.GetOutput(0);
    //     _output1 = _output.QueryInterface<Output1>();
    //
    //     _width = _output.Description.DesktopBounds.Right;
    //     _height = _output.Description.DesktopBounds.Bottom;
    //
    //     var textureDesc = new Texture2DDescription
    //     {
    //         CpuAccessFlags = CpuAccessFlags.Read, //None
    //         BindFlags = BindFlags.None,//BindFlags.RenderTarget | BindFlags.ShaderResource,
    //         Format = Format.B8G8R8A8_UNorm,
    //         Width = _width,
    //         Height = _height,
    //         OptionFlags = ResourceOptionFlags.None, //,Shared
    //         MipLevels = 1,
    //         ArraySize = 1,
    //         SampleDescription = { Count = 1, Quality = 0 },
    //         Usage = ResourceUsage.Staging
    //     };
    //     _stagingTexture = new Texture2D(_device, textureDesc);
    //     _duplication = _output1.DuplicateOutput(_device);
    // }
    
    // public static Bitmap CaptureRegion(int x, int y, int width, int height)
    // { 
    //     const System.Drawing.Imaging.PixelFormat pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
    //     
    //     var bmp = new Bitmap(width, height, pixelFormat);
    //     width *= 4;
    //     Resource? screenResource = null;
    //     
    //     try
    //     {
    //         if (_duplication.TryAcquireNextFrame(10, out OutputDuplicateFrameInformation duplicateFrameInformation, out screenResource) != Result.Ok)
    //             return bmp;
    //         
    //         //var box = new ResourceRegion(x, y, 0, x + width, y + height, 1);
    //         using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
    //         {
    //             //_device.ImmediateContext.CopySubresourceRegion(screenTexture2D, 0, box, _stagingTexture, 0, 0, 0, 0);
    //             _device.ImmediateContext.CopyResource(screenTexture2D, _stagingTexture);
    //         }
    //
    //         var mapSource = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
    //         var bitmapData = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.WriteOnly, bmp.PixelFormat);
    //         var sourcePtr = mapSource.DataPointer;
    //         var rowPitch = mapSource.RowPitch;
    //         var bitmapScan = bitmapData.Scan0;
    //         var bitmapStride = bitmapData.Stride;
    //         Parallel.For(0, height, (row, _) =>
    //         {
    //             var sourceY = y + row;
    //             var sourceOffset = rowPitch * sourceY + x * 4;
    //             var targetOffset = row * bitmapStride;
    //
    //             Utilities.CopyMemory(
    //                 bitmapScan + targetOffset,
    //                 sourcePtr + sourceOffset,
    //                 width
    //             );
    //         });
    //         bmp.UnlockBits(bitmapData);
    //         _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
    //         _duplication.ReleaseFrame();
    //     }
    //     catch (SharpDXException ex)
    //     {
    //         Console.WriteLine(ex.Message);
    //     }
    //     finally
    //     {
    //         screenResource?.Dispose();
    //     }
    //     return bmp;
    // }
    //
    // public static byte CapturePixelRed(int x, int y)
    // {
    //     byte red = 0;
    //     Resource? screenResource = null;
    //     
    //     try
    //     {
    //         if (_duplication.TryAcquireNextFrame(10, out OutputDuplicateFrameInformation duplicateFrameInformation, out screenResource) != Result.Ok)
    //             return 0;
    //
    //         using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
    //         {
    //             _device.ImmediateContext.CopyResource(screenTexture2D, _stagingTexture);
    //         }
    //         var mapSource = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
    //
    //         unsafe
    //         {
    //             // Never optimize pixelData to int*, it breaks compiler)
    //             var pixelData = (byte*)mapSource.DataPointer + y * mapSource.RowPitch + x * 4 + 1;
    //             red = *pixelData;
    //         }
    //         
    //         _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
    //         _duplication.ReleaseFrame();
    //     }
    //     catch (SharpDXException ex)
    //     {
    //         Console.WriteLine(ex.Message);
    //     }
    //     finally
    //     {
    //         screenResource?.Dispose();
    //     }
    //
    //     return red;
    // }
}