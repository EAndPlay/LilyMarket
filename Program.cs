using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Tesseract;
using static LilyMarket.DarkSide;
using PageIteratorLevel = Tesseract.PageIteratorLevel;
using Rectangle = System.Drawing.Rectangle;

namespace LilyMarket
{
    internal class Program
    {
        private static Rectangle Rect;
        private static Settings Settings;

        private static readonly Rectangle SlotRect = new(0, 0, 490, 37);
        private static readonly Rectangle SlotToScanRect = new(0, 0, 490, 34);
        private static readonly Rectangle CountRect = new(0, 8, 37, SlotToScanRect.Height - 13); //new(5, 14, 30, 18);
        private static readonly Rectangle PriceRect = new(354, 0, SlotRect.Width - 354, SlotToScanRect.Height - 10); // old: -5
        private static readonly Size CountSize = new Size(CountRect.Width, CountRect.Height) * 2;
        private static readonly Size PriceSize = new Size(PriceRect.Width, PriceRect.Height) * 2;
        
        private static readonly Rectangle SlotsAreaRect = new(378, 172, SlotRect.Width,
            SlotRect.Height * 9 + SlotToScanRect.Height);
        
        private static readonly Rectangle FullPagesAreaRect = new(471, 545, 317, 20);
        private static readonly Rectangle PagesAreaRect = FullPagesAreaRect with { X = 0, Y = 0, Width = FullPagesAreaRect.Width - 12};
        private static readonly Rectangle BalanceRect = new(650, 584, 185, 22);
        private static readonly Rectangle ExtraPagesMarkerRect = new(FullPagesAreaRect.Width - 1, 10, 0, 0);

        private static readonly Rectangle SafetyCheckBoxRect = new(39, 10, 23, -1);
        private static readonly Rectangle PreLastPageCheckRect = new(269, 2, 1, 1);
        
        private static readonly StringBuilder StringBuilder = new();
        private static readonly Regex LettersExcludeRegex = new(@"\D");

        // private static readonly Bgr WhiteBgr = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        // private static readonly Bgr BlackBgr = new(byte.MinValue, byte.MinValue, byte.MinValue);

        private const string TessdataPath = "TesseractOCR_Data";

        private const string LogPath = "results.log";
        private const string ScreenshotsFolder = @"Screenshots\";

        private static TesseractEngine[]? _rusEngines = new TesseractEngine[10];
        private static TesseractEngine[]? _engEngines = new TesseractEngine[10];
        
        private static TesseractEngine? _russianOcr;
        private static TesseractEngine? _englishOcr;
        private const PixelFormat PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;

        private static IntPtr _windowHandle;
        private static IntPtr _selfWindowHandle;
        private static int _pageIndex;
        private static int _scrollIndex;
        private static readonly UserActivityHook ActivityHook = new(false, false);
        private static int _lastScrollIndex;
        private static int _balance = 0;
        private static int _consumedBalance;
        private static int _productsBought;
        private static int _clickTimer;
        private static bool _stopped;
        private static (int, int) _screenResolution;

        private static Random _random = new();
        
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
        
