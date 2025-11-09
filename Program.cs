using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Tesseract;
using static LilyMarket.DarkSide;
using PageIteratorLevel = Tesseract.PageIteratorLevel;
using Rectangle = System.Drawing.Rectangle;

namespace LilyMarket
{
    internal class Program
    {
        public struct ResultSlot
        {
            public int SlotIndex;
            public int Count;
            public int Price;
            public int UnitPrice;
            public Bitmap SlotImage;
        }

        private static Rectangle Rect;
        private static Settings Settings;

        private static readonly Rectangle SlotRect = new(0, 0, 493, 37);
        private static readonly Rectangle SlotToScanRect = new(0, 0, 493, 34);
        private static readonly Rectangle CountRect = new(0, 8, 35, SlotToScanRect.Height - 13);

        private static readonly Rectangle PriceRect = new(354, 0, SlotRect.Width - 354, SlotToScanRect.Height - 10); // old: -5

        private static readonly Size CountSize = new Size(CountRect.Width, CountRect.Height) * 2;
        private static readonly Size PriceSize = new Size(PriceRect.Width, PriceRect.Height) * 2;
        private static readonly Size FirstPageCheck = new Size(624, 547);
        
        private static readonly Rectangle SlotsAreaRect = new(379, 172, SlotRect.Width,
            SlotRect.Height * 9 + SlotToScanRect.Height);
        private static readonly Rectangle ResizedCountBitmapRect = new(0, 0, CountSize.Width, CountSize.Height);
        private static readonly Rectangle ResizedPriceBitmapRect = new(0, 0, PriceSize.Width, PriceSize.Height);

        private static readonly Rectangle FullPagesAreaRect = new(474, 547, 314, 1);
        private static readonly Rectangle NextPageButtonRect = new(782, 547, 0, 0);
        private static readonly Rectangle PagesLineRect = new(0, 0, FullPagesAreaRect.Width, FullPagesAreaRect.Height);
        
        private static readonly Rectangle BalanceRect = new(650, 584, 185, 22);

        private static readonly Rectangle SafetyCheckBoxRect = new(39, 10, 23, -1);
        private static readonly Rectangle PreLastPageCheckRect = new(269, 2, 1, 1);

        private static readonly StringBuilder StringBuilder = new();
        private static readonly Regex LettersExcludeRegex = new(@"\D");

        private const string TessdataPath = "TesseractOCR_Data";
        private const int DefaultManualStartPage = -1;
        private const int ScrollXOffset = 885;
        private const int ScrollYOffset = 99;
        private const int ScrollSize = 81;

        private const string LogPath = "results.log";
        private const string ScreenshotsFolder = @"Screenshots\";
        private static string ProcessPath;

        private const int TesseractsCount = 9;
        private static readonly TesseractEngine[]? RusEngines = new TesseractEngine[TesseractsCount];
        private static readonly TesseractEngine[]? EngEngines = new TesseractEngine[TesseractsCount];
        private static readonly Image<Bgr, byte>[] CountImagesBuffer = new Image<Bgr, byte>[9];
        private static readonly Image<Bgr, byte>[] PriceImagesBuffer = new Image<Bgr, byte>[9];

        private static TesseractEngine? _russianOcr;
        private static TesseractEngine? _englishOcr;
        private const PixelFormat PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
        private static bool _isCUDASupported;
        private static bool _isOnlyOneTarget;
        private static bool _firstOnlyOneTargetScanDone;
        
        private static IntPtr _windowHandle;
        private static IntPtr _selfWindowHandle;
        private static int _pageIndex;
        private static int _scrollIndex;
        private static readonly UserActivityHook ActivityHook = new(false, false);
        private static int _balance;
        private static int _clickTimer;
        private static bool _stopped;
        private static bool _hotkeyHolded;
        // 1: balance, 2: products
        private static readonly Dictionary<ProductTarget, (int, int)> ProductsInfo = new();
        private static int _failConsumedBalance;
        private static int _totalConsumedBalance;
        
        private static int _scrollYCheck;
        private static int _scrollX;
        private static int _scrollY;
        private static int _searchButtonX;
        private static int _searchButtonY;
        private static int _buyButtonCheckX;
        private static int _pageButtonX;
        private static int _pageButtonY;
        private static int _nextPageButtonX;
        private static int _boughtOkButtonX;
        private static int _boughtOkButtonY;
        private static (int, int) _screenResolution;
        private static TextRecognitionModel _textModel;

        private static Random _random = new();

        private static readonly object WriteLocker = new();
        private static readonly object SlotsLocker = new();

        private static string GenerateString(int length)
        {
            const string chars = "0123456789-_=+*!(),.<>/\\[]qwertyuiopasdfghjklzxcvbnm";
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var charIndex = _random.Next(0, 52);
                var @char = chars[charIndex];
                if (charIndex > 26 && _random.Next(0, 100) > 50)
                    @char = char.ToUpper(@char);
                stringBuilder.Append(@char);
            }

            return stringBuilder.ToString();
        }

        private static void InitCvMethods()
        {
            _isCUDASupported = CudaInvoke.HasCuda;
        }
        
        public static Func<IInputArray, IOutputArray, double, double, ThresholdType, Emgu.CV.Cuda.Stream, double> CvThreshold;

        private const int ConsoleWidth = 600;
        private const int ConsoleHeight = 500;

