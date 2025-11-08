using System.Runtime.InteropServices;
using System.Reflection;
using System.ComponentModel;

namespace LilyMarket
{
    public enum MouseButtonState
    {
        /// <summary>The button is released.</summary>
        Released,
        /// <summary>The button is pressed.</summary>
        Pressed,
    }
    public enum MouseButton
    {
        /// <summary>The left mouse button.</summary>
        Left,
        /// <summary>The middle mouse button.</summary>
        Middle,
        /// <summary>The right mouse button.</summary>
        Right,
        /// <summary>The first extended mouse button.</summary>
        XButton1,
        /// <summary>The second extended mouse button.</summary>
        XButton2,
    }
    /// <summary>
    /// This class allows you to tap keyboard and mouse and / or to detect their activity even when an 
    /// application runes in background or does not have any user interface at all. This class raises 
    /// common .NET events with KeyEventArgs and MouseEventArgs so you can easily retrive any information you need.
    /// </summary>
    
    public class UserActivityHook
    {
        #region Windows structure definitions

        /// <summary>
        /// The POINT structure defines the x- and y- coordinates of a point. 
        /// </summary>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/gdi/rectangl_0tiq.asp
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            /// <summary>
            /// Specifies the x-coordinate of the point. 
            /// </summary>
            public int x;

            /// <summary>
            /// Specifies the y-coordinate of the point. 
            /// </summary>
            public int y;
        }

        /// <summary>
        /// The MOUSEHOOKSTRUCT structure contains information about a mouse event passed to a WH_MOUSE hook procedure, MouseProc. 
        /// </summary>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookstructures/cwpstruct.asp
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookStruct
        {
            /// <summary>
            /// Specifies a POINT structure that contains the x- and y-coordinates of the cursor, in screen coordinates. 
            /// </summary>
            public Point Point;

            /// <summary>
            /// Handle to the window that will receive the mouse message corresponding to the mouse event. 
            /// </summary>
            public int hwnd;

            /// <summary>
            /// Specifies the hit-test value. For a list of hit-test values, see the description of the WM_NCHITTEST message. 
            /// </summary>
            public int wHitTestCode;

            /// <summary>
            /// Specifies extra information associated with the message. 
            /// </summary>
            public int dwExtraInfo;
        }

        /// <summary>
        /// The MSLLHOOKSTRUCT structure contains information about a low-level keyboard input event. 
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseLLHookStruct
        {
            /// <summary>
            /// Specifies a POINT structure that contains the x- and y-coordinates of the cursor, in screen coordinates. 
            /// </summary>
            public Point Point;

            /// <summary>
            /// If the message is WM_MOUSEWHEEL, the high-order word of this member is the wheel delta. 
            /// The low-order word is reserved. A positive value indicates that the wheel was rotated forward, 
            /// away from the user; a negative value indicates that the wheel was rotated backward, toward the user. 
            /// One wheel click is defined as WHEEL_DELTA, which is 120. 
            ///If the message is WM_XBUTTONDOWN, WM_XBUTTONUP, WM_XBUTTONDBLCLK, WM_NCXBUTTONDOWN, WM_NCXBUTTONUP,
            /// or WM_NCXBUTTONDBLCLK, the high-order word specifies which X button was pressed or released, 
            /// and the low-order word is reserved. This value can be one or more of the following values. Otherwise, mouseData is not used. 
            ///XBUTTON1
            ///The first X button was pressed or released.
            ///XBUTTON2
            ///The second X button was pressed or released.
            /// </summary>
            public int mouseData;

            /// <summary>
            /// Specifies the event-injected flag. An application can use the following value to test the mouse flags. Value Purpose 
            ///LLMHF_INJECTED Test the event-injected flag.  
            ///0
            ///Specifies whether the event was injected. The value is 1 if the event was injected; otherwise, it is 0.
            ///1-15
            ///Reserved.
            /// </summary>
            public int flags;

            /// <summary>
            /// Specifies the time stamp for this message.
            /// </summary>
            public int time;

            /// <summary>
            /// Specifies extra information associated with the message. 
            /// </summary>
            public int dwExtraInfo;
        }


        /// <summary>
        /// The KBDLLHOOKSTRUCT structure contains information about a low-level keyboard input event. 
        /// </summary>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookstructures/cwpstruct.asp
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookStruct
        {
            /// <summary>
            /// Specifies a virtual-key code. The code must be a value in the range 1 to 254. 
            /// </summary>
            public int vkCode;

            /// <summary>
            /// Specifies a hardware scan code for the key. 
            /// </summary>
            public int scanCode;

            /// <summary>
            /// Specifies the extended-key flag, event-injected flag, context code, and transition-state flag.
            /// </summary>
            public int flags;

            /// <summary>
            /// Specifies the time stamp for this message.
            /// </summary>
            private int time;

            /// <summary>
            /// Specifies extra information associated with the message. 
            /// </summary>
            private int dwExtraInfo;
        }

        #endregion

        #region Windows function imports