        [STAThread]
        public static void Main()
        {
            Console.Title = GenerateString(16);
            _screenResolution = GetScreenResolution();
            ActivityHook.KeyDown += OnKeyDown;
            ActivityHook.KeyUp += OnKeyUp;
            ActivityHook.Start(false, true);
            //ClipCursorPointer = Marshal.AllocHGlobal(Marshal.SizeOf(new Rectangle()));
            // var rect = new Rectangle(500, 500, 504, 504);  
            // var p = Marshal.AllocHGlobal(Marshal.SizeOf(rect));
            // Marshal.StructureToPtr(rect, p, false);
            // ClipCursor(p);
            //
            // Thread.Sleep(5000);
            // ClipCursor(IntPtr.Zero);
            
            // var str = Assembly.GetCallingAssembly().Location;
            // str = str.Substring(0, str.Length - 3) + "exe";
            // if (DateTime.Now.Minute == 56) return;
            // Console.WriteLine(str);
            // Process.Start(str);
            // Environment.Exit(0);
            // return;
            
            _russianOcr = new(TessdataPath, "rus", EngineMode.TesseractAndLstm);
            _englishOcr = new(TessdataPath, "eng", EngineMode.TesseractOnly);
            _russianOcr.SetVariable("tessedit_char_whitelist", "0123456789ост");
            _russianOcr.SetVariable("debug_file", "/dev/null");
            _russianOcr.SetVariable("applybox_debug", 0);
            //_russianOcr.SetVariable("classify_bln_numeric_mode", 1);
            _englishOcr.SetVariable("tessedit_char_whitelist", "0123456789");
            _englishOcr.SetVariable("debug_file", "/dev/null");
            _englishOcr.SetVariable("classify_bln_numeric_mode", 1);
            _englishOcr.SetVariable("tessedit_do_invert", 0);

            // if (_englishOcr.TryGetIntVariable("classify_enable_adaptive_matcher", out var answer))
            // {
            //     Console.WriteLine(answer);
            // }
            // else
            // {
            //     Console.WriteLine("variable doesn't exist");
            // }
            // return;
            for (int i = 0; i < _rusEngines.Length; i++)
            {
                var engine = _rusEngines[i] = new(TessdataPath, "rus", EngineMode.TesseractAndLstm);
                engine.DefaultPageSegMode = PageSegMode.SingleLine;
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                engine.SetVariable("debug_file", "/dev/null");
                engine.SetVariable("classify_bln_numeric_mode", 1);
                engine.SetVariable("applybox_debug", 0);
                //engine.SetVariable("tessedit_do_invert", 0);
                //engine.SetVariable("tessedit_parallelize", 1);
            }

            for (int i = 0; i < 10; i++)
            {
                var engine = _engEngines[i] = new(TessdataPath, "eng", EngineMode.Default);
                engine.DefaultPageSegMode = PageSegMode.SingleLine;
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                engine.SetVariable("debug_file", "/dev/null");
                engine.SetVariable("classify_bln_numeric_mode", 1);
                engine.SetVariable("applybox_debug", 0);
                //engine.SetVariable("tessedit_do_invert", 0);
                //engine.SetVariable("tessedit_parallelize", 1);
            }
            
            _russianOcr.DefaultPageSegMode = _englishOcr.DefaultPageSegMode = PageSegMode.SingleLine; //single line

            var currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.RealTime;

            _windowHandle = FindWindow(null, "STALCRAFT");
            _selfWindowHandle = currentProcess.MainWindowHandle;
            
            var runTask = Task.Run(async () =>
            {
                while (true)
                {
                    // try
                    // {
                    if (_stopped)
                    {
                        await Task.Delay(100);
                        continue;
                    }
            
                    await StartBot();
                    // }
                    // catch (Exception e)
                    // {
                    //     Console.WriteLine(e);
                    // }
                }
            });
            var lagTask = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    if (_stopped) continue;
                    _clickTimer++;
                    if (_clickTimer >= 15)
                    {
                        _stopped = true;
                        _clickTimer = 0;
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Stopped. Restart");
                        await Task.Delay(1500);
                        _stopped = false;
                    }
                }
            });

            Task.WaitAll(runTask, lagTask);
            //task.Wait();
        }

        private static void Close()
        {
            _stopped = true;
            ActivityHook.KeyDown -= OnKeyDown;
            SendLeftButtonKey(_windowHandle, false);
            Console.WriteLine(new string('-', 48));
            if (_consumedBalance != 0)
            {
                StringBuilder.Clear().Append("Consumed bal: ").Append(_consumedBalance.ToString("N0")).AppendLine()
                    .Append("Products bought: ").Append(_productsBought).AppendLine();
                Console.WriteLine(StringBuilder);
                File.AppendAllText(LogPath, StringBuilder.ToString());
            }

            Settings.StartPage += 1;
            Settings.Save();
            Thread.Sleep(500);
            //Dispose();
            Environment.Exit(0);
        }

        private static bool _hotkeyHolded;
        
        private static void OnKeyDown(VirtualKey key)
        {
            //if (key == VirtualKey.LeftMenu) Close();
            if (key == VirtualKey.LeftMenu && !_hotkeyHolded)
            {
                _hotkeyHolded = true;
                _stopped = !_stopped;
                if (!_stopped) return;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(new string('-', 48));
                if (_consumedBalance != 0)
                {
                    StringBuilder.Clear().Append("Consumed bal: ").Append(_consumedBalance.ToString("N0")).AppendLine()
                        .Append("Products bought: ").Append(_productsBought.ToString("N0")).AppendLine();
                    Console.Write(StringBuilder);
                    _consumedBalance = 0;
                    _productsBought = 0;
                    File.AppendAllText(LogPath, StringBuilder.ToString());
                }
            }
        }

        private static void OnKeyUp(VirtualKey key)
        {
            if (key == VirtualKey.LeftMenu) _hotkeyHolded = false;
        }

        public static async Task StartBot()
        {
            Console.ResetColor();
            Console.Clear();
            Settings = await Settings.Load();
            if (Settings.MinimalUnitPrice == 0)
            {
                Settings.Save();
                Console.WriteLine("Settings file is empty. Fill it.");
                return;
            }

            _lastScrollIndex = Settings.ScrollsCount - 1;

            StringBuilder.Append("Pages: ").Append(Settings.StartPage).Append(" -> ").Append(Settings.PagesCount)
                .AppendLine()
                .Append("Min Unit Price: ").Append(Settings.MinimalUnitPrice.ToString("N0"));
            Console.WriteLine(StringBuilder);
            StringBuilder.Clear();

            Settings.StartPage -= 1;
            
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
            SetWindowPos(_windowHandle, DarkSide.SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y, Rect.Width, Rect.Height,
                DarkSide.SetWindowPosFlags.SHOWWINDOW);
            SetForegroundWindow(_windowHandle);
            await Task.Delay(500);
            SetForegroundWindow(_windowHandle);
            SetWindowPos(_selfWindowHandle, DarkSide.SetWindowPosShowFlags.HWND_TOP, Rect.X, Rect.Y + Rect.Height, 500, 500,
                DarkSide.SetWindowPosFlags.SHOWWINDOW);
            await Task.Delay(500);
            
            const int scrollSize = 81;
            const int scrollXOffset = 885;
            const int scrollYOffset = 99;
            
            var searchButtonX = Rect.X + 825;
            var searchButtonY = Rect.Y + 125;
            var pagesOffsetY = Rect.Y + FullPagesAreaRect.Y + 8;
            var scrollX = Rect.X + scrollXOffset;
            var scrollYCheck = Rect.Y + scrollYOffset + 2;
            var scrollY = Rect.Y + scrollYOffset + 38;

            var nextPageX = Rect.X + FullPagesAreaRect.X + ExtraPagesMarkerRect.X;
            var nextPageY = Rect.Y + FullPagesAreaRect.Y + ExtraPagesMarkerRect.Y;
            
            int pagesOffsetX;
            var pageToReturn = Settings.StartPage;
            var bought = false;
            var preBought = false;
            
            string ocrString;
            int pagesCount, lastPage;
            bool extraPages;
            var scanEnded = true;
            
            void PreInitialize()
            {
                using var balanceBitmap = CaptureScreenshot(Rect.X + BalanceRect.X, Rect.Y + BalanceRect.Y, BalanceRect.Width, BalanceRect.Height);
                // var balanceWidth = balanceBitmap.Width;
                //
                // for (var y = 0; y < balanceBitmap.Height; y++)
                // for (var x = 0; x < balanceWidth; x++)
                //     balanceBitmap.SetPixel(x, y, balanceBitmap.GetPixel(x, y).R > 100 ? Color.Black : Color.White);
                
                using var balanceImage = balanceBitmap.ToImage<Gray, byte>();
                using var cudaBalanceImage = new CudaImage<Gray, byte>(balanceImage);
                CudaInvoke.Threshold(cudaBalanceImage, cudaBalanceImage, 100, 255, ThresholdType.BinaryInv);
                _pageIndex = extraPages ? Settings.StartPage : pageToReturn;
                pageToReturn = Settings.StartPage;
                using var cudaBitmap = cudaBalanceImage.ToBitmap();
                using var balancePage = _russianOcr.Process(cudaBitmap);
                ocrString = LettersExcludeRegex.Replace(balancePage.GetText(), string.Empty);
                if (string.IsNullOrEmpty(ocrString))
                    ocrString = "0";
            }
            
            void FindPages()
            {
                using var fullPagesBitmap = CaptureScreenshot(Rect.X + FullPagesAreaRect.X, Rect.Y + FullPagesAreaRect.Y, FullPagesAreaRect.Width, FullPagesAreaRect.Height);
                extraPages = fullPagesBitmap.GetPixel(ExtraPagesMarkerRect.X, ExtraPagesMarkerRect.Y).R > 128;

                if (extraPages)
                {
                    lastPage = Settings.StartPage + Settings.PagesCount;
                    pagesOffsetX = Rect.X + FullPagesAreaRect.X + ExtraPagesMarkerRect.Y;
                }
                else
                {
                    using var pagesBitmap = fullPagesBitmap.Clone(PagesAreaRect, PixelFormat);
                    
                    // var pagesWidth = pagesBitmap.Width;
                    //
                    // for (var y = 0; y < pagesBitmap.Height; y++)
                    // for (var x = 0; x < pagesWidth; x++)
                    //     pagesBitmap.SetPixel(x, y, pagesBitmap.GetPixel(x, y).R > 160 ? Color.Black : Color.White);
                    
                    using var pagesImage = pagesBitmap.ToImage<Gray, byte>();
                    using CudaImage<Gray, byte> grayPages = new(pagesImage);
                    CudaInvoke.Threshold(grayPages, grayPages, 160, 255, ThresholdType.BinaryInv);
                    using var cudaBitmap = grayPages.ToBitmap();
                    //cudaBitmap.Save(@$"Z:\Development\LilyMarket\pagesBitMap.png");
                    using var ocrPage = _englishOcr.Process(cudaBitmap);
                    //Console.WriteLine($"rawPages: \'{ocrPage.GetText()}\'");
                    ocrString = LettersExcludeRegex.Replace(ocrPage.GetText(), string.Empty);
                    //ocrString = newOcrString;
                    //Console.WriteLine($"PagesOCR: {newOcrString}");
                    
                    var wordLength = ocrString.Length;
                    pagesCount = wordLength < 10 ? wordLength : 9 + (wordLength - 9) / 2;
                    
                    lastPage = Math.Min(pagesCount - 1, Settings.StartPage + Settings.PagesCount);
                    var pagesRegion = ocrPage.GetSegmentedRegions(PageIteratorLevel.Symbol)[0];
                    pagesOffsetX = Rect.X + FullPagesAreaRect.X + pagesRegion.X + 1;
                    
                    //Console.WriteLine(pagesCount + " ; " + ocrString);
                }
            }
            
            {
                using var firstScrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                var firstPixelColor = firstScrollBitmap.GetPixel(0, 0);
                if (firstPixelColor.R >= 100)
                {
                    await Click(scrollX, scrollYCheck + 400);

                    while (true)
                    {
                        using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                        var pixelColor = scrollBitmap.GetPixel(0, 0);
                        if (pixelColor.R < 100)
                        {
                            await Click(scrollX, scrollYCheck + 400);
                            break;
                        }

                        await Task.Delay(16);
                    }
                }
            }

            FindPages();
            //PreInitialize();
            //_balance = int.Parse(ocrString);
            //StringBuilder.Append("Bal: ").Append(_balance.ToString("N0"));
            //Console.WriteLine(StringBuilder.ToString());
            //StringBuilder.Clear();
            
            (bool, int, int, int) scanResult = (false, 0, 0, 0);
            Label_StartScan:
            _clickTimer = 0;
            SendLeftButtonKey(_windowHandle, false);
            PreInitialize();
            var newBalance = int.Parse(ocrString);
            if (bought)
            {
                preBought = true;
                const int messageOffset = 14;
                var balanceDiff = _balance - newBalance;
                if (balanceDiff == 0)
                {
                    StringBuilder.Insert(messageOffset, "Sold");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                else if (balanceDiff != scanResult.Item3)
                {
                    // if (Settings.Screenshots)
                    // {
                    //     scanResult.Item5?.Save(StringBuilder.ToString());
                    // }
                    StringBuilder.Insert(messageOffset, "Buy").Append("Fail. Expected: ").Append(scanResult.Item3.ToString("N0"))
                        .Append(". Lost: ").AppendLine(balanceDiff.ToString("N0"));
                    _consumedBalance += balanceDiff;
                    Console.ForegroundColor = ConsoleColor.Red;
                    //StringBuilder.Append("Bal: ").Append(newBalance.ToString("N0"));
                    //Console.WriteLine(StringBuilder.ToString());
                }
                else
                {
                    _consumedBalance += balanceDiff;
                    _productsBought += scanResult.Item4;
                    StringBuilder.Insert(messageOffset, "Buy");
                    Console.ForegroundColor = ConsoleColor.Green;
                    //StringBuilder.Clear().Append("Bal: ").Append(newBalance.ToString("N0"));
                    //Console.WriteLine(StringBuilder.ToString());
                }

                await File.AppendAllTextAsync(LogPath, StringBuilder.ToString());
                Console.Write(StringBuilder);
                StringBuilder.Clear();

                bought = false;
            }
            _balance = newBalance;
            //Console.WriteLine("balance: " + _balance);
            if (_stopped) return;
            const int pagesDiffOffsetX = 24;
            if (scanEnded)
            {
                if (extraPages)
                {
                    await Click(searchButtonX, searchButtonY);
                    while (true)
                    {
                        using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                        var pixelColor = scrollBitmap.GetPixel(0, 0);
                        if (pixelColor.R >= 100)
                            break;
                        await Task.Delay(16);
                    }
                    await Task.Delay(64);
                    
                    if (_pageIndex != 0)
                    {
                        await Click(scrollX, scrollYCheck + 400);
                        while (true)
                        {
                            using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                            var pixelColor = scrollBitmap.GetPixel(0, 0);
                            if (pixelColor.R < 100)
                                break;
                            await Task.Delay(16);
                            await Click(scrollX, scrollYCheck + 400);
                        }
                        await Click(pagesOffsetX + pagesDiffOffsetX * _pageIndex, pagesOffsetY, 48);
                        
                        while (true)
                        {
                            using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                            var pixelColor = scrollBitmap.GetPixel(0, 0);
                            if (pixelColor.R >= 100)
                                break;
                            await Task.Delay(16);
                        }
                    }
                    else
                    {
                        if (preBought)
                        {
                            if (_pageIndex == 0)
                                await Task.Delay(1000);
                            preBought = false;
                        }
                    }
                }
                scanEnded = false;
            }

            for (; _pageIndex < lastPage; _pageIndex++)
            {
                SendLeftButtonKey(_windowHandle, false);
                var pageRepeats = 0;
                Label_SwapPage:
                {
                    if (extraPages)
                    {
                        if (_pageIndex != Settings.StartPage)
                        {
                            var preLastPageImage = CaptureScreenshot(Rect.X + FullPagesAreaRect.X + PreLastPageCheckRect.X, Rect.Y + FullPagesAreaRect.Y + PreLastPageCheckRect.Y, 1, 1);
                            if (preLastPageImage.GetPixel(0, 0).R > 100)
                            {
                                preLastPageImage.Dispose();
                                scanEnded = true;
                                goto Label_StartScan;
                            }
                            preLastPageImage.Dispose();
                            await Click(nextPageX, nextPageY, 48);
                        }
                        else if (_pageIndex == 0 && preBought)
                        {
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        await Click(pagesOffsetX + pagesDiffOffsetX * _pageIndex, pagesOffsetY, 48);
                        if (preBought && _pageIndex == 0) await Task.Delay(1000);
                    }

                    preBought = false;

                    if (_stopped) return;
                    
                    while (true)
                    {
                        using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                        var pixelColor = scrollBitmap.GetPixel(0, 0);
                        if (pixelColor.R >= 100)
                            break;
                        pageRepeats++;
                        switch (pageRepeats)
                        {
                            case 16:
                                goto Label_SwapPage;
                            case 64:
                                FindPages();
                                scanEnded = true;
                                goto Label_StartScan;
                        }

                        await Task.Delay(64);
                    }
                }
                //Thread.Sleep(32);
                //SetCursorPos(scrollX, scrollY);

                _scrollIndex = 0;

                SetCursorPos(scrollX, scrollY);
                await Task.Delay(64);
                SendLeftButtonKey(_windowHandle, true);
                // var scanResult = NewHandleSlots(9, out scannedSlots, out boughtPrice, out boughtCount, out slotBitmap);
                scanResult = await HandleSlots(9);
                if (scanResult.Item3 != 0)
                {
                    bought = true;
                    pageToReturn = extraPages ? Settings.StartPage : _pageIndex;
                        
                    while (true)
                    {
                        using var slotPixelBitmap = CaptureScreenshot(Rect.X + 380, Rect.Y + 180, 1, 1);
                        var pixelColor = slotPixelBitmap.GetPixel(0, 0);
                        if (pixelColor.R >= 24)
                            break;
                        await Task.Delay(32);
                    }
                    await Task.Delay(64);
                    if (pageToReturn != 0)
                    {
                        var repeats = 0;
                        await Click(scrollX, scrollYCheck + 400, afterDelay:96);
                        while (true)
                        {
                            using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                            var pixelColor = scrollBitmap.GetPixel(0, 0);
                            if (pixelColor.R < 100)
                                break;
                            repeats++;
                            if (repeats == 11)
                            {
                                await Click(scrollX, scrollYCheck + 400, afterDelay:96);
                                repeats = 0;
                                continue;
                            }
                            await Task.Delay(48);
                        }
                    }

                    scanEnded = true;
                    
                    goto Label_StartScan;
                }
                
                // var freeSlots = 9 - scanResult.Item2;
                // if (freeSlots != 0)
                //     await Task.Delay(5 * freeSlots);

                //await Click(scrollX, scrollY);
                await Task.Delay(48);
                Label_StartScroll:
                for (_scrollIndex = 0; _scrollIndex < Settings.ScrollsCount; _scrollIndex++)
                {
                    if (_stopped)
                    {
                        SendLeftButtonKey(_windowHandle, false);
                        return;
                    }
                    SetCursorPos(scrollX, scrollY + scrollSize * (_scrollIndex + 1));
                    await Task.Delay(80);

                    if (_scrollIndex == 0)
                    {
                        using var scrollBitmap1 = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                        if (scrollBitmap1.GetPixel(0, 0).R > 100)
                        {
                            SendLeftButtonKey(_windowHandle, false);
                            SetCursorPos(scrollX, scrollY);
                            await Task.Delay(64);
                            SendLeftButtonKey(_windowHandle, true);
                            goto Label_StartScroll;
                            // await Click(scrollX, scrollYCheck + 400);
                            // Thread.Sleep(64);
                            // while (true)
                            // {
                            //     using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                            //     var pixelColor = scrollBitmap.GetPixel(0, 0);
                            //     if (pixelColor.R < 100)
                            //         break;
                            //     Thread.Sleep(64);
                            // }
                            //
                            // Thread.Sleep(64);
                        }
                    }

                    var maxSlots = _scrollIndex == _lastScrollIndex ? 10 : 9;
                    _scrollIndex += 1;
                    // scanResult = NewHandleSlots(maxSlots, out scannedSlots, out boughtPrice, out boughtCount, out slotBitmap);
                    scanResult = await HandleSlots(maxSlots);
                    if (scanResult.Item1)
                    {
                        if (scanResult.Item3 != 0)
                        {
                            bought = true;
                            pageToReturn = extraPages ? Settings.StartPage : _pageIndex;

                            while (true)
                            {
                                using var slotPixelBitmap = CaptureScreenshot(Rect.X + 380, Rect.Y + 180, 1, 1);
                                var pixelColor = slotPixelBitmap.GetPixel(0, 0);
                                if (pixelColor.R >= 24)
                                    break;
                                await Task.Delay(32);
                            }

                            await Task.Delay(64);
                            if (pageToReturn != 0)
                            {
                                var repeats = 0;
                                await Click(scrollX, scrollYCheck + 400, afterDelay: 96);
                                while (true)
                                {
                                    using var scrollBitmap = CaptureScreenshot(scrollX, scrollYCheck, 1, 1);
                                    var pixelColor = scrollBitmap.GetPixel(0, 0);
                                    if (pixelColor.R < 100)
                                        break;
                                    repeats++;
                                    if (repeats == 11)
                                    {
                                        await Click(scrollX, scrollYCheck + 400, afterDelay: 96);
                                        repeats = 0;
                                        continue;
                                    }

                                    await Task.Delay(48);
                                }
                            }

                            scanEnded = true;
                            
                            goto Label_StartScan;
                        }
                    }
                    else
                    {
                        break;
                    }
                    
                    _scrollIndex -= 1;
                    // freeSlots = maxSlots - scanResult.Item2;
                    // if (freeSlots != 0)
                    //     await Task.Delay(10 * freeSlots);
                }

                FindPages();
            }

            scanEnded = true;
            SendLeftButtonKey(_windowHandle, false);
            
            goto Label_StartScan;
        }
        
        // private static async Task<bool> NewHandleSlots(int slotsToScan, out int scannedSlots, out int boughtPrice, out int boughtCount, out Bitmap returnSlot)
        private static async Task<(bool, int, int, int)> HandleSlots(int slotsToScan)
        {
            //Bitmap? returnSlot = null;
            var slotToBuyIndex = -1;

            using var windowShot = CaptureScreenshot(_windowHandle);
            using var slotsMap = windowShot.Clone(SlotsAreaRect, PixelFormat);
            var slotBitmaps = new Bitmap[slotsToScan];
            for (var i = 0; i < slotsToScan; i++)
            {
                var lastSlotOffset = i == 9 ? 5 : 0;
                slotBitmaps[i] = slotsMap.Clone(
                    SlotRect with
                    {
                        Y = SlotRect.Height * i + 5, Height = SlotToScanRect.Height - lastSlotOffset
                    }, PixelFormat);
            }

            var foundPreBreak = false;
            int outPrice = 0, outCount = 0, outUnitPrice = 0, outScannedSlots = 0;
            var lastIndex = 0;
            Parallel.For(0, slotsToScan, (slotIndex, loopState) =>
            {
                //var lastSlotOffset = slotIndex == 9 ? 5 : 0;
                var slotBitmap = slotBitmaps[slotIndex];
                int price = 0, count = 0;
                var doCancel = false;
                //int rusCount = 0;
                var unavailableSlot = false;
                //string countOcr = null;
                var priceBitmap = slotBitmap.Clone(PriceRect, PixelFormat);
                var countBitmap = slotBitmap.Clone(CountRect, PixelFormat);

                Parallel.Invoke(new ParallelOptions(), () =>
                    {
                        // using var priceImage1 = priceBitmap.ToImage<Bgr, byte>();
                        // priceBitmap.Dispose();
                        // //countImage1.ToBitmap().Save(@$"Z:\Development\SCMarket_csharp_net8\slot001.png");
                        // using var priceImage2 = new Image<Bgr, byte>(PriceSize);
                        // using var priceImage = new Image<Gray, byte>(PriceSize);
                        // CvInvoke.ResizeForFrame(priceImage1, priceImage2, PriceSize);
                        // CvInvoke.CvtColor(priceImage2, priceImage, ColorConversion.Bgr2Gray);
                        // //priceImage.ToBitmap().Save(@$"Z:\Development\LilyMarket\price_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}_c{count}.png");
                        // CvInvoke.Threshold(priceImage, priceImage, 160, 255, ThresholdType.BinaryInv);
                        // using var countBitmapResized = priceImage.ToBitmap();
                        
                        //countBitmapResized.Save(@$"Z:\Development\LilyMarket\price_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}_c{count}.png");
                        // var priceWidth = priceBitmap.Width;
                        // for (var y = 0; y < priceBitmap.Height; y++)
                        // for (var x = 0; x < priceWidth; x++)
                        //     priceBitmap.SetPixel(x, y, priceBitmap.GetPixel(x, y).R > 100 ? Color.Black : Color.White);

                        //priceBitmap.Save(@$"Z:\Development\SCMarket_csharp_net8\price_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}_c{count}.png");
                        using var pricePage = _rusEngines![slotIndex].Process(priceBitmap);
                        priceBitmap.Dispose();
                        var ocrString = LettersExcludeRegex.Replace(pricePage.GetText(), string.Empty);
                        if (ocrString.StartsWith('0'))
                        {
                            unavailableSlot = true;
                            return;
                        }
                        //Console.WriteLine("ocr: " + ocrString);
                        
                        if (!int.TryParse(ocrString, out price))
                        {
                            unavailableSlot = true;
                            if (lastIndex != 0 && slotIndex > lastIndex)
                            {
                                //Console.WriteLine($"{slotIndex} {lastIndex}");
                                slotBitmap.Dispose();
                                foundPreBreak = true;
                            }

                            return;
                        }

                        if (slotIndex > lastIndex)
                            lastIndex = slotIndex;
                        outScannedSlots++;
                    },
                    () =>
                    {
                        using var countImage1 = countBitmap.ToImage<Bgr, byte>();
                        countBitmap.Dispose();
                        //countImage1.ToBitmap().Save(@$"Z:\Development\SCMarket_csharp_net8\slot001.png");
                        using var countImage = new Image<Bgr, byte>(CountSize);
                        CvInvoke.ResizeForFrame(countImage1, countImage, CountSize, Inter.Linear);
                        using var countBitmapResized = countImage.ToBitmap();
                        //countImage.ToBitmap().Save(@$"Z:\Development\SCMarket_csharp_net8\slot000.png");

                        var countWidth = CountSize.Width;
                        for (var y = 0; y < CountSize.Height; y++)
                        {
                            for (var x = 0; x < countWidth; x++)
                            {
                                var pixel = countBitmapResized.GetPixel(x, y);
                                var r = pixel.R;
                                if (r >= 164 && Math.Abs(pixel.G - r) <= 15 && Math.Abs(pixel.B - r) <= 15)
                                    countBitmapResized.SetPixel(x, y, Color.Black);
                                else
                                    countBitmapResized.SetPixel(x, y, Color.White);
                            }
                        }

                        using var countPage = _engEngines[slotIndex].Process(countBitmapResized);
                        var ocrString = LettersExcludeRegex.Replace(countPage.GetText(), string.Empty);
                        count = string.IsNullOrEmpty(ocrString) ? 1 : int.Parse(ocrString);

                        if (count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Count: {count} | {ocrString}");
                            Console.ResetColor();
                            //countBitmapResized.Save(@$"Z:\Development\SCMarket_csharp_net8\slot_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}_c{count}.png");
                        }
                        
                        // using var countPage1 = _engEngines[10 + slotIndex].Process(countBitmapResized);
                        // ocrString = LettersExcludeRegex.Replace(countPage1.GetText(), string.Empty);
                        // rusCount = string.IsNullOrEmpty(ocrString) ? 1 : int.Parse(ocrString);
                    });

                if (unavailableSlot)
                {
                    //slotBitmaps[slotIndex].Dispose();
                    return;
                }

                // if (count == 0)
                // {
                //     Console.WriteLine("count==0: countOcr=\'" + countOcr + '\'');
                // }
                // if (count != rusCount)
                // {
                //     //countBitmap1.Save(@$"Z:\Development\SCMarket_csharp_net8\slot_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}.png");
                //     Console.ForegroundColor = ConsoleColor.Red;
                //     Console.WriteLine($"{count} != {rusCount} (p{_pageIndex}_s{_scrollIndex}_i{slotIndex})");
                //     return;
                // }
                // else
                // {
                //     countBitmap1.Save(@$"Z:\Development\SCMarket_csharp_net8\slot_p{_pageIndex}_s{_scrollIndex}_i{slotIndex}_c{count}.png");
                //     //Console.ForegroundColor = ConsoleColor.Black;
                //     //Console.WriteLine($"{count} == {rusCount}");
                // }
                //Console.WriteLine(count);
                if (doCancel) return;
                var unitPrice = price / count;
                if (count > 100)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"count=={count}");
                    return;
                }
                if (unitPrice <= Settings.MinimalUnitPrice && count <= 100)
                {
                    // var time1 = DateTime.Now.ToString("dd/hh:mm:ss");
                    // StringBuilder.Append('[').Append(time1).Append("] : c(").Append(count.ToString("N0"))
                    //     .Append(") p(")
                    //     .Append(price.ToString("N0"))
                    //     .Append(") up(").Append(unitPrice.ToString("N0")).Append(')');
                    // Console.WriteLine(StringBuilder);
                    // slotBitmaps[slotIndex].Dispose();
                    // return;
                    if (_balance >= price)
                    {
                        doCancel = true;
                        // if (count % 10 == 8 || count % 10 == 6)
                        // {
                        //     var buyButtonX = Rect.X + SlotsAreaRect.X + PriceRect.X + PriceRect.Width / 2;
                        //     var buyButtonY = Rect.Y + SlotsAreaRect.Y + SlotRect.Height * slotIndex + 15;
                        //     SendLeftButtonKey(_windowHandle, false);
                        //     SetCursorPos(buyButtonX, buyButtonY);
                        //     Console.WriteLine(count);
                        //     Environment.Exit(0);
                        // }

                        outPrice = price;
                        outCount = count;
                        outUnitPrice = unitPrice;
                        slotToBuyIndex = slotIndex;
                        loopState.Break();
                        //slotBitmap.Dispose();
                        return;
                    }

                    var time = DateTime.Now.ToString("dd/hh:mm:ss");
                    StringBuilder.Append('[').Append(time).Append("] Not enough balance : c(")
                        .Append(count.ToString("N0"))
                        .Append(") p(")
                        .Append(price.ToString("N0"))
                        .Append(") up(").Append(unitPrice.ToString("N0")).Append(')');
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(StringBuilder);
                    StringBuilder.Clear();
                }

                slotBitmaps[slotIndex].Dispose();
            });
            StringBuilder.Clear();

            if (slotToBuyIndex != -1)
            {
                var slotToBuy = slotBitmaps[slotToBuyIndex];
                var checkSlotHeight = slotToBuy.Height;
                using var checkImage =
                    slotToBuy.Clone(
                        SafetyCheckBoxRect with { Height = checkSlotHeight - SafetyCheckBoxRect.Y },
                        PixelFormat);
                var checkHeight = checkImage.Height;
                for (var y = 0; y < checkHeight; y++)
                {
                    for (var x = 0; x < SafetyCheckBoxRect.Width; x++)
                    {
                        var pixel = checkImage.GetPixel(x, y);
                        if (pixel.R >= 90 && pixel.G >= 90)
                            checkImage.SetPixel(x, y, Color.White);
                        else
                            checkImage.SetPixel(x, y, Color.Black);
                    }
                }

                using var checkPage = _russianOcr.Process(checkImage);
                var ocrString = checkPage.GetText().Trim().ToLower();
                if (ocrString != "ост")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Bad scan");
                    return (false, 0, 0, 0);
                }

                var time = DateTime.Now.ToString("dd/hh:mm:ss");
                StringBuilder.Append('[').Append(time).Append("] : c(").Append(outCount.ToString("N0"))
                    .Append(") p(")
                    .Append(outPrice.ToString("N0"))
                    .Append(") up(").Append(outUnitPrice.ToString("N0")).Append(')');
                // if (_balance < outPrice)
                // {
                //     StringBuilder.Insert(11, "Not enough balance").AppendLine();
                //     Console.Write(StringBuilder);
                //     File.AppendAllText(LogPath, StringBuilder.ToString());
                //     StringBuilder.Clear();
                //     continue;
                // }

                //StringBuilder.Insert(11, "Buy");
                SendLeftButtonKey(_windowHandle, false);
                await Task.Delay(64);
                //Console.WriteLine(StringBuilder);
                //continue;
                var buyButtonX = Rect.X + SlotsAreaRect.X + PriceRect.X + PriceRect.Width / 2;
                var buyButtonY = Rect.Y + SlotsAreaRect.Y + SlotRect.Height * slotToBuyIndex + 15;

                await Click(buyButtonX, buyButtonY, afterMoveDelay:96);
                buyButtonY += _scrollIndex switch { 0 => 40, 1 => 35, 2 => 25, 3 => 15, 4 => 5, 5 => 0 };
                await Click(buyButtonX, buyButtonY, afterMoveDelay:64);
                StringBuilder.AppendLine();
                //await File.AppendAllTextAsync(LogPath, StringBuilder.ToString());
                // if (Settings.Screenshots)
                // {
                //     time = time.Replace(':', '_');
                //     StringBuilder.Clear().Append(ScreenshotsFolder).Append(time).Append(".png");
                //     returnSlot = slotBitmaps[slotToBuy];
                //     //slotBitmap.Save(StringBuilder.ToString());
                // }

                //slotBitmap.Dispose();
                //StringBuilder.Clear();

                var repeats = 0;
                while (true)
                {
                    using var okBitmap = CaptureScreenshot(Rect.X + 256, Rect.Y + 392, 1, 1);
                    if (okBitmap.GetPixel(0, 0).R >= 24)
                        break;
                    await Task.Delay(2);
                    repeats++;
                    if (repeats == 2000)
                    {
                        return (!foundPreBreak, outScannedSlots, 0, 0);
                    }
                }

                await Click(Rect.X + 445, Rect.Y + 370, 96);
                
                repeats = 0;
                while (true)
                {
                    using var okBitmap = CaptureScreenshot(Rect.X + 256, Rect.Y + 392, 1, 1);
                    if (okBitmap.GetPixel(0, 0).R < 24)
                        break;
                    await Task.Delay(2);
                    repeats++;
                    if (repeats == 1000)
                    {
                        await Click(Rect.X + 445, Rect.Y + 370, 96);
                    }
                    if (repeats == 2000)
                    {
                        return (!foundPreBreak, outScannedSlots, 0, 0);
                    }
                }

                return (true, outScannedSlots, outPrice, outCount);
            }

            StringBuilder.Clear();
            return (!foundPreBreak, outScannedSlots, 0, 0);
        }
        
        private static async Task Click(int x, int y, int afterMoveDelay = 32, int afterDelay = 32)
        {
            _clickTimer = 0;
            SetCursorPos(x, y);
            await Task.Delay(afterMoveDelay);
            SendLeftButtonKey(_windowHandle, true);
            await Task.Delay(48);
            SendLeftButtonKey(_windowHandle, false);
            await Task.Delay(afterDelay);
        }

        private static void Dispose()
        {
            // ClipCursor(IntPtr.Zero);
            _russianOcr.Dispose();
            _englishOcr.Dispose();
        }
    }
}