        [STAThread]
        public static void Main()
        {
            

            AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeBitBlt();
            
            //InitBitBlt();
            var currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.RealTime;
            _selfWindowHandle = currentProcess.MainWindowHandle;
            SetWindowPos(_selfWindowHandle, SetWindowPosShowFlags.HWND_TOP, 0, 0, ConsoleWidth, ConsoleHeight,
                SetWindowPosFlags.HIDEWINDOW);

            StringBuilder.Append(Assembly.GetExecutingAssembly().Location).Length -= 3;
            StringBuilder.Append("exe");
            ProcessPath = StringBuilder.ToString();
            StringBuilder.Clear();
            
            Console.Title = GenerateString(16);
            _screenResolution = GetScreenResolution();
            ActivityHook.KeyDown += OnKeyDown;
            ActivityHook.KeyUp += OnKeyUp;
            ActivityHook.Start(false, true);

            _russianOcr = new(TessdataPath, "rus", EngineMode.LstmOnly);
            _englishOcr = new(TessdataPath, "eng", EngineMode.LstmOnly);
            _russianOcr.SetVariable("tessedit_char_whitelist", "0123456789ост");
            _russianOcr.SetVariable("debug_file", "/dev/null");
            _russianOcr.SetVariable("applybox_debug", 0);
            
            _englishOcr.SetVariable("tessedit_char_whitelist", "0123456789");
            _englishOcr.SetVariable("debug_file", "/dev/null");
            _englishOcr.SetVariable("classify_bln_numeric_mode", 1);
            _englishOcr.SetVariable("tessedit_do_invert", 0);

            for (int i = 0; i < TesseractsCount; i++)
            {
                var engine = RusEngines[i] = new(TessdataPath, "rus", EngineMode.LstmOnly);
                engine.DefaultPageSegMode = PageSegMode.SingleLine;
                //engine.SetVariable("tessedit_char_whitelist", "0123456789");
                engine.SetVariable("debug_file", "/dev/null");
                //engine.SetVariable("classify_bln_numeric_mode", 1);
                engine.SetVariable("applybox_debug", 0);
                
                engine.SetVariable("load_system_dawg", "0");
                engine.SetVariable("load_freq_dawg", "0");
                engine.SetVariable("load_punc_dawg", "0");
                engine.SetVariable("load_number_dawg", "1");
                engine.SetVariable("load_unambig_dawg", "0");
                engine.SetVariable("load_bigram_dawg", "0");
                engine.SetVariable("load_fixed_length_dawgs", "0");
            }

            for (int i = 0; i < TesseractsCount; i++)
            {
                var engine = EngEngines[i] = new(TessdataPath, "eng", EngineMode.LstmOnly);
                engine.DefaultPageSegMode = PageSegMode.SingleLine;
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                engine.SetVariable("debug_file", "/dev/null");
                engine.SetVariable("classify_bln_numeric_mode", 1);
                engine.SetVariable("applybox_debug", 0);
                engine.SetVariable("tessedit_do_invert", 0);
                engine.SetVariable("tessedit_create_hocr", "0");
                engine.SetVariable("tessedit_create_tsv", "0");
                engine.SetVariable("tessedit_create_pdf", "0");
                
                engine.SetVariable("load_system_dawg", "0");
                engine.SetVariable("load_freq_dawg", "0");
                engine.SetVariable("load_punc_dawg", "0");
                engine.SetVariable("load_number_dawg", "1");
                engine.SetVariable("load_unambig_dawg", "0");
                engine.SetVariable("load_bigram_dawg", "0");
                engine.SetVariable("load_fixed_length_dawgs", "0");
            }

            for (int i = 0; i < CountImagesBuffer.Length; i++)
            {
                CountImagesBuffer[i] = new Image<Bgr, byte>(CountSize);
                PriceImagesBuffer[i] = new Image<Bgr, byte>(PriceSize);
            }
            _russianOcr.DefaultPageSegMode = _englishOcr.DefaultPageSegMode = PageSegMode.SingleLine;

            // var task = Task.Run(async () =>
            // {
            //     try
            //     {
            //         Settings = await Settings.Load();
            //         _windowHandle = FindWindow(null, "STALCRAFT");
            //         _screenResolution = GetScreenResolution();
            //         if (IsIconic(_windowHandle))
            //         {
            //             ShowWindow(_windowHandle, 9);
            //             await Task.Delay(1000);
            //         }
            //
            //         GetWindowRect(_windowHandle, out Rect);
            //         Rect.Width = 900;
            //         Rect.Height = 615;
            //         if (Rect.X < 5)
            //             Rect.X = 5;
            //         else if (Rect.X + Rect.Width > _screenResolution.Item1)
            //             Rect.X = _screenResolution.Item1 - Rect.Width;
            //         if (Rect.Y + Rect.Height > _screenResolution.Item2 - 256)
            //             Rect.Y = 100;
            //         SetWindowPos(_windowHandle, SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y, Rect.Width, Rect.Height,
            //             SetWindowPosFlags.SHOWWINDOW);
            //         SetForegroundWindow(_windowHandle);
            //         //await Task.Delay(500);
            //         SetWindowPos(_selfWindowHandle, SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y + Rect.Height,
            //             ConsoleWidth,
            //             ConsoleHeight,
            //             SetWindowPosFlags.SHOWWINDOW);
            //         await Task.Delay(500);
            //         SetForegroundWindow(_windowHandle);
            //
            //         var searchBoxX = Rect.X + 661;
            //         var searchBoxY = Rect.Y + 123;
            //         _searchButtonX = Rect.X + 827;
            //         _searchButtonY = Rect.Y + 113;
            //         _buyButtonCheckX = Rect.X + 767;
            //         _pageButtonX = Rect.X + FullPagesAreaRect.X;
            //         _pageButtonY = Rect.Y + FullPagesAreaRect.Y;
            //         _nextPageButtonX = Rect.X + NextPageButtonRect.X;
            //
            //         _scrollYCheck = Rect.Y + ScrollYOffset + 2;
            //         _scrollX = Rect.X + ScrollXOffset;
            //         _scrollY = Rect.Y + ScrollYOffset + 38;
            //         _boughtOkButtonX = Rect.X + 367;
            //         _boughtOkButtonY = Rect.Y + 335;
            //
            //         const int pagesDiffOffsetX = 24;
            //
            //         var pagesOffsetY = Rect.Y + FullPagesAreaRect.Y + 8;
            //
            //         var okButtonX = Rect.X + 445;
            //         var okButtonY2 = Rect.Y + 370;
            //
            //         var buyButtonX = _buyButtonCheckX;
            //         var buyButtonY = Rect.Y + SlotsAreaRect.Y + SlotRect.Height * 5;
            //
            //         for (int i = 1; i < 8; i++)
            //         {
            //             var bitmap = new Bitmap(Environment.CurrentDirectory + $"/Screenshots/{i}.png");
            //             Console.WriteLine(GetCount1(0, bitmap).Item1);
            //         }
            //         
            //         if (_stopped) return false;
            //
            //     }
            //     catch (Exception e)
            //     {
            //         Console.WriteLine(e);
            //         throw;
            //     }
            //
            //     return false;
            // });
            // task.Wait();
            // return;
            
            var runTask = Task.Run(async () =>
            {
                while (true)
                {
                    // try
                    // {
                    if (_stopped)
                    {
                        await Task.Delay(5);
                        continue;
                    }
                    
                    await StartBotAsync();
                    // }
                    // catch (Exception e)
                    // {
                    //     Console.WriteLine(e);
                    // }
                }
            });
            var lagTask = Task.Run(async () =>
            {
                while (Settings == null)
                    await Task.Delay(100);
                while (true)
                {
                    await Task.Delay(100);
                    if (_stopped) continue;
                    _clickTimer++;
                    if (_clickTimer >= Settings.Delays.TimeoutRestart / 10)
                    {
                        _stopped = true;
                        _clickTimer = 0;
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Stopped, restarting...");
                        OnStop();
                        var info = new ProcessStartInfo
                        {
                            WorkingDirectory = Environment.CurrentDirectory,
                            FileName = ProcessPath,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(info);
                        await Task.Delay(500);
                        Environment.Exit(0);
                        _stopped = false;
                    }
                }
            });

            Task.WaitAll(runTask, lagTask);
        }
        
        private static async Task DropScroll()
        {
            var firstScrollPixelR = CapturePixelRed(_scrollX, _scrollYCheck);
            if (firstScrollPixelR >= 100)
            {
                await Click(_scrollX + _random.Next(-3, 5), _scrollYCheck + _random.Next(170, 458), Settings.Delays.AfterMoveDelays.ScrollDrop);

                var repeats = 0;
                while (true)
                {
                    var scrollPixelR = CapturePixelRed(_scrollX, _scrollYCheck);
                    if (scrollPixelR < 80) break;

                    repeats++;
                    if ((repeats & 4) == 4)
                        await Click(_scrollX + _random.Next(-3, 5), _scrollYCheck + _random.Next(170, 458), Settings.Delays.AfterMoveDelays.ScrollDrop);
                    
                    await Task.Delay(4);
                }
            }
        }
        
        public static async Task StartBotAsync()
        { 
            Console.Clear();
            InitCvMethods();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(_isCUDASupported ? "CUDA Supported" : "No CUDA");
            
            Settings = await Settings.Load();
            Settings.Save();
            
            // *.lang file shouldn't contain "example1"
            if (Settings.Targets.Length == 0 || Settings.Targets[0].Text == "example1")
            {
                Console.WriteLine("No targets loaded. Delete \'settings.json\' and relaunch app. U'll get examples for targets.");
                Console.ReadKey();
                Environment.Exit(0);
                return;
            }
            ProductsInfo.Clear();
            StringBuilder.Clear();

            foreach (var target in Settings.Targets)
            {
                const string offset = "  ";
                StringBuilder.Append("Target: \'").Append(target.Text).AppendLine("\'").Append(offset).Append("Pages: ")
                    .Append(target.StartPage).Append(" -> ").Append(target.PagesCount).AppendLine()
                    .Append(offset).Append("Unit Price: ").AppendLine(target.UnitPrice.ToString("N0"));
                target.StartPage -= 1;
                ProductsInfo[target] = (0, 0);
            }

            Console.WriteLine(StringBuilder);
            StringBuilder.Clear();

            _windowHandle = FindWindow(null, "STALCRAFT");
            if (IsIconic(_windowHandle))
            {
                ShowWindow(_windowHandle, 9);
                await Task.Delay(1000);
            }

            GetWindowRect(_windowHandle, out Rect);
            Rect.Width = 900;
            Rect.Height = 615;
            if (Rect.X < 5)
                Rect.X = 5;
            else if (Rect.X + Rect.Width > _screenResolution.Item1)
                Rect.X = _screenResolution.Item1 - Rect.Width;
            if (Rect.Y + Rect.Height > _screenResolution.Item2 - 256)
                Rect.Y = 100;
            SetWindowPos(_windowHandle, SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y, Rect.Width, Rect.Height,
                SetWindowPosFlags.SHOWWINDOW);
            SetForegroundWindow(_windowHandle);
            SetWindowPos(_selfWindowHandle, SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y + Rect.Height, Rect.Width,
                ConsoleHeight,
                SetWindowPosFlags.SHOWWINDOW);
            await Task.Delay(500);
            SetForegroundWindow(_windowHandle);
            PostMessage(_windowHandle, SystemMessage.WindowActivate, (int)SystemMessage.WindowActivate, 0);
            await Click(Rect.X, Rect.Y);

            var searchBoxX = Rect.X + 661;
            _searchButtonX = Rect.X + 796;
            _searchButtonY = Rect.Y + 113;
            _pageButtonX = Rect.X + FullPagesAreaRect.X;
            _pageButtonY = Rect.Y + FullPagesAreaRect.Y;
            _nextPageButtonX = Rect.X + NextPageButtonRect.X;
            
            _scrollYCheck = Rect.Y + ScrollYOffset + 2;
            _scrollX = Rect.X + ScrollXOffset;
            _scrollY = Rect.Y + ScrollYOffset + 38;
            _buyButtonCheckX = Rect.X + 767;
            _boughtOkButtonX = Rect.X + 367;
            _boughtOkButtonY = Rect.Y + 335; // +9 = 344
            _totalConsumedBalance = 0;

            _balance = GetBalance();
            
            var scanCodeLeftControl = MapVirtualKey(VirtualKey.LeftControl, 0) << 16 | 0;
            var scanCodeAUp = MapVirtualKey(VirtualKey.A, 0) << 16 | 1;
            var scanCodeVUp = MapVirtualKey(VirtualKey.V, 0) << 16 | 1;
            //var scanCodeBackspaceUp = MapVirtualKey(VirtualKey.Back, 0) << 16 | 1;
            //var scanCodeVDown = MapVirtualKey(VirtualKey.V, 0) << 16 | 0xC0000001;

            _isOnlyOneTarget = Settings.Targets.Length == 1;
            _firstOnlyOneTargetScanDone = false;
            var onceUsed = false;

            if (CapturePixelRed(_boughtOkButtonX, _boughtOkButtonY) > 200 ||
                CapturePixelRed(_boughtOkButtonX, _boughtOkButtonY + 9) > 200)
            {
                keybd_event((byte)VirtualKey.Escape, 0, 0, 0);
                keybd_event((byte)VirtualKey.Escape, 0, 2, 0);
            }
            
            while (!_stopped)
            {
                foreach (var target in Settings.Targets)
                {
                    if (_stopped) return;

                    if (!_isOnlyOneTarget || (_isOnlyOneTarget && !onceUsed))
                    {
                        onceUsed = true;
                        _pageIndex = 0;
                        if (GetOpenClipboardWindow() != 0)
                        {
                            CloseClipboard();
                        }

                        OpenClipboard(0);
                        EmptyClipboard();
                        StringBuilder.Append(target.Text).Append('\0');
                        var bytes = Encoding.Unicode.GetBytes(StringBuilder.ToString());
                        StringBuilder.Clear();
                        var textPtr = GlobalAlloc(2, (UIntPtr)bytes.Length);

                        var pGlobal = GlobalLock(textPtr);
                        Marshal.Copy(bytes, 0, pGlobal, bytes.Length);
                        GlobalUnlock(textPtr);
                
                        SetClipboardData(13, textPtr);
                        CloseClipboard();
                        
                        await Click(searchBoxX + _random.Next(0, 131), _searchButtonY + _random.Next(0, 18), Settings.Delays.AfterMoveDelays.SearchTextBox);
                        await Click(searchBoxX + _random.Next(0, 131), _searchButtonY + _random.Next(0, 18), Settings.Delays.AfterMoveDelays.SearchTextBox);
                        SendLeftButtonKey(_windowHandle, true);
                        
                        // IDK how, but it works
                        
                        PostMessage(_windowHandle, SystemMessage.KeyboardKeyDown, (uint)VirtualKey.LeftControl, scanCodeLeftControl);
                        await Task.Delay(8);
                        PostMessage(_windowHandle, SystemMessage.KeyboardKeyDown, (uint)VirtualKey.LeftControl, scanCodeAUp);
                        //PostMessage(_windowHandle, SystemMessage.KeyboardKeyUp, (uint)VirtualKey.Back, scanCodeBackspaceUp);
                        PostMessage(_windowHandle, SystemMessage.KeyboardKeyDown, (uint)VirtualKey.LeftControl, scanCodeAUp);
                        await Task.Delay(16);
                        //PostMessage(_windowHandle, SystemMessage.KeyboardKeyUp, (uint)VirtualKey.Back, scanCodeBackspaceUp);
                        PostMessage(_windowHandle, SystemMessage.KeyboardKeyDown, (uint)VirtualKey.LeftControl, scanCodeVUp);

                        await DropScroll();

                        await Click(_searchButtonX + _random.Next(0, 73), _searchButtonY + _random.Next(0, 18), Settings.Delays.AfterMoveDelays.SearchButton);
                        
                        while (true)
                        {
                            var pixelColorR = CapturePixelRed(_scrollX, _scrollYCheck);
                            if (pixelColorR >= 100)
                            {
                                break;
                            }

                            await Task.Delay(16);
                        }
                    }

                    _balance = GetBalance();
                    if (_stopped) return;
                    
                    // Feature: target can be bought if total < maxUsage even if overs maxUsage after bought
                    // Otherwise it would make cycle infinity with ~1 to maxUsage
                    if (Settings.MaxBalanceUsage != 0 && _totalConsumedBalance >= Settings.MaxBalanceUsage)
                    {
                        _stopped = true;
                        OnStop();
                    }
                    var (needToRescan, pageToReturn) = await ScanForTarget(target);
                    _firstOnlyOneTargetScanDone = true;
                    while (needToRescan)
                    {
                        if (Settings.MaxBalanceUsage != 0 && _totalConsumedBalance >= Settings.MaxBalanceUsage)
                        {
                            _stopped = true;
                            OnStop();
                        }
                        while (true)
                        {
                            var pixelColorR = CapturePixelRed(Rect.X + 380, Rect.Y + 181);
                            if (pixelColorR >= 24)
                                break;
                            
                            await Task.Delay(16);
                        }

                        (needToRescan, pageToReturn) = await ScanForTarget(target, pageToReturn);
                    }
                }
            }
        }

        // bool: whether need to rescan, int: page to return
        public static async Task<(bool, int)> ScanForTarget(ProductTarget target,
            int manualStartPage = DefaultManualStartPage)
        {
            if (_stopped) return (false, DefaultManualStartPage);
            const int pagesDiffOffsetX = 24; // 12x13 - page button size
            
            int pagesCount, lastIndex = 0, lastPage = 0, startPage;
            bool extraPages;

            void FindPages()
            {
                //await Task.Delay(Settings.Delays.BeforePagesUpdate);
                using var pagesBitmap = CaptureRegion(Rect.X + FullPagesAreaRect.X, Rect.Y + FullPagesAreaRect.Y, FullPagesAreaRect.Width, FullPagesAreaRect.Height);
                extraPages = pagesBitmap.GetPixel(FullPagesAreaRect.Width - 1, 0).R > 70;
                
                var pagesData = pagesBitmap.LockBits(PagesLineRect, ImageLockMode.ReadOnly, PixelFormat);
                var pagesScan = pagesData.Scan0 + 25;

                Parallel.For(0, 25, (pageIndex, _) =>
                {
                    unsafe
                    {
                        if (*(byte*)(pagesScan + pageIndex * 12 * 4) > 100)
                        {
                            lastIndex = pageIndex;
                        }
                    }
                });
                pagesBitmap.UnlockBits(pagesData);
                if (extraPages)
                {
                    lastPage = lastIndex == 23 ? 0 : target.StartPage + target.PagesCount;
                }
                else
                {
                    pagesCount = 13 - lastIndex + _pageIndex * 2;
                    lastPage = Math.Min(pagesCount - 1, target.StartPage + target.PagesCount);
                }
            }
            
            startPage = manualStartPage != DefaultManualStartPage ? manualStartPage : target.StartPage;
            startPage = int.Min(startPage, 12);
            if (!_isOnlyOneTarget)
            {
                _pageIndex = 0;
            }
            FindPages();
            if (_isOnlyOneTarget && _pageIndex != 0)
            {
                lastIndex -= _pageIndex * 2;
                pagesCount = 13 - lastIndex + _pageIndex * 2;
                lastPage = Math.Min(pagesCount - 1, target.StartPage + target.PagesCount);
                //Console.WriteLine($"_pageIndex: {_pageIndex} | lastIndex: {lastIndex} | startPage: {startPage}");
            }
            
            _pageIndex = startPage;
            
            for (; _pageIndex < lastPage; _pageIndex++)
            {
                SendLeftButtonKey(_windowHandle, false);
                var pageRepeats = 0;

                var needToReswap = false;

                async Task<bool> SwapPage()
                {
                    if (_pageIndex == startPage && startPage != 0 || (_isOnlyOneTarget && _pageIndex == 0 && _firstOnlyOneTargetScanDone))
                    {
                        await DropScroll();
                        await Click(_pageButtonX + lastIndex * 12 + pagesDiffOffsetX * startPage + _random.Next(0, 12), _pageButtonY + _random.Next(0, 13), Settings.Delays.AfterMoveDelays.Page);
                    }
                    else if (_pageIndex != startPage)
                    {
                        await Click(_pageButtonX + lastIndex * 12 + pagesDiffOffsetX + _random.Next(0, 12), _pageButtonY + _random.Next(0, 13), Settings.Delays.AfterMoveDelays.Page);
                        // await Click(_pageButtonX + pageOffset * 12 + pagesDiffOffsetX * (_pageIndex) + _random.Next(0, 12), _pageButtonY + _random.Next(0, 13), 48);
                    }

                    if (_stopped) return false;
                    
                    while (true)
                    {
                        var pixelColorR = CapturePixelRed(_scrollX, _scrollYCheck);
                        if (pixelColorR >= 100)
                            break;
                        pageRepeats++;
                        switch (pageRepeats)
                        {
                            case 16:
                                //case 32:
                                needToReswap = true;
                                return true;
                            case 64:
                                return false;
                        }

                        await Task.Delay(64);
                    }

                    if (_pageIndex == startPage && startPage == 0)
                        await Task.Delay(Settings.Delays.OnFirstPage);

                    needToReswap = false;
                    return true;
                }

                while (true)
                {
                    var swapResult = await SwapPage();
                    if (!swapResult)
                        return (false, -1);

                    if (!needToReswap)
                        break;

                    await DropScroll();
                }

                var scrollY = _scrollY + _random.Next(-34, 48); //-38 50
                SendLeftButtonKey(_windowHandle, false);
                
                var (resultSlots, foundPreBreak) = await ScanSlots(target, 0, 9);
                
                if (resultSlots.Length != 0)
                {
                    // index reset for switch inside 'BuyProfitSlot'
                    _scrollIndex = 0;
                    var pageToReturn = _pageIndex - 1;
                    if (await BuyProfitSlot(resultSlots, target))
                        _pageIndex = 0;
                    return (true, pageToReturn + 1);
                }

                if (foundPreBreak) return (false, DefaultManualStartPage);
                
                SetCursorPos(_scrollX + _random.Next(-3, 6), scrollY);
                await Task.Delay(Settings.Delays.AfterMoveDelays.Scroll);
                // hold LMB for scrolling
                SendLeftButtonKey(_windowHandle, true);
                await Task.Delay(Settings.Delays.AfterHold);

                while (true)
                {
                    var restartScroll = false;
                    for (_scrollIndex = 0; _scrollIndex < target.ScrollsCount; _scrollIndex++)
                    {
                        if (_stopped)
                        {
                            SendLeftButtonKey(_windowHandle, false);
                            return (false, DefaultManualStartPage);
                        }
                        
                        SetCursorPos(_scrollX + _random.Next(-3, 6), scrollY + ScrollSize * (_scrollIndex + 1) - (_scrollIndex >= 4 ? 1 : 0));
                        await Task.Delay(Settings.Delays.AfterScroll);
                        
                        if (_scrollIndex == 0)
                        {
                            var pixelR = CapturePixelRed(_scrollX, _scrollYCheck);
                            if (pixelR > 80)
                            {
                                restartScroll = true;
                                break;
                            }
                        }

                        var startIndex = 0;
                        var slotsToScan = 9;
                        if (_scrollIndex == 4)
                        {
                            startIndex = 5;
                            slotsToScan = 5;
                            SendLeftButtonKey(_windowHandle, false);
                        }

                        // Kostil' for best working
                        _scrollIndex++;
                        (resultSlots, foundPreBreak) = await ScanSlots(target, startIndex, slotsToScan);
                        _scrollIndex--;
                        if (resultSlots.Length != 0)
                        {
                            var pageToReturn = _pageIndex - 1;
                            if (await BuyProfitSlot(resultSlots, target))
                                _pageIndex = 0;
                            return (true, pageToReturn + 1); // Back to current page
                        }
                        
                        if (foundPreBreak) return (false, DefaultManualStartPage);
                    }

                    if (restartScroll)
                    {
                        SendLeftButtonKey(_windowHandle, false);
                        scrollY = _scrollY + _random.Next(-34, 48);
                        SetCursorPos(_scrollX + _random.Next(-3, 6), scrollY);
                        await Task.Delay(Settings.Delays.AfterMoveDelays.Scroll);
                        SendLeftButtonKey(_windowHandle, true);
                        await Task.Delay(Settings.Delays.AfterHold);
                        continue;
                    }

                    break;
                }
                
                if (_pageIndex != lastPage - 1)
                    FindPages();
            }
            _pageIndex--;
            
            SendLeftButtonKey(_windowHandle, false);
            return (false, DefaultManualStartPage);
        }

        public static async Task<(ResultSlot[], bool)> ScanSlots(ProductTarget target, int startIndex, int slotsToScan)
        {
            var time = DateTime.Now.ToString("dd/hh:mm:ss");
            var resultSlots = new ResultSlot[slotsToScan];
            var resultIndex = 0;

            //using var windowShot = CaptureRegion(Rect.X, Rect.Y, Rect.Width, Rect.Height);//CaptureRegion(_windowHandle);
            using var slotsMap = CaptureRegion(Rect.X + SlotsAreaRect.X, Rect.Y + SlotsAreaRect.Y + startIndex * SlotRect.Height, SlotsAreaRect.Width, SlotsAreaRect.Height - startIndex * SlotRect.Height);
            //slotsMap.Save(Environment.CurrentDirectory + $@"/Screenshots/{DateTime.Now.ToFileTime()} bitmap.png");

            var slotsBitmaps = new Bitmap[slotsToScan];

            // for (int i = 0; i < slotsToScan; i++)
            // {
            //     slotsBitmaps[i] = slotsMap.Clone(SlotToScanRect with {Y = SlotRect.Height * i}, PixelFormat);
            //     //slotsBitmaps[i].Save(Environment.CurrentDirectory + $@"/Screenshots/{DateTime.Now.ToFileTime().ToString()}.png");
            // }
            var sourceData = slotsMap.LockBits(new Rectangle(0, 0, slotsMap.Width, slotsMap.Height),
                ImageLockMode.ReadOnly, PixelFormat);

            var sourceScan = sourceData.Scan0;
            var sourceStride = sourceData.Stride;

            var slotWidth = SlotRect.Width;
            var slotHeight = SlotToScanRect.Height;
            var slotY = SlotRect.Height;
            var hasLimitedSlot = slotsToScan == 5;
            Parallel.For(0, slotsToScan, (slotIndex, _) =>
            {
                var y = slotY * slotIndex + 5;
                var height = slotHeight;
                if (hasLimitedSlot && slotIndex == 4)
                    height -= 5;

                var slotBitmap = slotsBitmaps[slotIndex] = new Bitmap(slotWidth, height);

                var slotData = slotBitmap.LockBits(SlotRect with { Height = height }, ImageLockMode.WriteOnly,
                    PixelFormat);
                var slotScan = slotData.Scan0;

                Parallel.For(y, y + height, (heightNum, _) =>
                {
                    var sourceRow = sourceScan + heightNum * sourceStride;
                    var slotRow = slotScan + (heightNum - y) * sourceStride;
                    unsafe
                    {
                        Buffer.MemoryCopy((byte*)sourceRow, (byte*)slotRow, sourceStride, sourceStride);
                    }
                });

                slotBitmap.UnlockBits(slotData);
                //slotBitmap.Save(Environment.CurrentDirectory + $@"/Screenshots/{DateTime.Now.ToFileTime().ToString()}.png");
            });
            slotsMap.UnlockBits(sourceData);

            var foundPreBreak = false;
            var forceInstaRestart = false;
            var lastIndex = 0;

            Parallel.For(0, slotsToScan, (slotIndex, _) =>
            {
                var slotBitmap = slotsBitmaps[slotIndex];
                var slotData = slotBitmap.LockBits(new Rectangle(0, 0, slotBitmap.Width, slotBitmap.Height),
                    ImageLockMode.ReadOnly, PixelFormat);
                var slotScan = slotData.Scan0;
                var slotStride = slotBitmap.Width * 4;
                int price = 0, count = 0;
                var unavailableSlot = false;

                var id = DateTime.Now.ToFileTime();
                Parallel.Invoke(new ParallelOptions(), () =>
                    {
                        var priceRectWidth = PriceRect.Width;
                        var priceRectHeight = PriceRect.Height;
                        var priceRect = new Rectangle(0, 0, priceRectWidth, priceRectHeight);
                        var priceStride = priceRectWidth * 4;
                        var priceBitmap = new Bitmap(priceRectWidth, priceRectHeight);

                        var priceData = priceBitmap.LockBits(priceRect, ImageLockMode.WriteOnly, PixelFormat);
                        var priceScan = priceData.Scan0;

                        Parallel.For(0, priceRectHeight, (heightNum, _) =>
                        {
                            var sourceRow = slotScan + heightNum * slotStride + 354 * 4;
                            var priceRow = priceScan + heightNum * priceStride;
                            unsafe
                            {
                                Buffer.MemoryCopy((byte*)sourceRow, (byte*)priceRow, priceStride, priceStride);
                            }
                        });

                        priceBitmap.UnlockBits(priceData);
                        using var image = priceBitmap.ToImage<Bgr, byte>();
                        priceBitmap.Dispose();
                        var priceImage = PriceImagesBuffer[slotIndex];
                        CvInvoke.ResizeForFrame(image, priceImage, PriceSize, Inter.Cubic);
                        CvInvoke.Threshold(priceImage, priceImage, 120, 255, ThresholdType.BinaryInv);
                        using var priceBitmapResized = priceImage.ToBitmap();
                        
                        using var pricePage = RusEngines![slotIndex].Process(priceBitmapResized);
                        //priceBitmapResized.Save(Environment.CurrentDirectory + $@"/Screenshots/{id}.png");
                        //priceBitmap.Dispose();
                        var ocrString = LettersExcludeRegex.Replace(pricePage.GetText(), string.Empty);
                        if (ocrString.StartsWith('0'))
                        {
                            unavailableSlot = true;
                            return;
                        }

                        if (!int.TryParse(ocrString, out price))
                        {
                            unavailableSlot = true;
                            if (lastIndex != 0 && slotIndex > lastIndex)
                            {
                                foundPreBreak = true;
                            }

                            return;
                        }

                        if (slotIndex > lastIndex)
                            lastIndex = slotIndex;
                    },
                    () => (count, forceInstaRestart) = GetCount(slotIndex, slotScan));//, false, true, id + $"_s{_scrollIndex}_i{slotIndex}_orig"));

                // var slotBitmapChecked = CaptureRegion(Rect.X + SlotsAreaRect.X, Rect.Y + SlotsAreaRect.Y + slotIndex * SlotRect.Height, SlotsAreaRect.Width, SlotRect.Height);
                // slotBitmapChecked.Save(Environment.CurrentDirectory + $"/Screenshots/{id}_s{_scrollIndex}_i{slotIndex}_dubl0.png");
                // var doubleSlotData = slotBitmapChecked.LockBits(SlotRect with { Height = slotBitmapChecked.Height }, ImageLockMode.ReadOnly, PixelFormat);
                // var doubleSlotScan = doubleSlotData.Scan0;
                // slotBitmapChecked.UnlockBits(doubleSlotData);
                // GetCount(slotIndex, doubleSlotScan, false, true, id + $"_s{_scrollIndex}_i{slotIndex}_dubl");

                slotBitmap.UnlockBits(slotData);
                //slotBitmap.Save(Environment.CurrentDirectory + @$"\Screenshots\scan_{DateTime.Now.ToFileTime()}_c{count}_p{price}.png");

                if (unavailableSlot)
                {
                    slotBitmap.Dispose();
                    return;
                }

                if (count > target.MaxAvailableCount) return;
                var unitPrice = price / count;
                if (unitPrice <= target.UnitPrice && unitPrice > target.MinUnitPrice && price > target.MinPrice)
                {
                    if (_balance >= price)
                    {
                        var resultSlot = new ResultSlot
                        {
                            SlotIndex = slotIndex + startIndex,
                            Price = price,
                            Count = count,
                            UnitPrice = unitPrice,
                            SlotImage = slotBitmap
                        };
                        lock (SlotsLocker)
                        {
                            resultSlots[resultIndex++] = resultSlot;
                        }
                    }
                    else
                    {
                        slotBitmap.Dispose();
                        lock (WriteLocker)
                        {
                            StringBuilder.Append('[').Append(time)
                                .Append("] Not enough balance : c(").Append(count.ToString("N0"))
                                .Append(") p(").Append(price.ToString("N0"))
                                .Append(") up(").Append(unitPrice.ToString("N0")).AppendLine(")");
                        }
                    }
                }
                else
                {
                    slotBitmap.Dispose();
                }
            });

            if (forceInstaRestart)
            {
                _clickTimer = 0x3FFFFFFF;
                await Task.Delay(_clickTimer);
            }

            if (StringBuilder.Length != 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write(StringBuilder);
                StringBuilder.Clear();
            }

            Array.Resize(ref resultSlots, resultIndex);
            return (resultSlots, foundPreBreak);
        }
        
        public static (int, bool) GetCount(int slotIndex, IntPtr slotScan, bool doubleCheck = false, bool print = false, string id = null)
        {
            var forceInstaRestart = false;
            var slotStride = SlotRect.Width * 4;
            var countRectWidth = CountRect.Width;
            var countRectHeight = CountRect.Height;
            var countRect = new Rectangle(0, 0, countRectWidth, countRectHeight);
            var countStride = countRectWidth * 4;
            var countBitmap = new Bitmap(countRectWidth, countRectHeight);

            var countData = countBitmap.LockBits(countRect, ImageLockMode.WriteOnly, PixelFormat);
            var countScan = countData.Scan0;

            Parallel.For(0, countRectHeight, (heightNum, _) =>
            {
                var sourceRow = slotScan + (heightNum + CountRect.Y) * slotStride;
                var countRow = countScan + heightNum * countStride;
                unsafe
                {
                    Buffer.MemoryCopy((byte*)sourceRow, (byte*)countRow, countStride, countStride);
                }
            });

            countBitmap.UnlockBits(countData);
            
            using var countImage1 = countBitmap.ToImage<Bgr, byte>();
            countBitmap.Dispose();
            var countImage = CountImagesBuffer[slotIndex];
            CvInvoke.ResizeForFrame(countImage1, countImage, CountSize, Inter.Cubic);
            using var countBitmapResized = countImage.ToBitmap();

            // using var testImg = countBitmapResized.ToImage<Bgr, byte>();
            
            // Micro-optimization be like
            var countSize = CountSize.Width * CountSize.Height;

            countData = countBitmapResized.LockBits(ResizedCountBitmapRect, ImageLockMode.ReadWrite, PixelFormat);
            countScan = countData.Scan0;
            
            Parallel.For(0, countSize, (i, _) =>
            {
                unsafe
                {
                    var pixel = (byte*)countScan + i * 4;
                    var r = pixel[2];
                    // if (r >= 190 && r == pixel[0] && r == pixel[1])
                    if (r >= 156 && Math.Abs(pixel[1] - r) <= 15 && Math.Abs(*pixel - r) <= 15)
                        *(uint*)pixel = 0xFF_000000;
                    else
                        *(uint*)pixel = 0xFF_FFFFFF;
                }
            });
            countBitmapResized.UnlockBits(countData);

            using var countPage = EngEngines[slotIndex].Process(countBitmapResized);
            var ocrString = LettersExcludeRegex.Replace(countPage.GetText(), string.Empty);
            var count = string.IsNullOrEmpty(ocrString) ? 1 : int.Parse(ocrString);

            if (print)
            {
                countBitmapResized.Save(Environment.CurrentDirectory + $@"/Screenshots/{id} = {count}.png");
                //countBitmapResized.Save(Environment.CurrentDirectory + @$"\Screenshots\print_{DateTime.Now.ToFileTime()}_c{count}.png");
            }
            
            // if (count == 7)
            //     countBitmapResized.Save(Environment.CurrentDirectory + @$"\Screenshots\{DateTime.Now.ToFileTime()}.png");

            // if (doubleCheck)
            // {
            //     countBitmapResized.Save(Environment.CurrentDirectory + $@"/Screenshots/{DateTime.Now.ToFileTime()}.png");
            // }
            
            // if (!doubleCheck && count > 1 && false)
            // {
            //     try
            //     {
            //         // var matsVector = new VectorOfMat();
            //         // CvInvoke.Split(testImg, matsVector);
            //         // CvInvoke.Threshold(matsVector[1], matsVector[1], 0, 0, ThresholdType.Binary);
            //         // CvInvoke.Merge(matsVector, testImg);
            //
            //         var kernel =
            //             CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(2, 2), new Point(-1, -1));
            //         CvInvoke.MorphologyEx(testImg, testImg, MorphOp.Dilate, kernel,
            //             new Point(-1, -1), 1, BorderType.Isolated, new MCvScalar(255, 0, 0));
            //
            //         var mat = new Mat();
            //         CvInvoke.CvtColor(testImg, mat, ColorConversion.Bgr2Gray);
            //         CvInvoke.CLAHE(mat, 100_000, new Size(4, 4), mat);
            //         CvInvoke.Threshold(mat, mat, 190, 255, ThresholdType.Binary);
            //
            //         CvInvoke.Filter2D(mat, mat, kernel, new Point(-1, -1), 4, BorderType.Isolated);
            //         //var newmat = new Mat();
            //         //CvInvoke.BilateralFilter(mat, newmat, 2, 60, 2);
            //
            //         using var testFinalBitmap = mat.ToBitmap();
            //         testFinalBitmap.Save(Environment.CurrentDirectory +
            //                              @$"\Screenshots\{DateTime.Now.ToFileTime() + _random.Next(0, 10)}_{count}.png");
            //     }
            //     catch // (Exception e)
            //     {
            //         //Console.WriteLine(e);
            //         //throw;
            //     }
            // }

            //btmp.Save(@$"Z:\Development\LilyMarket\debug\{DateTime.Now.ToFileTime()}_{count}.png");
            // try
            // {
            //     countBitmapResized.Save(@$"Z:\Development\LilyMarket\debug\{DateTime.Now.ToFileTime()}_{count}.png");
            // }
            // catch (Exception e)
            // {
            //     Console.WriteLine(e);
            //     throw;
            // }

            if (count == 0)
            {
                lock (WriteLocker)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Count: {count} | \'{ocrString}\'");
                    countBitmapResized.Save(Environment.CurrentDirectory + @$"/Screenshots/{DateTime.Now.ToFileTime()}_{count}.png");
                    forceInstaRestart = true;
                }
            }

            return (count, forceInstaRestart);
        }
        