        /// <summary>
        /// The SetWindowsHookEx function installs an application-defined hook procedure into a hook chain. 
        /// You would install a hook procedure to monitor the system for certain types of events. These events 
        /// are associated either with a specific thread or with all threads in the same desktop as the calling thread. 
        /// </summary>
        /// <param name="idHook">
        /// [in] Specifies the type of hook procedure to be installed. This parameter can be one of the following values.
        /// </param>
        /// <param name="lpfn">
        /// [in] Pointer to the hook procedure. If the dwThreadId parameter is zero or specifies the identifier of a 
        /// thread created by a different process, the lpfn parameter must point to a hook procedure in a dynamic-link 
        /// library (DLL). Otherwise, lpfn can point to a hook procedure in the code associated with the current process.
        /// </param>
        /// <param name="hMod">
        /// [in] Handle to the DLL containing the hook procedure pointed to by the lpfn parameter. 
        /// The hMod parameter must be set to NULL if the dwThreadId parameter specifies a thread created by 
        /// the current process and if the hook procedure is within the code associated with the current process. 
        /// </param>
        /// <param name="dwThreadId">
        /// [in] Specifies the identifier of the thread with which the hook procedure is to be associated. 
        /// If this parameter is zero, the hook procedure is associated with all existing threads running in the 
        /// same desktop as the calling thread. 
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is the handle to the hook procedure.
        /// If the function fails, the return value is NULL. To get extended error information, call GetLastError.
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
        /// </remarks>
        [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int SetWindowsHookEx(
            int idHook,
            HookProcedure lpfn,
            IntPtr hMod,
            int dwThreadId);

        /// <summary>
        /// The UnhookWindowsHookEx function removes a hook procedure installed in a hook chain by the SetWindowsHookEx function. 
        /// </summary>
        /// <param name="idHook">
        /// [in] Handle to the hook to be removed. This parameter is a hook handle obtained by a previous call to SetWindowsHookEx. 
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is nonzero.
        /// If the function fails, the return value is zero. To get extended error information, call GetLastError.
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
        /// </remarks>
        [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int idHook);

        /// <summary>
        /// The CallNextHookEx function passes the hook information to the next hook procedure in the current hook chain. 
        /// A hook procedure can call this function either before or after processing the hook information. 
        /// </summary>
        /// <param name="idHook">Ignored.</param>
        /// <param name="nCode">
        /// [in] Specifies the hook code passed to the current hook procedure. 
        /// The next hook procedure uses this code to determine how to process the hook information.
        /// </param>
        /// <param name="wParam">
        /// [in] Specifies the wParam value passed to the current hook procedure. 
        /// The meaning of this parameter depends on the type of hook associated with the current hook chain. 
        /// </param>
        /// <param name="lParam">
        /// [in] Specifies the lParam value passed to the current hook procedure. 
        /// The meaning of this parameter depends on the type of hook associated with the current hook chain. 
        /// </param>
        /// <returns>
        /// This value is returned by the next hook procedure in the chain. 
        /// The current hook procedure must also return this value. The meaning of the return value depends on the hook type. 
        /// For more information, see the descriptions of the individual hook procedures.
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
        /// </remarks>
        [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(
            int idHook,
            int nCode,
            int wParam,
            IntPtr lParam);

        /// <summary>
        /// The CallWndProc hook procedure is an application-defined or library-defined callback 
        /// function used with the SetWindowsHookEx function. The HOOKPROC type defines a pointer 
        /// to this callback function. CallWndProc is a placeholder for the application-defined 
        /// or library-defined function name.
        /// </summary>
        /// <param name="nCode">
        /// [in] Specifies whether the hook procedure must process the message. 
        /// If nCode is HC_ACTION, the hook procedure must process the message. 
        /// If nCode is less than zero, the hook procedure must pass the message to the 
        /// CallNextHookEx function without further processing and must return the 
        /// value returned by CallNextHookEx.
        /// </param>
        /// <param name="wParam">
        /// [in] Specifies whether the message was sent by the current thread. 
        /// If the message was sent by the current thread, it is nonzero; otherwise, it is zero. 
        /// </param>
        /// <param name="lParam">
        /// [in] Pointer to a CWPSTRUCT structure that contains details about the message. 
        /// </param>
        /// <returns>
        /// If nCode is less than zero, the hook procedure must return the value returned by CallNextHookEx. 
        /// If nCode is greater than or equal to zero, it is highly recommended that you call CallNextHookEx 
        /// and return the value it returns; otherwise, other applications that have installed WH_CALLWNDPROC 
        /// hooks will not receive hook notifications and may behave incorrectly as a result. If the hook 
        /// procedure does not call CallNextHookEx, the return value should be zero. 
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/callwndproc.asp
        /// </remarks>
        private delegate int HookProcedure(int nCode, int wParam, IntPtr lParam);

        /// <summary>
        /// The ToAscii function translates the specified virtual-key code and keyboard 
        /// state to the corresponding character or characters. The function translates the code 
        /// using the input language and physical keyboard layout identified by the keyboard layout handle.
        /// </summary>
        /// <param name="uVirtKey">
        /// [in] Specifies the virtual-key code to be translated. 
        /// </param>
        /// <param name="uScanCode">
        /// [in] Specifies the hardware scan code of the key to be translated. 
        /// The high-order bit of this value is set if the key is up (not pressed). 
        /// </param>
        /// <param name="lpbKeyState">
        /// [in] Pointer to a 256-byte array that contains the current keyboard state. 
        /// Each element (byte) in the array contains the state of one key. 
        /// If the high-order bit of a byte is set, the key is down (pressed). 
        /// The low bit, if set, indicates that the key is toggled on. In this function, 
        /// only the toggle bit of the CAPS LOCK key is relevant. The toggle state 
        /// of the NUM LOCK and SCROLL LOCK keys is ignored.
        /// </param>
        /// <param name="lpwTransKey">
        /// [out] Pointer to the buffer that receives the translated character or characters. 
        /// </param>
        /// <param name="fuState">
        /// [in] Specifies whether a menu is active. This parameter must be 1 if a menu is active, or 0 otherwise. 
        /// </param>
        /// <returns>
        /// If the specified key is a dead key, the return value is negative. Otherwise, it is one of the following values. 
        /// Value Meaning 
        /// 0 The specified virtual key has no translation for the current state of the keyboard. 
        /// 1 One character was copied to the buffer. 
        /// 2 Two characters were copied to the buffer. This usually happens when a dead-key character 
        /// (accent or diacritic) stored in the keyboard layout cannot be composed with the specified 
        /// virtual key to form a single character. 
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/userinput/keyboardinput/keyboardinputreference/keyboardinputfunctions/toascii.asp
        /// </remarks>
        [DllImport("user32")]
        private static extern int ToAscii(
            int uVirtKey,
            int uScanCode,
            byte[] lpbKeyState,
            byte[] lpwTransKey,
            int fuState);

        /// <summary>
        /// The GetKeyboardState function copies the status of the 256 virtual keys to the 
        /// specified buffer. 
        /// </summary>
        /// <param name="pbKeyState">
        /// [in] Pointer to a 256-byte array that contains keyboard key states. 
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is nonzero.
        /// If the function fails, the return value is zero. To get extended error information, call GetLastError. 
        /// </returns>
        /// <remarks>
        /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/userinput/keyboardinput/keyboardinputreference/keyboardinputfunctions/toascii.asp
        /// </remarks>
        [DllImport("user32")]
        private static extern int GetKeyboardState(byte[] pbKeyState);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern short GetKeyState(int vKey);

        #endregion

        #region Windows constants

        //values from Winuser.h in Microsoft SDK.
        /// <summary>
        /// Windows NT/2000/XP: Installs a hook procedure that monitors low-level mouse input events.
        /// </summary>
        private const int WH_MOUSE_LL = 14;

        /// <summary>
        /// Windows NT/2000/XP: Installs a hook procedure that monitors low-level keyboard  input events.
        /// </summary>
        private const int WH_KEYBOARD_LL = 13;

        /// <summary>
        /// Installs a hook procedure that monitors mouse messages. For more information, see the MouseProc hook procedure. 
        /// </summary>
        private const int WH_MOUSE = 7;

        /// <summary>
        /// Installs a hook procedure that monitors keystroke messages. For more information, see the KeyboardProc hook procedure. 
        /// </summary>
        private const int WH_KEYBOARD = 2;

        /// <summary>
        /// The WM_MOUSEMOVE message is posted to a window when the cursor moves. 
        /// </summary>
        private const int WM_MOUSEMOVE = 0x200;

        /// <summary>
        /// The WM_LBUTTONDOWN message is posted when the user presses the left mouse button 
        /// </summary>
        private const int WM_LBUTTONDOWN = 0x201;

        /// <summary>
        /// The WM_RBUTTONDOWN message is posted when the user presses the right mouse button
        /// </summary>
        private const int WM_RBUTTONDOWN = 0x204;

        /// <summary>
        /// The WM_MBUTTONDOWN message is posted when the user presses the middle mouse button 
        /// </summary>
        private const int WM_MBUTTONDOWN = 0x207;

        /// <summary>
        /// The WM_LBUTTONUP message is posted when the user releases the left mouse button 
        /// </summary>
        private const int WM_LBUTTONUP = 0x202;

        /// <summary>
        /// The WM_RBUTTONUP message is posted when the user releases the right mouse button 
        /// </summary>
        private const int WM_RBUTTONUP = 0x205;

        /// <summary>
        /// The WM_MBUTTONUP message is posted when the user releases the middle mouse button 
        /// </summary>
        private const int WM_MBUTTONUP = 0x208;

        /// <summary>
        /// The WM_LBUTTONDBLCLK message is posted when the user double-clicks the left mouse button 
        /// </summary>
        private const int WM_LBUTTONDBLCLK = 0x203;

        /// <summary>
        /// The WM_RBUTTONDBLCLK message is posted when the user double-clicks the right mouse button 
        /// </summary>
        private const int WM_RBUTTONDBLCLK = 0x206;

        /// <summary>
        /// The WM_RBUTTONDOWN message is posted when the user presses the right mouse button 
        /// </summary>
        private const int WM_MBUTTONDBLCLK = 0x209;

        /// <summary>
        /// The WM_MOUSEWHEEL message is posted when the user presses the mouse wheel. 
        /// </summary>
        private const int WM_MOUSEWHEEL = 0x020A;

        /// <summary>
        /// The WM_KEYDOWN message is posted to the window with the keyboard focus when a nonsystem 
        /// key is pressed. A nonsystem key is a key that is pressed when the ALT key is not pressed.
        /// </summary>
        private const int WM_KEYDOWN = 0x100;

        /// <summary>
        /// The WM_KEYUP message is posted to the window with the keyboard focus when a nonsystem 
        /// key is released. A nonsystem key is a key that is pressed when the ALT key is not pressed, 
        /// or a keyboard key that is pressed when a window has the keyboard focus.
        /// </summary>
        private const int WM_KEYUP = 0x101;

        /// <summary>
        /// The WM_SYSKEYDOWN message is posted to the window with the keyboard focus when the user 
        /// presses the F10 key (which activates the menu bar) or holds down the ALT key and then 
        /// presses another key. It also occurs when no window currently has the keyboard focus; 
        /// in this case, the WM_SYSKEYDOWN message is sent to the active window. The window that 
        /// receives the message can distinguish between these two contexts by checking the context 
        /// code in the lParam parameter. 
        /// </summary>
        private const int WM_SYSKEYDOWN = 0x104;

        /// <summary>
        /// The WM_SYSKEYUP message is posted to the window with the keyboard focus when the user 
        /// releases a key that was pressed while the ALT key was held down. It also occurs when no 
        /// window currently has the keyboard focus; in this case, the WM_SYSKEYUP message is sent 
        /// to the active window. The window that receives the message can distinguish between 
        /// these two contexts by checking the context code in the lParam parameter. 
        /// </summary>
        private const int WM_SYSKEYUP = 0x105;

        private const byte VK_SHIFT = 0x10;
        private const byte VK_CAPITAL = 0x14;
        private const byte VK_NUMLOCK = 0x90;

        #endregion

        /// <summary>
        /// Creates an instance of UserActivityHook object and sets mouse and keyboard hooks.
        /// </summary>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        public UserActivityHook() => Start();

        /// <summary>
        /// Creates an instance of UserActivityHook object and installs both or one of mouse and/or keyboard hooks and starts rasing events
        /// </summary>
        /// <param name="installMouseHook"><b>true</b> if mouse events must be monitored</param>
        /// <param name="installKeyboardHook"><b>true</b> if keyboard events must be monitored</param>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        /// <remarks>
        /// To create an instance without installing hooks call new UserActivityHook(false, false)
        /// </remarks>
        public UserActivityHook(bool installMouseHook, bool installKeyboardHook)
        {
            Start(installMouseHook, installKeyboardHook);
        }

        /// <summary>
        /// Destruction.
        /// </summary>
        ~UserActivityHook()
        {
            //uninstall hooks and do not throw exceptions
            Stop(true, true, false);
        }

        public class CustomMouseButtonEvent
        {
            public MouseButton Button;
            public MouseButtonState ButtonState;
            public Point Point;
        }
        public delegate void MouseEventHandler(ref CustomMouseButtonEvent args);
        //public delegate void MouseEventHandler(ref MouseButtonEventArgs args);
        public delegate void KeyEventHandler(VirtualKey key);
        /// <summary>
        /// Occurs when the user moves the mouse, presses any mouse button or scrolls the wheel
        /// </summary>
        public event MouseEventHandler OnMouseActivity;

        /// <summary>
        /// Occurs when the user presses a key
        /// </summary>
        public event KeyEventHandler KeyDown;

        /// <summary>
        /// Occurs when the user presses and releases 
        /// </summary>
        public event KeyEventHandler KeyPress;

        /// <summary>
        /// Occurs when the user releases a key
        /// </summary>
        public event KeyEventHandler KeyUp;


        /// <summary>
        /// Stores the handle to the mouse hook procedure.
        /// </summary>
        private int _hMouseHook = 0;

        /// <summary>
        /// Stores the handle to the keyboard hook procedure.
        /// </summary>
        private int _hKeyboardHook = 0;
        
        /// <summary>
        /// Declare MouseHookProcedure as HookProc type.
        /// </summary>
        private static HookProcedure _mouseHookProcedure;

        /// <summary>
        /// Declare KeyboardHookProcedure as HookProc type.
        /// </summary>
        private static HookProcedure _keyboardHookProcedure;

        /// <summary>
        /// Points to the program assembly.
        /// </summary>
        private readonly IntPtr _modulePointer = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
        
        /// <summary>
        /// Installs both mouse and keyboard hooks and starts rasing events
        /// </summary>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        public void Start() => Start(true, true);

        /// <summary>
        /// Installs both or one of mouse and/or keyboard hooks and starts rasing events
        /// </summary>
        /// <param name="hookMouse"><b>true</b> if mouse events must be monitored</param>
        /// <param name="hookKeyboard"><b>true</b> if keyboard events must be monitored</param>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        public void Start(bool hookMouse, bool hookKeyboard)
        {
            // install Mouse hook only if it is not installed and must be installed
            if (_hMouseHook == 0 && hookMouse)
            {
                // Create an instance of HookProc.
                _mouseHookProcedure = MouseHookProc;
                //install hook
                _hMouseHook = SetWindowsHookEx(
                    WH_MOUSE_LL,
                    _mouseHookProcedure,
                    _modulePointer,
                    0);
                //If SetWindowsHookEx fails.
                if (_hMouseHook == 0)
                {
                    //Returns the error code returned by the last unmanaged function called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    var errorCode = Marshal.GetLastWin32Error();
                    //do cleanup
                    Stop(true, false, false);
                    //Initializes and throws a new instance of the Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }
            }

            // install Keyboard hook only if it is not installed and must be installed
            if (_hKeyboardHook == 0 && hookKeyboard)
            {
                // Create an instance of HookProc.
                _keyboardHookProcedure = KeyboardHookProc;
                //install hook
                _hKeyboardHook = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    _keyboardHookProcedure,
                    _modulePointer,
                    0);
                //If SetWindowsHookEx fails.
                if (_hKeyboardHook == 0)
                {
                    //Returns the error code returned by the last unmanaged function called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    var errorCode = Marshal.GetLastWin32Error();
                    //do cleanup
                    Stop(false, true, false);
                    //Initializes and throws a new instance of the Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }
            }
        }

        /// <summary>
        /// Stops monitoring both mouse and keyboard events and rasing events.
        /// </summary>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        public void Stop() => Stop(true, true, true);

        /// <summary>
        /// Stops monitoring both or one of mouse and/or keyboard events and rasing events.
        /// </summary>
        /// <param name="unhookMouse"><b>true</b> if mouse hook must be uninstalled</param>
        /// <param name="unhookKeyboard"><b>true</b> if keyboard hook must be uninstalled</param>
        /// <param name="throwExceptions"><b>true</b> if exceptions which occured during uninstalling must be thrown</param>
        /// <exception cref="Win32Exception">Any windows problem.</exception>
        public void Stop(bool unhookMouse, bool unhookKeyboard, bool throwExceptions)
        {
            //if mouse hook set and must be uninstalled
            if (_hMouseHook != 0 && unhookMouse)
            {
                //uninstall hook
                var retMouse = UnhookWindowsHookEx(_hMouseHook);
                //reset invalid handle
                _hMouseHook = 0;
                //if failed and exception must be thrown
                if (retMouse == 0 && throwExceptions)
                {
                    //Returns the error code returned by the last unmanaged function called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    var errorCode = Marshal.GetLastWin32Error();
                    //Initializes and throws a new instance of the Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }
            }

            //if keyboard hook set and must be uninstalled
            if (_hKeyboardHook != 0 && unhookKeyboard)
            {
                //uninstall hook
                var retKeyboard = UnhookWindowsHookEx(_hKeyboardHook);
                //reset invalid handle
                _hKeyboardHook = 0;
                //if failed and exception must be thrown
                if (retKeyboard == 0 && throwExceptions)
                {
                    //Returns the error code returned by the last unmanaged function called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    var errorCode = Marshal.GetLastWin32Error();
                    //Initializes and throws a new instance of the Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }
            }
        }


        /// <summary>
        /// A callback function which will be called every time a mouse activity detected.
        /// </summary>
        /// <param name="nCode">
        /// [in] Specifies whether the hook procedure must process the message. 
        /// If nCode is HC_ACTION, the hook procedure must process the message. 
        /// If nCode is less than zero, the hook procedure must pass the message to the 
        /// CallNextHookEx function without further processing and must return the 
        /// value returned by CallNextHookEx.
        /// </param>
        /// <param name="wParam">
        /// [in] Specifies whether the message was sent by the current thread. 
        /// If the message was sent by the current thread, it is nonzero; otherwise, it is zero. 
        /// </param>
        /// <param name="lParam">
        /// [in] Pointer to a CWPSTRUCT structure that contains details about the message. 
        /// </param>
        /// <returns>
        /// If nCode is less than zero, the hook procedure must return the value returned by CallNextHookEx. 
        /// If nCode is greater than or equal to zero, it is highly recommended that you call CallNextHookEx 
        /// and return the value it returns; otherwise, other applications that have installed WH_CALLWNDPROC 
        /// hooks will not receive hook notifications and may behave incorrectly as a result. If the hook 
        /// procedure does not call CallNextHookEx, the return value should be zero. 
        /// </returns>
        private int MouseHookProc(int nCode, int wParam, IntPtr lParam)
        {
            MouseButton GetButton(Int32 wParam)
            {
                switch (wParam)
                {
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONUP:
                    case WM_LBUTTONDBLCLK:
                        return MouseButton.Left;
                    case WM_RBUTTONDOWN:
                    case WM_RBUTTONUP:
                    case WM_RBUTTONDBLCLK:
                        return MouseButton.Right;
                    case WM_MBUTTONDOWN:
                    case WM_MBUTTONUP:
                    case WM_MBUTTONDBLCLK:
                        return MouseButton.Middle;
                }

                return MouseButton.Middle;
            }
            
            MouseButtonState GetEventType(Int32 wParam)
            {
                switch (wParam)
                {
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        return MouseButtonState.Pressed;
                    case WM_LBUTTONUP:
                    case WM_RBUTTONUP:
                    case WM_MBUTTONUP:
                        return MouseButtonState.Released;
                }

                return MouseButtonState.Released;
            }
            
            if (nCode < 0 || OnMouseActivity == null) return CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
            MouseLLHookStruct mouseHookStruct = (MouseLLHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseLLHookStruct));
            
            //detect button clicked
            var button = GetButton(wParam);
            var state = GetEventType(wParam);
            short mouseDelta = 0;
            switch (wParam)
            {
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                    state = MouseButtonState.Pressed;
                    break;
            }
            switch (wParam)
            {
                case WM_LBUTTONUP:
                case WM_RBUTTONUP:
                    state = MouseButtonState.Released;
                    break;
            }
            
            // switch (wParam)
            // {
            //     case WM_LBUTTONDOWN:
            //         //case WM_LBUTTONUP: 
            //         //case WM_LBUTTONDBLCLK: 
            //         button = MouseButton.Left;
            //         break;
            //     case WM_RBUTTONDOWN:
            //         //case WM_RBUTTONUP: 
            //         //case WM_RBUTTONDBLCLK: 
            //         button = MouseButton.Right;
            //         break;
            //     //case WM_MOUSEWHEEL:
            //         //If the message is WM_MOUSEWHEEL, the high-order word of mouseData member is the wheel delta. 
            //         //One wheel click is defined as WHEEL_DELTA, which is 120. 
            //         //(value >> 16) & 0xffff; retrieves the high-order word from the given 32-bit value
            //         mouseDelta = (short)((mouseHookStruct.mouseData >> 16) & 0xffff);
            //         //TODO: X BUTTONS (I havent them so was unable to test)
            //         //If the message is WM_XBUTTONDOWN, WM_XBUTTONUP, WM_XBUTTONDBLCLK, WM_NCXBUTTONDOWN, WM_NCXBUTTONUP, 
            //         //or WM_NCXBUTTONDBLCLK, the high-order word specifies which X button was pressed or released, 
            //         //and the low-order word is reserved. This value can be one or more of the following values. 
            //         //Otherwise, mouseData is not used. 
            //         break;
            // }

            //MessageBox.Show(Convert.ToString(mouseHookStruct.flags, 2));

            //double clicks
            // int clickCount = 0;
            // if (button != default)
            //     if (wParam == WM_LBUTTONDBLCLK || wParam == WM_RBUTTONDBLCLK) clickCount = 2;
            //     else clickCount = 1;

            //generate event
            //var e = new MouseEventArgs(Mouse.PrimaryDevice, 0);
            //var e = new MouseEventArgs(button, clickCount, mouseHookStruct.Point.x, mouseHookStruct.Point.y, mouseDelta);
            //var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button);
            var args = new CustomMouseButtonEvent
            {
                Button = button,
                ButtonState = state,
                Point = mouseHookStruct.Point
            };
            OnMouseActivity(ref args);

            //call next hook
            return CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
        }
        
        /// <summary>
        /// A callback function which will be called every time a keyboard activity detected.
        /// </summary>
        /// <param name="nCode">
        /// [in] Specifies whether the hook procedure must process the message. 
        /// If nCode is HC_ACTION, the hook procedure must process the message. 
        /// If nCode is less than zero, the hook procedure must pass the message to the 
        /// CallNextHookEx function without further processing and must return the 
        /// value returned by CallNextHookEx.
        /// </param>
        /// <param name="wParam">
        /// [in] Specifies whether the message was sent by the current thread. 
        /// If the message was sent by the current thread, it is nonzero; otherwise, it is zero. 
        /// </param>
        /// <param name="lParam">
        /// [in] Pointer to a CWPSTRUCT structure that contains details about the message. 
        /// </param>
        /// <returns>
        /// If nCode is less than zero, the hook procedure must return the value returned by CallNextHookEx. 
        /// If nCode is greater than or equal to zero, it is highly recommended that you call CallNextHookEx 
        /// and return the value it returns; otherwise, other applications that have installed WH_CALLWNDPROC 
        /// hooks will not receive hook notifications and may behave incorrectly as a result. If the hook 
        /// procedure does not call CallNextHookEx, the return value should be zero. 
        /// </returns>
        private int KeyboardHookProc(int nCode, int wParam, IntPtr lParam)
        {
            //var handled = false;
            if (nCode < 0 || KeyDown == null && KeyUp == null && KeyPress == null)
                return CallNextHookEx(_hKeyboardHook, nCode, wParam, lParam);
            
            var myKeyboardHookStruct = ((KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct)));
            if (KeyDown != null && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                // var keyData = KeyInterop.KeyFromVirtualKey(myKeyboardHookStruct.vkCode);
                // var args = new KeyEventArgs(_keyboardDevice,
                //     Source, 0, keyData);
                KeyDown((VirtualKey)myKeyboardHookStruct.vkCode);
                //handled = args.Handled;
            }

            if (KeyPress != null && wParam == WM_KEYDOWN)
            {
                var isDownShift = (GetKeyState(VK_SHIFT) & 0x80) == 0x80;
                var isDownCapslock = GetKeyState(VK_CAPITAL) != 0;

                var keyState = new byte[256];
                GetKeyboardState(keyState);
                var inBuffer = new byte[1];
                if (ToAscii(myKeyboardHookStruct.vkCode,
                    myKeyboardHookStruct.scanCode,
                    keyState,
                    inBuffer,
                    myKeyboardHookStruct.flags) == 1)
                {
                    Array.Clear(keyState, 0, keyState.Length);
                    var key = (char) inBuffer[0];
                    if (isDownCapslock ^ isDownShift && char.IsLetter(key)) 
                        key = char.ToUpper(key);
                    // var args = new KeyEventArgs(_keyboardDevice,
                    //     Source, 0, KeyInterop.KeyFromVirtualKey(key));
                    KeyPress((VirtualKey)key);
                    //handled = handled || args.Handled;
                }
            }

            if (KeyUp != null && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
            {
                //var keyData = KeyInterop.KeyFromVirtualKey(myKeyboardHookStruct.vkCode);
                // var args = new KeyEventArgs(_keyboardDevice,
                //     Source, 0, keyData);
                KeyUp((VirtualKey)myKeyboardHookStruct.vkCode);
                //handled = handled || args.Handled;
            }

            //return handled ? 1 : CallNextHookEx(_hKeyboardHook, nCode, wParam, lParam);
            return CallNextHookEx(_hKeyboardHook, nCode, wParam, lParam);
        }
    }
    public enum VirtualKey : ushort
    {
        LeftButton = 0x01,
        RightButton = 0x02,
        Cancel = 0x03,
        MiddleButton = 0x04,
        ExtraButton1 = 0x05,
        ExtraButton2 = 0x06,
        Back = 0x08,
        Tab = 0x09,
        Clear = 0x0C,
        Return = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        /// <summary></summary>
        Menu = 0x12,
        /// <summary></summary>
        Pause = 0x13,
        /// <summary></summary>
        CapsLock = 0x14,
        /// <summary></summary>
        Kana = 0x15,
        /// <summary></summary>
        Hangeul = 0x15,
        /// <summary></summary>
        Hangul = 0x15,
        /// <summary></summary>
        Junja = 0x17,
        /// <summary></summary>
        Final = 0x18,
        /// <summary></summary>
        Hanja = 0x19,
        /// <summary></summary>
        Kanji = 0x19,
        /// <summary></summary>
        Escape = 0x1B,
        /// <summary></summary>
        Convert = 0x1C,
        /// <summary></summary>
        NonConvert = 0x1D,
        /// <summary></summary>
        Accept = 0x1E,
        /// <summary></summary>
        ModeChange = 0x1F,
        /// <summary></summary>
        Space = 0x20,
        /// <summary></summary>
        Prior = 0x21,
        /// <summary></summary>
        Next = 0x22,
        /// <summary></summary>
        End = 0x23,
        /// <summary></summary>
        Home = 0x24,
        /// <summary></summary>
        Left = 0x25,
        /// <summary></summary>
        Up = 0x26,
        /// <summary></summary>
        Right = 0x27,
        /// <summary></summary>
        Down = 0x28,
        /// <summary></summary>
        Select = 0x29,
        /// <summary></summary>
        Print = 0x2A,
        /// <summary></summary>
        Execute = 0x2B,
        /// <summary></summary>
        Snapshot = 0x2C,
        /// <summary></summary>
        Insert = 0x2D,
        /// <summary></summary>
        Delete = 0x2E,
        /// <summary></summary>
        Help = 0x2F,
        /// <summary></summary>
        N0 = 0x30,
        /// <summary></summary>
        N1 = 0x31,
        /// <summary></summary>
        N2 = 0x32,
        /// <summary></summary>
        N3 = 0x33,
        /// <summary></summary>
        N4 = 0x34,
        /// <summary></summary>
        N5 = 0x35,
        /// <summary></summary>
        N6 = 0x36,
        /// <summary></summary>
        N7 = 0x37,
        /// <summary></summary>
        N8 = 0x38,
        /// <summary></summary>
        N9 = 0x39,
        /// <summary></summary>
        A = 0x41,
        /// <summary></summary>
        B = 0x42,
        /// <summary></summary>
        C = 0x43,
        /// <summary></summary>
        D = 0x44,
        /// <summary></summary>
        E = 0x45,
        /// <summary></summary>
        F = 0x46,
        /// <summary></summary>
        G = 0x47,
        /// <summary></summary>
        H = 0x48,
        /// <summary></summary>
        I = 0x49,
        /// <summary></summary>
        J = 0x4A,
        /// <summary></summary>
        K = 0x4B,
        /// <summary></summary>
        L = 0x4C,
        /// <summary></summary>
        M = 0x4D,
        /// <summary></summary>
        N = 0x4E,
        /// <summary></summary>
        O = 0x4F,
        /// <summary></summary>
        P = 0x50,
        /// <summary></summary>
        Q = 0x51,
        /// <summary></summary>
        R = 0x52,
        /// <summary></summary>
        S = 0x53,
        /// <summary></summary>
        T = 0x54,
        /// <summary></summary>
        U = 0x55,
        /// <summary></summary>
        V = 0x56,
        /// <summary></summary>
        W = 0x57,
        /// <summary></summary>
        X = 0x58,
        /// <summary></summary>
        Y = 0x59,
        /// <summary></summary>
        Z = 0x5A,
        /// <summary></summary>
        LeftWindows = 0x5B,
        /// <summary></summary>
        RightWindows = 0x5C,
        /// <summary></summary>
        Application = 0x5D,
        /// <summary></summary>
        Sleep = 0x5F,
        /// <summary></summary>
        Numpad0 = 0x60,
        /// <summary></summary>
        Numpad1 = 0x61,
        /// <summary></summary>
        Numpad2 = 0x62,
        /// <summary></summary>
        Numpad3 = 0x63,
        /// <summary></summary>
        Numpad4 = 0x64,
        /// <summary></summary>
        Numpad5 = 0x65,
        /// <summary></summary>
        Numpad6 = 0x66,
        /// <summary></summary>
        Numpad7 = 0x67,
        /// <summary></summary>
        Numpad8 = 0x68,
        /// <summary></summary>
        Numpad9 = 0x69,
        /// <summary></summary>
        Multiply = 0x6A,
        /// <summary></summary>
        Add = 0x6B,
        /// <summary></summary>
        Separator = 0x6C,
        /// <summary></summary>
        Subtract = 0x6D,
        /// <summary></summary>
        Decimal = 0x6E,
        /// <summary></summary>
        Divide = 0x6F,
        /// <summary></summary>
        F1 = 0x70,
        /// <summary></summary>
        F2 = 0x71,
        /// <summary></summary>
        F3 = 0x72,
        /// <summary></summary>
        F4 = 0x73,
        /// <summary></summary>
        F5 = 0x74,
        /// <summary></summary>
        F6 = 0x75,
        /// <summary></summary>
        F7 = 0x76,
        /// <summary></summary>
        F8 = 0x77,
        /// <summary></summary>
        F9 = 0x78,
        /// <summary></summary>
        F10 = 0x79,
        /// <summary></summary>
        F11 = 0x7A,
        /// <summary></summary>
        F12 = 0x7B,
        /// <summary></summary>
        F13 = 0x7C,
        /// <summary></summary>
        F14 = 0x7D,
        /// <summary></summary>
        F15 = 0x7E,
        /// <summary></summary>
        F16 = 0x7F,
        /// <summary></summary>
        F17 = 0x80,
        /// <summary></summary>
        F18 = 0x81,
        /// <summary></summary>
        F19 = 0x82,
        /// <summary></summary>
        F20 = 0x83,
        /// <summary></summary>
        F21 = 0x84,
        /// <summary></summary>
        F22 = 0x85,
        /// <summary></summary>
        F23 = 0x86,
        /// <summary></summary>
        F24 = 0x87,
        /// <summary></summary>
        NumLock = 0x90,
        /// <summary></summary>
        ScrollLock = 0x91,
        /// <summary></summary>
        NEC_Equal = 0x92,
        /// <summary></summary>
        Fujitsu_Jisho = 0x92,
        /// <summary></summary>
        Fujitsu_Masshou = 0x93,
        /// <summary></summary>
        Fujitsu_Touroku = 0x94,
        /// <summary></summary>
        Fujitsu_Loya = 0x95,
        /// <summary></summary>
        Fujitsu_Roya = 0x96,
        /// <summary></summary>
        LeftShift = 0xA0,
        /// <summary></summary>
        RightShift = 0xA1,
        /// <summary></summary>
        LeftControl = 0xA2,
        /// <summary></summary>
        RightControl = 0xA3,
        /// <summary></summary>
        LeftMenu = 0xA4,
        /// <summary></summary>
        RightMenu = 0xA5,
        /// <summary></summary>
        BrowserBack = 0xA6,
        /// <summary></summary>
        BrowserForward = 0xA7,
        /// <summary></summary>
        BrowserRefresh = 0xA8,
        /// <summary></summary>
        BrowserStop = 0xA9,
        /// <summary></summary>
        BrowserSearch = 0xAA,
        /// <summary></summary>
        BrowserFavorites = 0xAB,
        /// <summary></summary>
        BrowserHome = 0xAC,
        /// <summary></summary>
        VolumeMute = 0xAD,
        /// <summary></summary>
        VolumeDown = 0xAE,
        /// <summary></summary>
        VolumeUp = 0xAF,
        /// <summary></summary>
        MediaNextTrack = 0xB0,
        /// <summary></summary>
        MediaPrevTrack = 0xB1,
        /// <summary></summary>
        MediaStop = 0xB2,
        /// <summary></summary>
        MediaPlayPause = 0xB3,
        /// <summary></summary>
        LaunchMail = 0xB4,
        /// <summary></summary>
        LaunchMediaSelect = 0xB5,
        /// <summary></summary>
        LaunchApplication1 = 0xB6,
        /// <summary></summary>
        LaunchApplication2 = 0xB7,
        /// <summary></summary>
        OEM1 = 0xBA,
        /// <summary></summary>
        OEMPlus = 0xBB,
        /// <summary></summary>
        OEMComma = 0xBC,
        /// <summary></summary>
        OEMMinus = 0xBD,
        /// <summary></summary>
        OEMPeriod = 0xBE,
        /// <summary></summary>
        OEM2 = 0xBF,
        /// <summary></summary>
        OEM3 = 0xC0,
        /// <summary></summary>
        OEM4 = 0xDB,
        /// <summary></summary>
        OEM5 = 0xDC,
        /// <summary></summary>
        OEM6 = 0xDD,
        /// <summary></summary>
        OEM7 = 0xDE,
        /// <summary></summary>
        OEM8 = 0xDF,
        /// <summary></summary>
        OEMAX = 0xE1,
        /// <summary></summary>
        OEM102 = 0xE2,
        /// <summary></summary>
        ICOHelp = 0xE3,
        /// <summary></summary>
        ICO00 = 0xE4,
        /// <summary></summary>
        ProcessKey = 0xE5,
        /// <summary></summary>
        ICOClear = 0xE6,
        /// <summary></summary>
        Packet = 0xE7,
        /// <summary></summary>
        OEMReset = 0xE9,
        /// <summary></summary>
        OEMJump = 0xEA,
        /// <summary></summary>
        OEMPA1 = 0xEB,
        /// <summary></summary>
        OEMPA2 = 0xEC,
        /// <summary></summary>
        OEMPA3 = 0xED,
        /// <summary></summary>
        OEMWSCtrl = 0xEE,
        /// <summary></summary>
        OEMCUSel = 0xEF,
        /// <summary></summary>
        OEMATTN = 0xF0,
        /// <summary></summary>
        OEMFinish = 0xF1,
        /// <summary></summary>
        OEMCopy = 0xF2,
        /// <summary></summary>
        OEMAuto = 0xF3,
        /// <summary></summary>
        OEMENLW = 0xF4,
        /// <summary></summary>
        OEMBackTab = 0xF5,
        /// <summary></summary>
        ATTN = 0xF6,
        /// <summary></summary>
        CRSel = 0xF7,
        /// <summary></summary>
        EXSel = 0xF8,
        /// <summary></summary>
        EREOF = 0xF9,
        /// <summary></summary>
        Play = 0xFA,
        /// <summary></summary>
        Zoom = 0xFB,
        /// <summary></summary>
        Noname = 0xFC,
        /// <summary></summary>
        PA1 = 0xFD,
        /// <summary></summary>
        OEMClear = 0xFE
    }
}