        public static async Task<bool> BuyProfitSlot(ResultSlot[] slots, ProductTarget target)
        {
            SendLeftButtonKey(_windowHandle, false);
            var sortedByProfit = new List<(int, ResultSlot)>();
            foreach (var resultSlot in slots)
            {
                var profit = target.UnitPrice - resultSlot.UnitPrice;
                profit *= resultSlot.Count;
                sortedByProfit.Add((profit, resultSlot));
            }

            sortedByProfit = sortedByProfit.OrderByDescending(x => x.Item1).ToList();
            var profitSlot = sortedByProfit.First().Item2;

            var slotsLength = slots.Length;
            
            var checkSlotHeight = profitSlot.SlotImage.Height;
            var checkRectangle = SafetyCheckBoxRect with { Height = checkSlotHeight - SafetyCheckBoxRect.Y };
            using var checkImage = profitSlot.SlotImage.Clone(checkRectangle, PixelFormat);
            //profitSlot.SlotImage.Dispose(); // uncomment
            var checkHeight = checkImage.Height;
            var checkWidth = SafetyCheckBoxRect.Width;

            var checkData = checkImage.LockBits(checkRectangle with { X = 0, Y = 0 }, ImageLockMode.ReadWrite, PixelFormat);
            var checkScan = checkData.Scan0;
            var checkStride = checkData.Stride;
            Parallel.For(0, checkHeight, (y, _) =>
            {
                var yOffset = checkScan + y * checkStride;
                Parallel.For(0, checkWidth, (x, _) =>
                {
                    unsafe
                    {
                        var pixel = (byte*)yOffset + x * 4;
                        if (pixel[1] >= 90 && pixel[2] >= 90)
                            *(uint*)pixel = 0xFF_FFFFFF;
                        else
                            *(uint*)pixel = 0xFF_000000;
                    }
                });
            });
            checkImage.UnlockBits(checkData);
            
            var checkPage = _russianOcr.Process(checkImage);
            var ocrString = checkPage.GetText().Trim().ToLower();
            checkPage.Dispose();
            
            if (ocrString != "ост")
            {
                Stop();
                return false;
            }
            
            // check

            await Task.Delay(Settings.Delays.DoubleCheckTimeout);
            var id = DateTime.Now.ToFileTime().ToString();
            using var slotBitmapChecked = CaptureRegion1(Rect.X + SlotsAreaRect.X, Rect.Y + SlotsAreaRect.Y + profitSlot.SlotIndex * SlotRect.Height + 5, SlotsAreaRect.Width, SlotRect.Height);
            //slotBitmapChecked.Save(Environment.CurrentDirectory + $@"/Screenshots/{id}_dobl = c{profitSlot.Count}_p{profitSlot.Price}.png");
            
            var slotData = slotBitmapChecked.LockBits(SlotRect with { Height = slotBitmapChecked.Height }, ImageLockMode.ReadOnly, PixelFormat);
            var slotScan = slotData.Scan0;
            var (countChecked, _) = GetCount(profitSlot.SlotIndex, slotScan);
            
            if (countChecked != profitSlot.Count)
            {
                GetCount(profitSlot.SlotIndex, slotScan, print: true, id: id + "_dubl");
                slotBitmapChecked.UnlockBits(slotData);
                
                //slotBitmapChecked.Save(Environment.CurrentDirectory + $@"/Screenshots/{id}_double = {countChecked}.png");
                //profitSlot.SlotImage.Save(Environment.CurrentDirectory + $@"/Screenshots/{id}_orig = {profitSlot.Count}.png");
                var profitSlotData = profitSlot.SlotImage.LockBits(SlotRect with { Height = slotBitmapChecked.Height }, ImageLockMode.ReadOnly, PixelFormat);
                var profitSlotScan = slotData.Scan0;
                profitSlot.SlotImage.UnlockBits(profitSlotData);
                GetCount(profitSlot.SlotIndex, profitSlotScan, print: true, id: id + "_orig");
                //profitSlot.SlotImage.Save(Environment.CurrentDirectory + $@"/Screenshots/{id}_orig = {profitSlot.Count}.png");
                
                Stop();
                return false;
            }
            slotBitmapChecked.UnlockBits(slotData);
            // Console.WriteLine($"profitSlot c{profitSlot.Count} p{profitSlot.Price}");
            
            var time = DateTime.Now.ToString("dd/hh:mm:ss");
            StringBuilder.Append('[').Append(time).Append("] n(\'").Append(target.Text)
                .Append("\') c(").Append(profitSlot.Count.ToString("N0"))
                .Append(") p(").Append(profitSlot.Price.ToString("N0"))
                .Append(") up(").Append(profitSlot.UnitPrice.ToString("N0")).Append(") : ");

            SendLeftButtonKey(_windowHandle, false);
            await Task.Delay(64);
            var buyButtonX = _buyButtonCheckX;
            var buyButtonY = Rect.Y + SlotsAreaRect.Y + SlotRect.Height * profitSlot.SlotIndex;
            
            await Click(buyButtonX + _random.Next(-350, 100), buyButtonY + _random.Next(0, SlotRect.Height - 2), Settings.Delays.AfterMoveDelays.Slot, 64);
            
            if (_stopped) return false;
            
            buyButtonY += _scrollIndex switch { 0 => 40, 1 => 31, 2 => 22, 3 => 13, 4 => 3, 5 => 3 };

            buyButtonY += 3;
            buyButtonY += _random.Next(0, 12); // height: 26
            buyButtonX += _random.Next(0, 105);
            SetCursorPos(buyButtonX, buyButtonY);
            while (true)
            {
                var buyButtonPixelR = CapturePixelRed(_buyButtonCheckX, buyButtonY);
                if (buyButtonPixelR > 100)
                    break;
                await Task.Delay(2);
            }
            
            // .stop
            // {
            //     var countBitmap = profitSlot.SlotImage.Clone(CountRect, PixelFormat);
            //     var fileId = DateTime.Now.ToFileTime();
            //     countBitmap.Save( Environment.CurrentDirectory + @$"\Screenshots\{fileId}_{profitSlot.Count}_count.png");
            //     profitSlot.SlotImage.Save(Environment.CurrentDirectory + @$"\Screenshots\{fileId}_{profitSlot.Count}.png");
            //     countBitmap.Dispose();
            //     profitSlot.SlotImage.Dispose();
            // }
            // Console.Write(StringBuilder);
            // Environment.Exit(-5);
            
            await Click(buyButtonX, buyButtonY, Settings.Delays.AfterMoveDelays.BuySlot);
            if (_stopped) return false;
            var okButtonY = _boughtOkButtonY;
            
            var repeats = 0;
            while (true)
            {
                var okPixelR = CapturePixelRed(_boughtOkButtonX, okButtonY);
                if (okPixelR > 200)
                    break;
                okPixelR = CapturePixelRed(_boughtOkButtonX, okButtonY + 9);
                if (okPixelR > 200)
                {
                    okButtonY += 9;
                    break;
                }
                await Task.Delay(2);
                repeats++;
                if (_stopped) return false;
                if (repeats == 2000)
                {
                    return false;
                }
            }
            if (_stopped) return false;
            
            keybd_event((byte)VirtualKey.Escape, 0, 0, 0);
            keybd_event((byte)VirtualKey.Escape, 0, 2, 0);

            // Wait for new slots loaded (for sync)
            while (true)
            {
                var slotPixelR = CapturePixelRed(Rect.X + 379, Rect.Y + 172);
                if (slotPixelR > 60)
                    break;
                await Task.Delay(16);
            }
            
            Parallel.For(1, slotsLength, (slot, _) =>
            {
                sortedByProfit[slot].Item2.SlotImage.Dispose();
            });
            
            // default: slot unavaible ; +9px = bought success
            if (okButtonY == _boughtOkButtonY)
            {
                lock (WriteLocker)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    StringBuilder.AppendLine("Sold");
                    Console.Write(StringBuilder);
                    StringBuilder.Clear();
                }
            }
            else
            {
                var newBalance = GetBalance();
                var balanceDiff = _balance - newBalance;

                lock (WriteLocker)
                {
                    if (balanceDiff == profitSlot.Price)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        StringBuilder.AppendLine("Bought");
                        var info = ProductsInfo[target];
                        info.Item1 += balanceDiff;
                        _totalConsumedBalance += balanceDiff;
                        info.Item2 += profitSlot.Count;
                        ProductsInfo[target] = info;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        StringBuilder.Append("Fail. Lost: ").AppendLine(balanceDiff.ToString("N0"));
                        _failConsumedBalance += balanceDiff;
                        _totalConsumedBalance += balanceDiff;
                    }

                    Console.Write(StringBuilder);
                }
                //profitSlot.SlotImage.Save(Environment.CurrentDirectory + @"\Screenshots\" + time + $"({profitSlot.Count}_{profitSlot.Price}).png");
                await File.AppendAllTextAsync(LogPath, StringBuilder.ToString());
                StringBuilder.Clear();
                _balance = newBalance;
            }
            
            profitSlot.SlotImage.Dispose();
            return true;

            void Stop()
            {
                lock (WriteLocker)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Bad scan");
                }

                Parallel.For(1, slotsLength, (slot, _) =>
                {
                    sortedByProfit[slot].Item2.SlotImage.Dispose();
                });
                profitSlot.SlotImage.Dispose();
            }
        }
        
        private static int GetBalance()
        {
            using var balanceBitmap = CaptureRegion(Rect.X + BalanceRect.X, Rect.Y + BalanceRect.Y,
                BalanceRect.Width,
                BalanceRect.Height);
            using var balanceImage = balanceBitmap.ToImage<Gray, byte>();
            Bitmap finalBitmap;
            if (_isCUDASupported)
            {
                using var cudaBitmap = balanceImage.ToBitmap();
                using var cudaBalanceImage = new CudaImage<Gray, byte>(balanceImage);
                CudaInvoke.Threshold(cudaBalanceImage, cudaBalanceImage, 100, 255, ThresholdType.BinaryInv);
                finalBitmap = cudaBalanceImage.ToBitmap();
            }
            else
            {
                CvInvoke.Threshold(balanceImage, balanceImage, 100, 255, ThresholdType.BinaryInv);
                finalBitmap = balanceImage.ToBitmap();
            }
            using var balancePage = _russianOcr.Process(finalBitmap);
            finalBitmap.Dispose();
            var ocrString = LettersExcludeRegex.Replace(balancePage.GetText(), string.Empty);
            if (string.IsNullOrEmpty(ocrString))
            {
                balanceBitmap.Save(Environment.CurrentDirectory + @"\Screenshots\balance.png");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No balance found");
                ocrString = "0";
            }

            return int.Parse(ocrString);
        }

        private static async Task Click(int x, int y, int afterMoveDelay = 0, int afterClickDelay = 0)
        {
            _clickTimer = 0;
            SetCursorPos(x, y);
            if (afterMoveDelay == 0)
                afterMoveDelay = Settings.Delays.MouseAfterMove;
            await Task.Delay(afterMoveDelay);
            SendLeftButtonKey(_windowHandle, true);
            await Task.Delay(48);
            SendLeftButtonKey(_windowHandle, false);
            if (afterClickDelay == 0)
                afterClickDelay = Settings.Delays.MouseAfterClick;
            await Task.Delay(afterClickDelay);
        }

        public static void OnStop()
        {
            StringBuilder.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(new string('-', 48));
            foreach (var productPair in ProductsInfo)
            {
                if (productPair.Value.Item1 != 0)
                {
                    StringBuilder.Append('\'').Append(productPair.Key.Text).Append("\' c(")
                        .Append(productPair.Value.Item2.ToString("N0")).Append(") total(").Append(productPair.Value.Item1.ToString("N0"))
                        .AppendLine(")");
                }
            }
            if (StringBuilder.Length != 0)
            {
                StringBuilder.Append("Total: ").AppendLine(_totalConsumedBalance.ToString("N0"));
            }
            if (_failConsumedBalance != 0)
            {
                StringBuilder.Append("Failed balance: ").AppendLine(_failConsumedBalance.ToString("N0"));
            }

            lock (WriteLocker)
            {
                if (StringBuilder.Length != 0)
                {
                    Console.Write(StringBuilder);
                    File.AppendAllText(LogPath, StringBuilder.ToString());
                }

                StringBuilder.Clear();
            }
        }

        private static void OnKeyDown(VirtualKey key)
        {
            if (key == VirtualKey.LeftMenu && !_hotkeyHolded)
            {
                _hotkeyHolded = true;
                _stopped = !_stopped;
                if (!_stopped) return;

                OnStop();
            }
        }
        
        private static void OnKeyUp(VirtualKey key)
        {
            if (key == VirtualKey.LeftMenu) _hotkeyHolded = false;
        }
    }
}