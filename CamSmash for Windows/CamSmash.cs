// =====================================================================
//  카메라 파괴소 (CamSmash) - 윈도우판
//  빌드: build_camsmash.bat  (Windows 내장 .NET Framework 4.x csc.exe, C# 5 문법)
//
//  - 화면: 픽셀 단위 소프트웨어 렌더러 + 블룸(빛 번짐) 후처리
//  - 소리: winmm waveOut 실시간 신시사이저 (동시 재생, 좌우 위치, 울림)
//  - 네트워크·레지스트리·파일 쓰기 없음
//
//  조작: 1~8 도구 / R 다시 소환 / C 잔해 치우기 / S 슬로모션 / M 소리 / F11 전체화면
// =====================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("CamSmash")]
[assembly: AssemblyDescription("Camera smashing toy. No network, no registry, no file writes.")]
[assembly: AssemblyProduct("CamSmash")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace CamSmash
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Native.timeBeginPeriod(1);
            try { Application.Run(new MainForm()); }
            finally { Native.timeEndPeriod(1); }
        }
    }

    // =================================================================
    //  Win32
    // =================================================================
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint p);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint p);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount;
            public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        public const uint SRCCOPY = 0x00CC0020;

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX { public ushort wFormatTag; public ushort nChannels; public uint nSamplesPerSec; public uint nAvgBytesPerSec; public ushort nBlockAlign; public ushort wBitsPerSample; public ushort cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength; public uint dwBytesRecorded; public IntPtr dwUser; public uint dwFlags; public uint dwLoops; public IntPtr lpNext; public IntPtr reserved; }
        [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr hwo, int dev, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr hwo);
    }

    // =================================================================
    //  데이터
    // =================================================================
    sealed class BodyDef
    {
        public string Name; public int Price; public int W, H, VGrip; public string Hump; public int HumpW, HumpH;
        public int Grip; public double MX, MY; public int MountR; public bool SilverTop, Dials, Leather, Ovf;
    }
    sealed class LensDef
    {
        public string Name; public int Price; public int R; public double G; public string Color; public int[] Accent; public bool Zoom, Foot;
    }
    sealed class BrandDef { public string Name; public int Dot; public BodyDef[] Bodies; public LensDef[] Lenses; }

    static class Catalog
    {
        static BodyDef BD(string name, int price, int w, int h, string hump, int hw, int hh, int grip, double mx, double my, int mountR, string flags = "", int vgrip = 0)
        {
            BodyDef d = new BodyDef();
            d.Name = name; d.Price = price; d.W = w; d.H = h; d.Hump = hump; d.HumpW = hw; d.HumpH = hh; d.Grip = grip;
            d.MX = mx; d.MY = my; d.MountR = mountR; d.VGrip = vgrip;
            d.SilverTop = flags.Contains("silver"); d.Dials = flags.Contains("dials"); d.Leather = flags.Contains("leather"); d.Ovf = flags.Contains("ovf");
            return d;
        }
        static LensDef LD(string name, int price, int r, double g, string color, int[] accent, string flags = "")
        {
            LensDef d = new LensDef();
            d.Name = name; d.Price = price; d.R = r; d.G = g; d.Color = color; d.Accent = accent;
            d.Zoom = flags.Contains("zoom"); d.Foot = flags.Contains("foot");
            return d;
        }
        static int[] A(int r, int g, int b) { return new int[] { r, g, b }; }

        public static readonly BrandDef[] Brands = new BrandDef[]
        {
            new BrandDef { Name = "Canon", Dot = 0xD9493D,
                Bodies = new BodyDef[] {
                    BD("EOS R5", 4600000, 150, 92, "evf", 60, 20, 30, .56, .55, 35),
                    BD("EOS R6 Mark II", 3200000, 144, 90, "evf", 56, 19, 30, .56, .55, 35),
                    BD("EOS R3", 7500000, 154, 92, "prism", 66, 24, 32, .56, .53, 35, "", 40),
                    BD("EOS 5D Mark IV", 3500000, 162, 100, "prism", 72, 32, 32, .55, .55, 37) },
                Lenses = new LensDef[] {
                    LD("RF 24-70mm F2.8 L IS USM", 3200000, 42, .6, "black", A(200, 40, 36), "zoom"),
                    LD("RF 50mm F1.2 L USM", 3000000, 45, .72, "black", A(200, 40, 36)),
                    LD("RF 70-200mm F2.8 L IS USM", 3500000, 44, .62, "white", A(200, 40, 36), "zoom"),
                    LD("RF 50mm F1.8 STM", 250000, 30, .55, "black", null) } },
            new BrandDef { Name = "Sony", Dot = 0xE0852F,
                Bodies = new BodyDef[] {
                    BD("α7 IV", 3000000, 140, 86, "evf", 50, 17, 30, .6, .55, 32),
                    BD("α7R V", 4800000, 142, 88, "evf", 52, 18, 31, .6, .55, 32),
                    BD("α1 II", 8000000, 142, 88, "evf", 54, 18, 32, .6, .55, 32),
                    BD("α6700", 2000000, 128, 76, null, 0, 0, 28, .62, .55, 29) },
                Lenses = new LensDef[] {
                    LD("FE 24-70mm F2.8 GM II", 2900000, 41, .6, "black", A(214, 112, 42), "zoom"),
                    LD("FE 85mm F1.4 GM", 2000000, 42, .7, "black", A(214, 112, 42)),
                    LD("FE 70-200mm F2.8 GM OSS II", 3400000, 43, .62, "white", A(214, 112, 42), "zoom foot"),
                    LD("FE 50mm F1.8", 300000, 30, .56, "black", null) } },
            new BrandDef { Name = "Nikon", Dot = 0xE7C320,
                Bodies = new BodyDef[] {
                    BD("Z8", 5000000, 150, 100, "angular", 64, 24, 32, .56, .55, 38),
                    BD("Z9", 7000000, 152, 100, "angular", 66, 26, 33, .56, .53, 38, "", 42),
                    BD("Zf", 2700000, 146, 92, "angular", 52, 22, 18, .54, .55, 38, "dials leather"),
                    BD("D850", 3700000, 162, 104, "prism", 74, 32, 33, .55, .55, 36) },
                Lenses = new LensDef[] {
                    LD("NIKKOR Z 24-70mm f/2.8 S", 2900000, 42, .6, "black", A(176, 176, 178), "zoom"),
                    LD("NIKKOR Z 50mm f/1.2 S", 2800000, 47, .7, "black", A(176, 176, 178)),
                    LD("NIKKOR Z 70-200mm f/2.8 VR S", 3300000, 44, .62, "black", A(176, 176, 178), "zoom foot"),
                    LD("NIKKOR Z 40mm f/2", 300000, 32, .5, "black", null) } },
            new BrandDef { Name = "Fujifilm", Dot = 0x3FA36B,
                Bodies = new BodyDef[] {
                    BD("X-T5", 2500000, 132, 86, "angular", 46, 22, 16, .52, .55, 30, "silver dials leather"),
                    BD("X-H2S", 3200000, 140, 92, "evf", 50, 20, 30, .56, .55, 30),
                    BD("X-Pro3", 2300000, 146, 82, null, 0, 0, 14, .5, .55, 30, "dials leather ovf"),
                    BD("GFX100S II", 6500000, 160, 104, "evf", 60, 20, 32, .56, .55, 40) },
                Lenses = new LensDef[] {
                    LD("XF 16-55mm F2.8 R LM WR", 1400000, 38, .6, "black", null, "zoom"),
                    LD("XF 56mm F1.2 R WR", 1300000, 36, .68, "black", null),
                    LD("XF 50-140mm F2.8 R LM OIS WR", 2000000, 40, .62, "black", null, "zoom foot"),
                    LD("XF 35mm F2 R WR", 500000, 26, .5, "silver", null) } },
        };

        public static readonly string[] ToolIds = { "hammer", "drill", "saw", "flame", "laser", "bomb", "hole", "hand" };
        public static readonly string[] ToolNames = { "망치", "드릴", "원형톱", "화염방사기", "레이저", "폭탄", "블랙홀", "손" };
    }

    // =================================================================
    //  공용 유틸
    // =================================================================
    static class U
    {
        public static readonly Random R = new Random();
        public static double Rnd(double a) { return R.NextDouble() * a; }
        public static double Rnd(double a, double b) { return a + R.NextDouble() * (b - a); }
        public static int C(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }
        public static int Rgb(double r, double g, double b) { return (C(r) << 16) | (C(g) << 8) | C(b); }
        public static double Clamp(double v, double a, double b) { return v < a ? a : v > b ? b : v; }
        public static double Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296.0;
            }
        }
        public static string Won(double v) { return "₩" + Math.Round(v).ToString("N0", CultureInfo.InvariantCulture); }
        public static Color Col(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    }

    // =================================================================
    //  재질
    // =================================================================
    static class Mt { public const byte EMPTY = 0, PLASTIC = 1, METAL = 2, RUBBER = 3, GLASS = 4, PCB = 5, SENSOR = 6, BATTERY = 7, LEATHER = 8; }
    static class Dt { public const int BLUNT = 0, CUT = 1, LASER = 2, EXPLODE = 3, SUCK = 4, HEAT = 5; }

    sealed class MatInfo
    {
        public double Hp; public double[] Res; public double Burn; public bool Metal, Glass, Spark, Battery;
        public MatInfo(double hp, double blunt, double cut, double laser, double explode, double burn)
        {
            Hp = hp; Res = new double[] { blunt, cut, laser, explode, 1, 1 }; Burn = burn;
        }
        public static readonly MatInfo[] All = Make();
        static MatInfo[] Make()
        {
            MatInfo[] m = new MatInfo[9];
            m[Mt.PLASTIC] = new MatInfo(4, 1, 1, 1, 1, .6);
            m[Mt.METAL] = new MatInfo(10, .45, .55, .7, .8, 0); m[Mt.METAL].Metal = true;
            m[Mt.RUBBER] = new MatInfo(5, .6, .9, 1, .9, .45);
            m[Mt.GLASS] = new MatInfo(2, 2.6, 1.3, 1.6, 1.6, 0); m[Mt.GLASS].Glass = true;
            m[Mt.PCB] = new MatInfo(3, 1, 1.2, 1.2, 1.2, .8); m[Mt.PCB].Spark = true;
            m[Mt.SENSOR] = new MatInfo(2, 2.2, 1.2, 1.4, 1.5, 0); m[Mt.SENSOR].Glass = true;
            m[Mt.BATTERY] = new MatInfo(5, .8, 1, 1, 1, .3); m[Mt.BATTERY].Battery = true;
            m[Mt.LEATHER] = new MatInfo(4, .8, 1, 1, 1, .5);
            return m;
        }
    }

    // =================================================================
    //  사운드: waveOut 스트리밍 + 실시간 합성
    // =================================================================
    sealed class Biquad
    {
        double b0, b1, b2, a1, a2, z1, z2;
        public Biquad(int type, double f, double q, double sr)
        {
            if (f > sr * .45) f = sr * .45; if (f < 10) f = 10; if (q < .05) q = .05;
            double w0 = 2 * Math.PI * f / sr, cs = Math.Cos(w0), sn = Math.Sin(w0), al = sn / (2 * q);
            if (type == 0) { b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; }
            else if (type == 1) { b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; }
            else { b0 = al; b1 = 0; b2 = -al; }
            double a0 = 1 + al;
            b0 /= a0; b1 /= a0; b2 /= a0; a1 = -2 * cs / a0; a2 = (1 - al) / a0;
        }
        public double Run(double x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
    }

    sealed class Voice
    {
        public bool Noise; public int Wave; public double F0 = 440, F1 = 440, Dur = 1, Gain = .5, Delay, T, Phase, Pan;
        public Biquad Filt; public bool Loop, Stopping, Dead; public double Level, Target, Pitch = 1, PitchTarget = 1;
        uint rng = (uint)U.R.Next(1, int.MaxValue);
        double envA = -1, envM, fCur, fM;
        public double Sample(double dt)
        {
            if (Delay > 0) { Delay -= dt; return 0; }
            if (envA < 0)
            {
                // 감쇠·주파수 변화를 곱셈 한 번으로 처리 (샘플마다 exp/pow 계산 안 함)
                envA = Gain; envM = Math.Exp(Math.Log(.0008 / Gain) * dt / Dur);
                fCur = F0; fM = Math.Pow(F1 / F0, dt / Dur);
            }
            double src;
            if (Noise) { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; src = rng / 2147483647.5 - 1; }
            else
            {
                double f = Loop ? F0 * Pitch : fCur;
                if (!Loop && T < Dur) fCur *= fM;
                Phase += f * dt; Phase -= Math.Floor(Phase);
                if (Wave == 0) src = Math.Sin(Phase * 2 * Math.PI);
                else if (Wave == 1) src = 1 - 4 * Math.Abs(Phase - .5);
                else if (Wave == 2) src = 2 * Phase - 1;
                else src = Phase < .5 ? 1 : -1;
            }
            if (Filt != null) src = Filt.Run(src);
            double amp;
            if (Loop)
            {
                Level += (Target - Level) * Math.Min(1, dt * 25);
                Pitch += (PitchTarget - Pitch) * Math.Min(1, dt * 20);
                amp = Gain * Level;
                if (Stopping && Level < .001) Dead = true;
            }
            else
            {
                amp = envA; envA *= envM;
                if (T < .002) amp *= T / .002;
                if (T >= Dur) Dead = true;
            }
            T += dt;
            return src * amp;
        }
    }

    sealed class LoopHandle
    {
        readonly Audio a; public readonly List<Voice> Vs = new List<Voice>();
        public LoopHandle(Audio a) { this.a = a; }
        public void Level(double v) { lock (a.Lk) foreach (Voice x in Vs) x.Target = v; }
        public void Pitch(double k) { lock (a.Lk) foreach (Voice x in Vs) if (!x.Noise) x.PitchTarget = k; }
        public void Stop() { lock (a.Lk) foreach (Voice x in Vs) { x.Target = 0; x.Stopping = true; } }
    }

    sealed class Audio
    {
        const int SR = 44100, FRAMES = 1024, NBUF = 4;
        public readonly object Lk = new object();
        readonly List<Voice> voices = new List<Voice>();
        IntPtr hwo = IntPtr.Zero; IntPtr[] hdrs, datas; int hdrSize, flagOff;
        Thread th; volatile bool run; public volatile bool Muted; public bool Slow;
        readonly double[] comb = new double[4 * 2000]; readonly int[] combLen = { 1557, 1617, 1491, 1422 }; readonly int[] combPos = new int[4];
        readonly double[] combLp = new double[4];
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Dictionary<string, double> last = new Dictionary<string, double>();

        public bool Start()
        {
            try
            {
                Native.WAVEFORMATEX f = new Native.WAVEFORMATEX();
                f.wFormatTag = 1; f.nChannels = 2; f.nSamplesPerSec = SR; f.wBitsPerSample = 16;
                f.nBlockAlign = 4; f.nAvgBytesPerSec = SR * 4; f.cbSize = 0;
                if (Native.waveOutOpen(out hwo, -1, ref f, IntPtr.Zero, IntPtr.Zero, 0) != 0) { hwo = IntPtr.Zero; return false; }
                hdrSize = Marshal.SizeOf(typeof(Native.WAVEHDR));
                flagOff = Marshal.OffsetOf(typeof(Native.WAVEHDR), "dwFlags").ToInt32();
                hdrs = new IntPtr[NBUF]; datas = new IntPtr[NBUF];
                for (int i = 0; i < NBUF; i++)
                {
                    datas[i] = Marshal.AllocHGlobal(FRAMES * 4);
                    hdrs[i] = Marshal.AllocHGlobal(hdrSize);
                    Native.WAVEHDR h = new Native.WAVEHDR();
                    h.lpData = datas[i]; h.dwBufferLength = FRAMES * 4;
                    Marshal.StructureToPtr(h, hdrs[i], false);
                    Native.waveOutPrepareHeader(hwo, hdrs[i], hdrSize);
                }
                run = true;
                th = new Thread(AudioThread); th.IsBackground = true; th.Priority = ThreadPriority.AboveNormal; th.Start();
                return true;
            }
            catch { hwo = IntPtr.Zero; return false; }
        }

        public void Stop()
        {
            if (hwo == IntPtr.Zero) return;
            run = false;
            if (th != null) th.Join(500);
            Native.waveOutReset(hwo);
            for (int i = 0; i < NBUF; i++) { Native.waveOutUnprepareHeader(hwo, hdrs[i], hdrSize); Marshal.FreeHGlobal(hdrs[i]); Marshal.FreeHGlobal(datas[i]); }
            Native.waveOutClose(hwo); hwo = IntPtr.Zero;
        }

        void AudioThread()
        {
            short[] tmp = new short[FRAMES * 2];
            for (int i = 0; i < NBUF; i++) { Mix(tmp); Marshal.Copy(tmp, 0, datas[i], tmp.Length); Native.waveOutWrite(hwo, hdrs[i], hdrSize); }
            while (run)
            {
                for (int i = 0; i < NBUF; i++)
                {
                    int flags = Marshal.ReadInt32(hdrs[i], flagOff);
                    if ((flags & 1) != 0)
                    {
                        Mix(tmp); Marshal.Copy(tmp, 0, datas[i], tmp.Length); Native.waveOutWrite(hwo, hdrs[i], hdrSize);
                    }
                }
                Thread.Sleep(2);
            }
        }

        void Mix(short[] buf)
        {
            double dt = 1.0 / SR;
            lock (Lk)
            {
                for (int n = 0; n < FRAMES; n++)
                {
                    double L = 0, R = 0;
                    for (int v = 0; v < voices.Count; v++)
                    {
                        Voice x = voices[v];
                        double s = x.Sample(dt);
                        L += s * Math.Min(1, 1 - x.Pan); R += s * Math.Min(1, 1 + x.Pan);
                    }
                    // 작업실 울림 (comb 필터 4개)
                    double mono = (L + R) * .5, wl = 0, wr = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        int baseI = c * 2000, p = combPos[c];
                        double y = comb[baseI + p];
                        combLp[c] = y * .6 + combLp[c] * .4;
                        comb[baseI + p] = mono + combLp[c] * .78;
                        combPos[c] = (p + 1) % combLen[c];
                        if ((c & 1) == 0) wl += y; else wr += y;
                    }
                    L += wl * .09; R += wr * .09;
                    double g = Muted ? 0 : .55;
                    L = Math.Tanh(L * g); R = Math.Tanh(R * g);
                    buf[n * 2] = (short)(L * 32000); buf[n * 2 + 1] = (short)(R * 32000);
                }
                voices.RemoveAll(vv => vv.Dead);
                if (voices.Count > 90) voices.RemoveRange(0, voices.Count - 90);
            }
        }

        bool Ok(string kind, double gap)
        {
            if (hwo == IntPtr.Zero || Muted) return false;
            double n = clock.Elapsed.TotalSeconds, t;
            if (last.TryGetValue(kind, out t) && n - t < gap) return false;
            last[kind] = n; return true;
        }
        void Add(Voice v) { lock (Lk) voices.Add(v); }
        void Noise(double dur, int type, double freq, double q, double gain, double delay, double pan)
        {
            Voice v = new Voice(); v.Noise = true; v.Dur = Slow ? dur * 1.6 : dur; v.Gain = gain; v.Delay = delay; v.Pan = pan;
            v.Filt = new Biquad(type, Slow ? freq * .6 : freq, q, SR); Add(v);
        }
        void Tone(int wave, double f0, double f1, double dur, double gain, double delay, double pan)
        {
            Voice v = new Voice(); v.Wave = wave; double k = Slow ? .6 : 1;
            v.F0 = f0 * k; v.F1 = Math.Max(20, f1 * k); v.Dur = Slow ? dur * 1.6 : dur; v.Gain = gain; v.Delay = delay; v.Pan = pan; Add(v);
        }
        public static double PanOf(double x) { return U.Clamp((x / 1000.0) * 2 - 1, -1, 1) * .7; }

        public void Thud(double p, double pan) { if (!Ok("thud", .03)) return; Noise(.28, 0, 260 + 420 * p, 1, .9 * p, 0, pan); Tone(0, 150, 42, .26, .8 * p, 0, pan); }
        public void Glass(double p, double pan)
        {
            if (!Ok("glass", .06)) return;
            Noise(.4, 1, 3600, .7, .32 * p, 0, pan);
            for (int k = 0; k < 5; k++) Tone(0, U.Rnd(2200, 5200), U.Rnd(1800, 3600), U.Rnd(.2, .5), .07 * p, U.Rnd(.14), pan + U.Rnd(-.2, .2));
        }
        public void Tink(double pan) { if (!Ok("tink", .045)) return; Tone(0, U.Rnd(3200, 6000), U.Rnd(2500, 4000), .12, .05, 0, pan); }
        public void Clank(double pan) { if (!Ok("clank", .07)) return; Tone(1, U.Rnd(700, 1300), 500, .2, .2, 0, pan); Noise(.08, 2, 4200, 3, .22, 0, pan); }
        public void Spark(double pan) { if (!Ok("spark", .07)) return; Noise(.05, 1, 5200, 1, .14, 0, pan); }
        public void Boom(double p, double pan)
        {
            if (!Ok("boom", .08)) return;
            Noise(1.5, 0, 520, .8, 1.2 * p, 0, pan); Tone(0, 95, 26, 1.1, 1 * p, 0, pan * .5); Noise(.25, 1, 1500, .5, .4 * p, 0, pan);
        }
        public void Sizzle(double pan) { if (!Ok("sizzle", .2)) return; Noise(.35, 2, 6400, 2, .12, 0, pan); }
        public void Pop(double pan) { if (!Ok("pop", .05)) return; Tone(3, 300, 80, .1, .12, 0, pan); }

        public LoopHandle Loop(string kind)
        {
            if (hwo == IntPtr.Zero) return null;
            LoopHandle h = new LoopHandle(this);
            if (kind == "drill") { LOsc(h, 2, 170, .35, 1500); LNoise(h, 2, 3000, 1, .15); }
            else if (kind == "saw") { LOsc(h, 2, 92, .3, 2200); LNoise(h, 2, 2400, 1.2, .35); }
            else if (kind == "flame") { LNoise(h, 0, 650, .7, .9); LNoise(h, 2, 180, 1, .6); }
            else if (kind == "laser") { LOsc(h, 3, 620, .12, 1600); LOsc(h, 0, 1245, .1, 0); }
            else if (kind == "hole") { LOsc(h, 0, 48, .8, 0); LNoise(h, 0, 220, .8, .5); }
            else return null;
            lock (Lk) voices.AddRange(h.Vs);
            return h;
        }
        void LOsc(LoopHandle h, int wave, double f, double vol, double lp)
        {
            Voice v = new Voice(); v.Loop = true; v.Wave = wave; v.F0 = f; v.Gain = vol;
            if (lp > 0) v.Filt = new Biquad(0, lp, .7, SR); h.Vs.Add(v);
        }
        void LNoise(LoopHandle h, int type, double f, double q, double vol)
        {
            Voice v = new Voice(); v.Loop = true; v.Noise = true; v.Gain = vol; v.Filt = new Biquad(type, f, q, SR); h.Vs.Add(v);
        }
    }

    // =================================================================
    //  프레임버퍼 + 빛 번짐 버퍼
    // =================================================================
    sealed class Fb
    {
        public int[] Px; public int W, H;
        public int CX0, CY0, CX1, CY1;
        public double[] GR, GG, GB; public int GW, GH;
        double[] tmpR, tmpG, tmpB;

        public void Resize(int w, int h)
        {
            W = w; H = h; Px = new int[w * h];
            GW = w / 4 + 2; GH = h / 4 + 2;
            GR = new double[GW * GH]; GG = new double[GW * GH]; GB = new double[GW * GH];
            tmpR = new double[GW * GH]; tmpG = new double[GW * GH]; tmpB = new double[GW * GH];
            ResetClip();
        }
        public void ResetClip() { CX0 = 0; CY0 = 0; CX1 = W; CY1 = H; }
        public void SetClip(Rectangle r) { CX0 = Math.Max(0, r.X); CY0 = Math.Max(0, r.Y); CX1 = Math.Min(W, r.Right); CY1 = Math.Min(H, r.Bottom); }

        public void Fill(int c) { for (int i = 0; i < Px.Length; i++) Px[i] = c; }

        public void FillRect(int x, int y, int w, int h, int c)
        {
            int x0 = Math.Max(CX0, x), y0 = Math.Max(CY0, y), x1 = Math.Min(CX1, x + w), y1 = Math.Min(CY1, y + h);
            for (int yy = y0; yy < y1; yy++) { int o = yy * W; for (int xx = x0; xx < x1; xx++) Px[o + xx] = c; }
        }
        public void Blend(int x, int y, int c, double a)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1 || a <= 0) return;
            int i = y * W + x, d = Px[i];
            if (a >= 1) { Px[i] = c; return; }
            int r = (int)(((d >> 16) & 255) + (((c >> 16) & 255) - ((d >> 16) & 255)) * a);
            int g = (int)(((d >> 8) & 255) + (((c >> 8) & 255) - ((d >> 8) & 255)) * a);
            int b = (int)((d & 255) + ((c & 255) - (d & 255)) * a);
            Px[i] = (r << 16) | (g << 8) | b;
        }
        public void BlendRect(int x, int y, int w, int h, int c, double a)
        {
            int x0 = Math.Max(CX0, x), y0 = Math.Max(CY0, y), x1 = Math.Min(CX1, x + w), y1 = Math.Min(CY1, y + h);
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Blend(xx, yy, c, a);
        }
        public void Add(int x, int y, double r, double g, double b)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1) return;
            int i = y * W + x, d = Px[i];
            Px[i] = (U.C(((d >> 16) & 255) + r) << 16) | (U.C(((d >> 8) & 255) + g) << 8) | U.C((d & 255) + b);
        }
        // 모서리를 부드럽게 처리한 둥근 사각형
        public void FillRRect(int x, int y, int w, int h, int rad, int c, double alpha)
        {
            int x0 = Math.Max(CX0, x), y0 = Math.Max(CY0, y), x1 = Math.Min(CX1, x + w), y1 = Math.Min(CY1, y + h);
            double r = Math.Min(rad, Math.Min(w, h) / 2.0);
            for (int yy = y0; yy < y1; yy++)
            {
                double py = yy + .5;
                double qy = py < y + r ? y + r - py : py > y + h - r ? py - (y + h - r) : 0;
                for (int xx = x0; xx < x1; xx++)
                {
                    double px = xx + .5;
                    double qx = px < x + r ? x + r - px : px > x + w - r ? px - (x + w - r) : 0;
                    double cov = 1;
                    if (qx > 0 && qy > 0) { double d = Math.Sqrt(qx * qx + qy * qy); cov = U.Clamp(r - d + .5, 0, 1); }
                    if (cov >= 1 && alpha >= 1) Px[yy * W + xx] = c;
                    else if (cov > 0) Blend(xx, yy, c, cov * alpha);
                }
            }
        }
        public void Box(Rectangle rc, int rad, int fill, int border)
        {
            FillRRect(rc.X, rc.Y, rc.Width, rc.Height, rad, border, 1);
            FillRRect(rc.X + 1, rc.Y + 1, rc.Width - 2, rc.Height - 2, Math.Max(0, rad - 1), fill, 1);
        }
        public void SoftDisc(double cx, double cy, double rad, int c, double a, bool additive)
        {
            if (rad < .5 || a <= 0) return;
            int x0 = Math.Max(CX0, (int)(cx - rad)), x1 = Math.Min(CX1 - 1, (int)(cx + rad) + 1);
            int y0 = Math.Max(CY0, (int)(cy - rad)), y1 = Math.Min(CY1 - 1, (int)(cy + rad) + 1);
            double r2 = rad * rad;
            double cr = (c >> 16) & 255, cg = (c >> 8) & 255, cb = c & 255;
            for (int y = y0; y <= y1; y++)
            {
                double dy = y + .5 - cy;
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x + .5 - cx, d2 = dx * dx + dy * dy;
                    if (d2 >= r2) continue;
                    double f = 1 - d2 / r2; f *= f;
                    if (additive) Add(x, y, cr * f * a, cg * f * a, cb * f * a);
                    else Blend(x, y, c, f * a);
                }
            }
        }
        public void Disc(double cx, double cy, double rad, int c, double a)
        {
            int x0 = Math.Max(CX0, (int)(cx - rad)), x1 = Math.Min(CX1 - 1, (int)(cx + rad) + 1);
            int y0 = Math.Max(CY0, (int)(cy - rad)), y1 = Math.Min(CY1 - 1, (int)(cy + rad) + 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    double dx = x + .5 - cx, dy = y + .5 - cy, d = Math.Sqrt(dx * dx + dy * dy);
                    double cov = U.Clamp(rad - d + .5, 0, 1);
                    if (cov > 0) Blend(x, y, c, cov * a);
                }
        }
        public void Line(double x0, double y0, double x1, double y1, int c, double a, bool additive, int thick)
        {
            double dx = x1 - x0, dy = y1 - y0; int n = (int)Math.Max(Math.Abs(dx), Math.Abs(dy)) + 1;
            double cr = (c >> 16) & 255, cg = (c >> 8) & 255, cb = c & 255;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n; int x = (int)(x0 + dx * t), y = (int)(y0 + dy * t);
                for (int oy = 0; oy < thick; oy++) for (int ox = 0; ox < thick; ox++)
                    {
                        int px = x + ox - thick / 2, py = y + oy - thick / 2;
                        if (additive) Add(px, py, cr * a, cg * a, cb * a); else Blend(px, py, c, a);
                    }
            }
        }
        public void Poly(double[] xs, double[] ys, int c, double a)
        {
            int n = xs.Length; double mnx = 1e9, mny = 1e9, mxx = -1e9, mxy = -1e9;
            for (int i = 0; i < n; i++) { mnx = Math.Min(mnx, xs[i]); mxx = Math.Max(mxx, xs[i]); mny = Math.Min(mny, ys[i]); mxy = Math.Max(mxy, ys[i]); }
            int x0 = Math.Max(CX0, (int)mnx), x1 = Math.Min(CX1 - 1, (int)mxx + 1), y0 = Math.Max(CY0, (int)mny), y1 = Math.Min(CY1 - 1, (int)mxy + 1);
            for (int y = y0; y <= y1; y++)
            {
                double py = y + .5;
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + .5; bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                        if ((ys[i] > py) != (ys[j] > py) && px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
                    if (inside) Blend(x, y, c, a);
                }
            }
        }
        public void Ring(double cx, double cy, double rad, int c, double a, double a0, double a1, int thick)
        {
            int steps = (int)Math.Max(12, rad * Math.Abs(a1 - a0) * 1.2);
            for (int i = 0; i <= steps; i++)
            {
                double t = a0 + (a1 - a0) * i / steps;
                int x = (int)(cx + Math.Cos(t) * rad), y = (int)(cy + Math.Sin(t) * rad);
                for (int o = 0; o < thick; o++) { Add(x + o, y, ((c >> 16) & 255) * a, ((c >> 8) & 255) * a, (c & 255) * a); }
            }
        }

        // ---- 빛 번짐(블룸) ----
        public void ClearGlow() { Array.Clear(GR, 0, GR.Length); Array.Clear(GG, 0, GG.Length); Array.Clear(GB, 0, GB.Length); }
        public void Glow(double x, double y, double r, double g, double b)
        {
            int gx = (int)(x / 4), gy = (int)(y / 4);
            if (gx < 0 || gy < 0 || gx >= GW || gy >= GH) return;
            int i = gy * GW + gx; GR[i] += r; GG[i] += g; GB[i] += b;
        }
        public void GlowDisc(double cx, double cy, double rad, double r, double g, double b)
        {
            double gcx = cx / 4, gcy = cy / 4, gr = Math.Max(.6, rad / 4), r2 = gr * gr;
            int x0 = Math.Max(0, (int)(gcx - gr)), x1 = Math.Min(GW - 1, (int)(gcx + gr) + 1);
            int y0 = Math.Max(0, (int)(gcy - gr)), y1 = Math.Min(GH - 1, (int)(gcy + gr) + 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    double dx = x + .5 - gcx, dy = y + .5 - gcy, d2 = dx * dx + dy * dy;
                    if (d2 >= r2) continue;
                    double f = 1 - d2 / r2; int i = y * GW + x;
                    GR[i] += r * f; GG[i] += g * f; GB[i] += b * f;
                }
        }
        void BoxBlur(double[] src, double[] dst, int rad, bool horiz)
        {
            int w = GW, h = GH; double inv = 1.0 / (rad * 2 + 1);
            if (horiz)
            {
                for (int y = 0; y < h; y++)
                {
                    int o = y * w; double s = 0;
                    for (int k = -rad; k <= rad; k++) s += src[o + Math.Min(w - 1, Math.Max(0, k))];
                    for (int x = 0; x < w; x++)
                    {
                        dst[o + x] = s * inv;
                        s += src[o + Math.Min(w - 1, x + rad + 1)] - src[o + Math.Max(0, x - rad)];
                    }
                }
            }
            else
            {
                for (int x = 0; x < w; x++)
                {
                    double s = 0;
                    for (int k = -rad; k <= rad; k++) s += src[Math.Min(h - 1, Math.Max(0, k)) * w + x];
                    for (int y = 0; y < h; y++)
                    {
                        dst[y * w + x] = s * inv;
                        s += src[Math.Min(h - 1, y + rad + 1) * w + x] - src[Math.Max(0, y - rad) * w + x];
                    }
                }
            }
        }
        public void BlurGlow()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                BoxBlur(GR, tmpR, 3, true); BoxBlur(tmpR, GR, 3, false);
                BoxBlur(GG, tmpG, 3, true); BoxBlur(tmpG, GG, 3, false);
                BoxBlur(GB, tmpB, 3, true); BoxBlur(tmpB, GB, 3, false);
            }
        }
        // 4x4 블록마다 네 모서리 값을 보간해서 더함 (빛이 없는 블록은 건너뜀)
        public void CompositeGlow(double k)
        {
            for (int gy = 0; gy < GH - 1; gy++)
            {
                for (int gx = 0; gx < GW - 1; gx++)
                {
                    int i00 = gy * GW + gx, i10 = i00 + 1, i01 = i00 + GW, i11 = i01 + 1;
                    double m = GR[i00] + GG[i00] + GB[i00] + GR[i10] + GG[i10] + GB[i10] + GR[i01] + GG[i01] + GB[i01] + GR[i11] + GG[i11] + GB[i11];
                    if (m < 1.5) continue;
                    int bx = gx * 4 + 2, by = gy * 4 + 2;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        int y = by + sy; if (y < CY0 || y >= CY1) continue;
                        double fy = sy / 4.0;
                        for (int sx = 0; sx < 4; sx++)
                        {
                            int x = bx + sx; if (x < CX0 || x >= CX1) continue;
                            double fx = sx / 4.0;
                            double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                            double r = GR[i00] * w00 + GR[i10] * w10 + GR[i01] * w01 + GR[i11] * w11;
                            double g = GG[i00] * w00 + GG[i10] * w10 + GG[i01] * w01 + GG[i11] * w11;
                            double b = GB[i00] * w00 + GB[i10] * w10 + GB[i01] * w01 + GB[i11] * w11;
                            int p = y * W + x, d = Px[p];
                            Px[p] = (U.C(((d >> 16) & 255) + r * k) << 16) | (U.C(((d >> 8) & 255) + g * k) << 8) | U.C((d & 255) + b * k);
                        }
                    }
                }
            }
        }
    }

    // =================================================================
    //  몸체: 픽셀 격자 + 강체
    // =================================================================
    struct Pt { public double X, Y; public Pt(double x, double y) { X = x; Y = y; } }

    sealed class Body
    {
        public const double CELL = 3;
        public int W, H; public byte[] Mat, IMat; public int[] Col, ICol, Img; public double[] Hp, Burn, Heat;
        public HashSet<int> Hot = new HashSet<int>(); public byte[] Anchor;
        public double X, Y, A, VX, VY, VA, CX, CY, Mass = 1, Inertia = 1, Radius;
        public double[] HullX = new double[0], HullY = new double[0];
        public int Count, Total, MaxY;
        public bool Pinned, Dead, Asleep, DirtyImg = true, DirtySplit;
        public double SleepT, SplitT, HitCool; public Cam Cam;

        public Body(int w, int h)
        {
            W = w; H = h; int n = w * h;
            Mat = new byte[n]; IMat = new byte[n]; Col = new int[n]; ICol = new int[n]; Img = new int[n];
            Hp = new double[n]; Burn = new double[n]; Heat = new double[n];
        }

        public void ComputeProps()
        {
            int n = 0; double sx = 0, sy = 0, sxx = 0, syy = 0;
            List<Pt> pts = new List<Pt>();
            for (int y = 0; y < H; y++)
            {
                int o = y * W, mn = -1, mx = -1;
                for (int x = 0; x < W; x++)
                {
                    if (Mat[o + x] == 0) continue;
                    double px = x + .5, py = y + .5;
                    n++; sx += px; sy += py; sxx += px * px; syy += py * py;
                    if (mn < 0) mn = x; mx = x;
                }
                if (mn >= 0) { pts.Add(new Pt(mn, y)); pts.Add(new Pt(mn, y + 1)); pts.Add(new Pt(mx + 1, y)); pts.Add(new Pt(mx + 1, y + 1)); }
            }
            Count = n;
            if (n == 0) { Dead = true; return; }
            double cx = sx / n, cy = sy / n, I = sxx + syy - n * (cx * cx + cy * cy);
            CX = cx; CY = cy; Mass = n; Inertia = (I + n / 6.0) * CELL * CELL;
            List<Pt> hull = Hull(pts);
            HullX = new double[hull.Count]; HullY = new double[hull.Count];
            double r = 0;
            for (int i = 0; i < hull.Count; i++)
            {
                HullX[i] = (hull[i].X - cx) * CELL; HullY[i] = (hull[i].Y - cy) * CELL;
                r = Math.Max(r, Math.Sqrt(HullX[i] * HullX[i] + HullY[i] * HullY[i]));
            }
            Radius = r;
        }

        static List<Pt> Hull(List<Pt> pts)
        {
            pts.Sort(delegate (Pt a, Pt b) { int c = a.X.CompareTo(b.X); return c != 0 ? c : a.Y.CompareTo(b.Y); });
            if (pts.Count < 3) return new List<Pt>(pts);
            List<Pt> lo = new List<Pt>(), up = new List<Pt>();
            foreach (Pt p in pts) { while (lo.Count >= 2 && Cross(lo[lo.Count - 2], lo[lo.Count - 1], p) <= 0) lo.RemoveAt(lo.Count - 1); lo.Add(p); }
            for (int i = pts.Count - 1; i >= 0; i--) { Pt p = pts[i]; while (up.Count >= 2 && Cross(up[up.Count - 2], up[up.Count - 1], p) <= 0) up.RemoveAt(up.Count - 1); up.Add(p); }
            lo.RemoveAt(lo.Count - 1); up.RemoveAt(up.Count - 1); lo.AddRange(up); return lo;
        }
        static double Cross(Pt o, Pt a, Pt b) { return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X); }

        public void Recalc()
        {
            double ocx = CX, ocy = CY;
            ComputeProps();
            if (Dead) return;
            double dx = (CX - ocx) * CELL, dy = (CY - ocy) * CELL, c = Math.Cos(A), s = Math.Sin(A);
            X += dx * c - dy * s; Y += dx * s + dy * c;
        }
        public void ToWorld(double lx, double ly, out double wx, out double wy)
        {
            double dx = (lx - CX) * CELL, dy = (ly - CY) * CELL, c = Math.Cos(A), s = Math.Sin(A);
            wx = X + dx * c - dy * s; wy = Y + dx * s + dy * c;
        }
        public void ToLocal(double wx, double wy, out double lx, out double ly)
        {
            double dx = wx - X, dy = wy - Y, c = Math.Cos(A), s = Math.Sin(A);
            lx = (dx * c + dy * s) / CELL + CX; ly = (-dx * s + dy * c) / CELL + CY;
        }
        public int CellAt(double wx, double wy)
        {
            double lx, ly; ToLocal(wx, wy, out lx, out ly);
            int x = (int)Math.Floor(lx), y = (int)Math.Floor(ly);
            if (x < 0 || y < 0 || x >= W || y >= H) return -1;
            int i = y * W + x; return Mat[i] != 0 ? i : -1;
        }
        public int CellColor(int i)
        {
            int c = Col[i]; double r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255;
            double dmg = 1 - Math.Max(0, Hp[i]) / MatInfo.All[Mat[i]].Hp;
            if (dmg > 0) { double k = 1 - dmg * .45; r *= k; g *= k; bl *= k; }
            double bu = Burn[i];
            if (bu > 0) { double k = Math.Min(1, bu * 1.1); r += (22 - r) * k; g += (18 - g) * k; bl += (16 - bl) * k; }
            double h = Heat[i];
            if (h > .15) { double k = Math.Min(1, (h - .15) * 1.3); r += (255 - r) * k * .85; g += (125 - g) * k * .6; bl += (20 - bl) * k * .3; }
            return U.Rgb(r, g, bl);
        }
        public void Redraw()
        {
            for (int i = 0; i < Img.Length; i++) Img[i] = Mat[i] != 0 ? (CellColor(i) | unchecked((int)0xFF000000)) : 0;
            DirtyImg = false;
        }
        public void Wake() { Asleep = false; SleepT = 0; }
    }

    // =================================================================
    //  카메라 그리기
    // =================================================================
    static class Paint
    {
        public static Func<int, int, int> Tone(double r, double g, double b, double n = 6, double y0 = double.NaN, double y1 = 0, double k = 0)
        {
            return delegate (int x, int y)
            {
                double f = 1;
                if (!double.IsNaN(y0)) { double t = U.Clamp((y - y0) / (y1 - y0), 0, 1); f = 1 + k * (.5 - t); }
                double nn = (U.Hash(x, y) - .5) * n;
                return U.Rgb(r * f + nn, g * f + nn, b * f + nn);
            };
        }
        public static Func<int, int, int> Metal(double r, double g, double b, double y0 = double.NaN, double y1 = 0)
        {
            return delegate (int x, int y)
            {
                double row = (U.Hash(7, y) - .5) * 14, nn = (U.Hash(x, y) - .5) * 6, f = 1;
                if (!double.IsNaN(y0)) { double t = U.Clamp((y - y0) / (y1 - y0), 0, 1); f = 1.12 - t * .3; }
                return U.Rgb(r * f + row + nn, g * f + row + nn, b * f + row + nn);
            };
        }
        public static Func<int, int, int> Ridged(double r, double g, double b)
        {
            return delegate (int x, int y) { double k = (x % 2 != 0) ? .7 : 1.06, nn = (U.Hash(x, y) - .5) * 8; return U.Rgb(r * k + nn, g * k + nn, b * k + nn); };
        }
        public static Func<int, int, int> Leather(double r, double g, double b)
        {
            return delegate (int x, int y)
            {
                double h = U.Hash(x, y), nn = (h < .18 ? -9 : h > .86 ? 9 : 0) + (U.Hash(x + 3, y * 7) - .5) * 5;
                return U.Rgb(r + nn, g + nn, b + nn);
            };
        }
        public static Func<int, int, int> Rubber(double r, double g, double b)
        {
            return delegate (int x, int y) { double rib = (y % 3 == 0) ? -7 : 3, nn = (U.Hash(x, y) - .5) * 7; return U.Rgb(r + rib + nn, g + rib + nn, b + rib + nn); };
        }
        public static Func<int, int, int> Ribs(double cx, double cy, double r, double g, double b, int n)
        {
            return delegate (int x, int y)
            {
                double a = Math.Atan2(y + .5 - cy, x + .5 - cx);
                int s = (int)Math.Floor((a + Math.PI) / (Math.PI * 2) * n) % 2;
                double k = s != 0 ? .68 : 1.08, nn = (U.Hash(x, y) - .5) * 5;
                return U.Rgb(r * k + nn, g * k + nn, b * k + nn);
            };
        }
        public static Func<int, int, int> Bezel(double cx, double cy, double R, double r, double g, double b)
        {
            return delegate (int x, int y)
            {
                double dx = (x + .5 - cx) / R, dy = (y + .5 - cy) / R;
                double f = .85 + .3 * Math.Sqrt(dx * dx + dy * dy) - .12 * dy, nn = (U.Hash(x, y) - .5) * 4;
                return U.Rgb(r * f + nn, g * f + nn, b * f + nn);
            };
        }
        public static Func<int, int, int> Glass(double cx, double cy, double R)
        {
            return delegate (int x, int y)
            {
                double dx = (x + .5 - cx) / R, dy = (y + .5 - cy) / R, d = Math.Sqrt(dx * dx + dy * dy);
                double r = 10, g = 14, b = 28;
                double ring = Math.Abs(Math.Sin(d * 9.5)); r += ring * 6; g += ring * 8; b += ring * 15;
                double ang = Math.Atan2(dy, dx), t = (Math.Sin(ang * 2 + 1) + 1) / 2;
                r += t * 26 * d; g += (1 - t) * 34 * d; b += 22 * d;
                double hx = dx + .32, hy = dy + .36, hd = Math.Sqrt(hx * hx + hy * hy);
                if (hd < .42 && d > .22) { double k = (.42 - hd) / .42; r += 95 * k; g += 110 * k; b += 130 * k; }
                double sx = dx + .28, sy = dy + .42;
                if (Math.Sqrt(sx * sx + sy * sy) < .075) { r = 236; g = 242; b = 255; }
                double ox = dx - .38, oy = dy - .3;
                if (Math.Sqrt(ox * ox + oy * oy) < .06) { r += 120; g += 90; b += 55; }
                return U.Rgb(r, g, b);
            };
        }
        public static Func<int, int, int> Pcb()
        {
            return delegate (int x, int y)
            {
                double h = U.Hash(x >> 2, y >> 2);
                if (h > .9) return U.Rgb(26, 26, 28);
                if (h > .85 && x % 2 == 0) return U.Rgb(178, 178, 168);
                if ((x % 6 == 0 || y % 8 == 0) && U.Hash(x, y * 3) > .35) return U.Rgb(196, 160, 62);
                double nn = (U.Hash(x, y) - .5) * 12;
                return U.Rgb(22 + nn, 98 + nn, 56 + nn);
            };
        }
        public static Func<int, int, int> Battery()
        {
            return delegate (int x, int y) { if (y % 14 == 6) return U.Rgb(222, 222, 214); double nn = (U.Hash(x, y) - .5) * 8; return U.Rgb(38 + nn, 52 + nn, 84 + nn); };
        }
        public static Func<int, int, int> Sensor()
        {
            return delegate (int x, int y)
            {
                double t = Math.Sin(x * .35 + y * .21) * .5 + .5, u = Math.Sin(x * .13 - y * .3) * .5 + .5;
                return U.Rgb(70 + t * 90, 40 + u * 110, 120 + t * 80);
            };
        }
        public static Func<int, int, int> Flat(int rgb) { return delegate (int x, int y) { return rgb; }; }
    }

    sealed class Raster
    {
        readonly Body b;
        public Raster(Body b) { this.b = b; }
        public void Fill(double x0, double y0, double x1, double y1, Func<double, double, bool> test, byte mat, Func<int, int, int> color, bool inner)
        {
            int ix0 = Math.Max(0, (int)Math.Floor(x0)), iy0 = Math.Max(0, (int)Math.Floor(y0));
            int ix1 = Math.Min(b.W - 1, (int)Math.Ceiling(x1)), iy1 = Math.Min(b.H - 1, (int)Math.Ceiling(y1));
            for (int y = iy0; y <= iy1; y++) for (int x = ix0; x <= ix1; x++)
                {
                    if (!test(x + .5, y + .5)) continue;
                    int i = y * b.W + x;
                    if (inner) { b.IMat[i] = mat; b.ICol[i] = color(x, y); }
                    else { b.Mat[i] = mat; b.Col[i] = color(x, y); }
                }
        }
        public static bool InRR(double px, double py, double x, double y, double w, double h, double r)
        {
            if (px < x || px > x + w || py < y || py > y + h) return false;
            r = Math.Min(r, Math.Min(w / 2, h / 2));
            double qx = px < x + r ? x + r - px : px > x + w - r ? px - (x + w - r) : 0;
            double qy = py < y + r ? y + r - py : py > y + h - r ? py - (y + h - r) : 0;
            return qx * qx + qy * qy <= r * r;
        }
        public static bool InPoly(double px, double py, double[] xs, double[] ys)
        {
            bool c = false;
            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                if ((ys[i] > py) != (ys[j] > py) && px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]) c = !c;
            return c;
        }
        public void RRect(double x, double y, double w, double h, double r, byte mat, Func<int, int, int> color, bool inner = false)
        {
            Fill(x, y, x + w, y + h, delegate (double px, double py) { return InRR(px, py, x, y, w, h, r); }, mat, color, inner);
        }
        public void Circle(double cx, double cy, double r, byte mat, Func<int, int, int> color, bool inner = false)
        {
            Fill(cx - r, cy - r, cx + r, cy + r, delegate (double px, double py) { return (px - cx) * (px - cx) + (py - cy) * (py - cy) <= r * r; }, mat, color, inner);
        }
        public void Ring(double cx, double cy, double r0, double r1, byte mat, Func<int, int, int> color)
        {
            Fill(cx - r1, cy - r1, cx + r1, cy + r1, delegate (double px, double py) { double d = (px - cx) * (px - cx) + (py - cy) * (py - cy); return d <= r1 * r1 && d > r0 * r0; }, mat, color, false);
        }
        public void Poly(double[] xs, double[] ys, byte mat, Func<int, int, int> color)
        {
            double mnx = 1e9, mny = 1e9, mxx = -1e9, mxy = -1e9;
            for (int i = 0; i < xs.Length; i++) { mnx = Math.Min(mnx, xs[i]); mxx = Math.Max(mxx, xs[i]); mny = Math.Min(mny, ys[i]); mxy = Math.Max(mxy, ys[i]); }
            Fill(mnx, mny, mxx, mxy, delegate (double px, double py) { return InPoly(px, py, xs, ys); }, mat, color, false);
        }
    }

    static class Builder
    {
        public static Body Build(BodyDef bd, LensDef ld)
        {
            const double pad = 8, topRoom = 9;
            double humpH = bd.Hump != null ? bd.HumpH : 0;
            double L = pad, T = topRoom + humpH, bw = bd.W, bh = bd.H, vg = bd.VGrip;
            double R = L + bw, Bt = T + bh;
            double mx = L + bw * bd.MX, my = T + bh * bd.MY, lr = ld.R;
            double footH = ld.Foot ? 17 : 0;
            double lowest = Math.Max(Bt + vg, my + lr + footH);
            Body b = new Body((int)Math.Ceiling(R + pad), (int)Math.Ceiling(lowest + 3));
            Raster rs = new Raster(b);
            Func<int, int, int> plastic = Paint.Tone(30, 30, 32, 5, T, Bt + vg, .28);
            double hx = mx, hw = bd.Hump != null ? bd.HumpW : 0;

            // 몸체
            if (bd.Leather)
            {
                rs.RRect(L, T, bw, bh, 9, Mt.LEATHER, Paint.Leather(34, 33, 32));
                double band = T + bh * .3;
                Func<int, int, int> topCol = bd.SilverTop ? Paint.Metal(196, 196, 192, T, T + bh * .3) : Paint.Tone(36, 36, 38, 4);
                rs.Fill(L, T, R, band, delegate (double px, double py) { return Raster.InRR(px, py, L, T, bw, bh, 9) && py < band; }, bd.SilverTop ? Mt.METAL : Mt.PLASTIC, topCol, false);
                rs.Fill(L, band - .6, R, band + .6, delegate (double px, double py) { return Raster.InRR(px, py, L, T, bw, bh, 9); }, Mt.METAL, Paint.Flat(U.Rgb(120, 120, 118)), false);
            }
            else rs.RRect(L, T, bw, bh, 9, Mt.PLASTIC, plastic);
            if (vg > 0) rs.RRect(L, Bt - 10, bw, vg + 10, 9, Mt.PLASTIC, plastic);

            // 상단 돌출부
            if (bd.Hump != null)
            {
                double hh = bd.HumpH;
                byte hm = bd.SilverTop ? Mt.METAL : Mt.PLASTIC;
                Func<int, int, int> hc = bd.SilverTop ? Paint.Metal(196, 196, 192, T - hh, T + 4) : Paint.Tone(34, 34, 36, 5, T - hh, T, .3);
                if (bd.Hump == "prism") rs.Poly(new double[] { hx - hw / 2, hx - hw * .3, hx + hw * .3, hx + hw / 2 }, new double[] { T + 2, T - hh, T - hh, T + 2 }, hm, hc);
                else if (bd.Hump == "angular") rs.Poly(new double[] { hx - hw / 2, hx - hw * .37, hx + hw * .37, hx + hw / 2 }, new double[] { T + 2, T - hh, T - hh, T + 2 }, hm, hc);
                else rs.RRect(hx - hw / 2, T - hh, hw, hh + 4, 7, hm, hc);
                rs.RRect(hx - 9, T - hh - 2.5, 18, 3, 1, Mt.METAL, Paint.Metal(150, 150, 152));
            }

            // 그립, 배터리
            double gw = bd.Grip;
            if (gw >= 20) rs.RRect(L - 3, T + 5, gw, bh - 10 + vg, 8, Mt.RUBBER, Paint.Rubber(24, 24, 25));
            else rs.RRect(L - 1, T + bh * .34, gw, bh * .58, 5, Mt.RUBBER, Paint.Rubber(26, 26, 27));
            rs.RRect(L + 1, T + 12, Math.Max(12, gw - 6), bh - 26, 3, Mt.BATTERY, Paint.Battery(), true);
            if (vg > 0) rs.RRect(L + gw, Bt + 4, bw - gw - 12, vg - 10, 3, Mt.BATTERY, Paint.Battery(), true);

            // 상단 버튼·다이얼
            rs.RRect(L + gw * .25, T - 3.2, Math.Max(8, gw * .5), 3.6, 1.6, Mt.METAL, Paint.Metal(170, 170, 172));
            if (bd.Dials)
            {
                Func<int, int, int> dial = bd.SilverTop ? Paint.Ridged(205, 205, 200) : Paint.Ridged(176, 176, 174);
                double leftX = bd.Hump != null ? hx - hw / 2 - 24 : L + bw * .32;
                double rightX = bd.Hump != null ? hx + hw / 2 + 6 : L + bw * .58;
                rs.RRect(leftX, T - 6.5, 18, 7, 1.5, Mt.METAL, dial);
                rs.RRect(rightX, T - 6.5, 18, 7, 1.5, Mt.METAL, dial);
                rs.RRect(R - 22, T - 5, 15, 5.5, 1.5, Mt.METAL, dial);
            }
            else
            {
                double dx = bd.Hump != null ? hx + hw / 2 + 6 : R - 26;
                rs.RRect(dx, T - 4.5, 16, 5, 1.5, Mt.PLASTIC, Paint.Ridged(52, 52, 54));
            }
            if (bd.Ovf)
            {
                rs.RRect(R - 34, T + 4, 26, 17, 3, Mt.METAL, Paint.Metal(150, 150, 150));
                rs.RRect(R - 32, T + 6, 22, 13, 2, Mt.GLASS, Paint.Glass(R - 21, T + 12.5, 12));
            }

            // 마운트, AF 보조광, 분리 버튼
            rs.Ring(mx, my, bd.MountR - 3, bd.MountR, Mt.METAL, Paint.Metal(196, 196, 200));
            rs.Circle(mx, my, bd.MountR - 3, Mt.PLASTIC, Paint.Tone(14, 14, 15, 3));
            rs.Circle(L + gw + 6, T + 8, 2.4, Mt.GLASS, Paint.Flat(U.Rgb(120, 74, 40)));
            rs.Circle(mx + bd.MountR + 5, my + bd.MountR * .45, 3, Mt.METAL, Paint.Metal(165, 165, 168));

            // 모델명
            int paint = bd.SilverTop ? U.Rgb(42, 42, 44) : U.Rgb(210, 208, 202);
            double roomRight = (R - 6) - (mx + lr + 4);
            if (roomRight >= 26) Decal(b, bd.Name, R - 6, T + 3, roomRight, 8, paint, false);
            else if (bd.Hump != null) Decal(b, bd.Name, hx, T - bd.HumpH + 5, hw * .62, 8, paint, true);
            else Decal(b, bd.Name, R - 6, T + 3, Math.Max(roomRight, 22), 7, paint, false);

            // 렌즈
            if (ld.Foot)
            {
                rs.RRect(mx - 6, my + lr - 3, 12, footH, 2, Mt.METAL, Paint.Metal(64, 64, 68));
                rs.RRect(mx - 15, my + lr + footH - 6, 30, 5, 2, Mt.METAL, Paint.Metal(80, 80, 84));
            }
            double br = 24, bgc = 24, bb = 26;
            if (ld.Color == "white") { br = 230; bgc = 229; bb = 224; } else if (ld.Color == "silver") { br = 192; bgc = 192; bb = 194; }
            if (ld.Color == "silver") rs.Circle(mx, my, lr, Mt.METAL, Paint.Metal(br, bgc, bb));
            else rs.Circle(mx, my, lr, Mt.PLASTIC, Paint.Bezel(mx, my, lr, br, bgc, bb));
            rs.Circle(mx, my, lr, Mt.METAL, Paint.Tone(66, 68, 72, 10), true);
            double rc = ld.Color == "white" ? 40 : 30;
            rs.Ring(mx, my, lr * .8, lr * .965, Mt.RUBBER, Paint.Ribs(mx, my, rc, rc, rc + 2, ld.Zoom ? 72 : 120));
            if (ld.Accent != null) rs.Ring(mx, my, lr * .765, lr * .8, Mt.PLASTIC, Paint.Tone(ld.Accent[0], ld.Accent[1], ld.Accent[2], 4));
            rs.Ring(mx, my, lr * ld.G + 1.5, lr * .765, Mt.PLASTIC, Paint.Bezel(mx, my, lr, 20, 20, 22));
            rs.Ring(mx, my, lr * ld.G, lr * ld.G + 1.5, Mt.METAL, Paint.Metal(52, 52, 56));
            rs.Circle(mx, my, lr * ld.G, Mt.GLASS, Paint.Glass(mx, my, lr * ld.G));
            rs.Circle(mx, my, lr * ld.G, Mt.SENSOR, Paint.Sensor(), true);

            // 내부 기판, 체력, 바닥 고정점
            Func<int, int, int> pcb = Paint.Pcb();
            int total = 0, maxY = 0;
            for (int i = 0; i < b.W * b.H; i++)
            {
                if (b.Mat[i] == 0) { b.IMat[i] = 0; continue; }
                int y = i / b.W;
                if (b.IMat[i] == 0 && b.Mat[i] != Mt.GLASS && y >= T - humpH + 2) { b.IMat[i] = Mt.PCB; b.ICol[i] = pcb(i % b.W, y); }
                b.Hp[i] = MatInfo.All[b.Mat[i]].Hp;
                total += b.IMat[i] != 0 ? 2 : 1;
                if (y > maxY) maxY = y;
            }
            b.Anchor = new byte[b.W * b.H];
            for (int i = 0; i < b.W * b.H; i++) if (b.Mat[i] != 0 && i / b.W >= maxY - 1) b.Anchor[i] = 1;
            b.ComputeProps();
            b.Total = total; b.MaxY = maxY;
            return b;
        }

        static void Decal(Body b, string text, double x, double y, double maxW, float size, int paint, bool centered)
        {
            if (maxW < 14) return;
            using (Bitmap bmp = new Bitmap(b.W, b.H, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Font f = new Font("Arial", size, FontStyle.Bold, GraphicsUnit.Pixel);
                SizeF sz = g.MeasureString(text, f, PointF.Empty, StringFormat.GenericTypographic);
                if (sz.Width > maxW)
                {
                    float ns = (float)Math.Floor(size * maxW / sz.Width);
                    f.Dispose();
                    if (ns < 5) return;
                    f = new Font("Arial", ns, FontStyle.Bold, GraphicsUnit.Pixel);
                    sz = g.MeasureString(text, f, PointF.Empty, StringFormat.GenericTypographic);
                }
                float tx = (float)(centered ? x - sz.Width / 2 : x - sz.Width);
                g.DrawString(text, f, Brushes.White, tx, (float)y, StringFormat.GenericTypographic);
                f.Dispose();
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, b.W, b.H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int[] px = new int[b.W * b.H];
                Marshal.Copy(bd.Scan0, px, 0, px.Length);
                bmp.UnlockBits(bd);
                for (int i = 0; i < px.Length; i++)
                {
                    int a = (px[i] >> 24) & 255; byte m = b.Mat[i];
                    if (a > 110 && (m == Mt.PLASTIC || m == Mt.METAL || m == Mt.LEATHER)) b.Col[i] = paint;
                }
            }
        }
    }

    // =================================================================
    //  게임 상태
    // =================================================================
    sealed class Cam
    {
        public BrandDef Brand; public BodyDef BodyD; public LensDef Lens; public double Value; public int Total, Destroyed;
        public bool Sensor, Wrecked, Counted; public double Drop = 1; public Body B;
        public bool BattOn, BattDone; public Body BattB; public double BattLX, BattLY, BattT, BattWX, BattWY;
    }
    sealed class Bomb { public double X, Y, VY, T; public Body AB; public double ALX, ALY; }
    sealed class Hole { public double X, Y, T, Max; }
    struct Part { public int K; public double X, Y, VX, VY, Life, Max, S, A, VA; public int C; public bool Rest; }

    sealed class Game
    {
        public const double CELL = Body.CELL, WW = 1000, WH = 640, FLOOR = 612, G = 1900;
        public const double TX0 = 210, TX1 = 790, TTOP = 520;
        const int DEB = 0, SHARD = 1, SPARK = 2, FIRE = 3, SMOKE = 4, ASH = 5, RING = 6, FLASH = 7, EMBER = 8;
        const int MAXP = 7000;

        public readonly List<Body> Bodies = new List<Body>();
        public readonly Part[] P = new Part[MAXP]; public int PN;
        public readonly List<Bomb> Bombs = new List<Bomb>(); public readonly List<Hole> Holes = new List<Hole>();
        public Cam Cam; public double TotalPrev; public int Sacrificed; public double Shake;
        bool lhOk; double lhX, lhY, lhK;
        Body grabB; double grabLX, grabLY;
        public int SelBrand, SelBody, SelLens; public int Tool;
        LoopHandle toolLoop; double swing, spin; public double Time;
        public double PX = 500, PY = 300, PPX = 500, PPY = 300; public bool PDown, POver;
        public readonly Audio Snd;
        public string ToastMsg = ""; public double ToastT;
        // 보기 변환 (버퍼 픽셀)
        public double S = 1, OX, OY; public Rectangle Stage;

        public Game(Audio a) { Snd = a; }
        string ToolId { get { return Catalog.ToolIds[Tool]; } }
        public bool Grabbing { get { return grabB != null; } }

        public void Toast(string m) { ToastMsg = m; ToastT = 2; }
        static double Pan(double x) { return Audio.PanOf(x); }

        // ---------------- 파티클 ----------------
        int Spawn(int k, double x, double y, double vx, double vy, double life)
        {
            int idx;
            if (PN >= MAXP)
            {
                idx = -1;
                for (int t = 0; t < 8; t++) { int q = U.R.Next(PN); if (P[q].Rest || P[q].K == SMOKE) { idx = q; break; } }
                if (idx < 0) return -1;
            }
            else idx = PN++;
            Part p = new Part(); p.K = k; p.X = x; p.Y = y; p.VX = vx; p.VY = vy; p.Life = life; p.Max = life;
            P[idx] = p; return idx;
        }
        void Debris(double x, double y, double vx, double vy, int c, bool big)
        { int i = Spawn(DEB, x, y, vx, vy, U.Rnd(9, 15)); if (i >= 0) { P[i].C = c; P[i].S = big ? CELL * 2 : CELL; } }
        void Shard(double x, double y, double vx, double vy, int c)
        { int i = Spawn(SHARD, x, y, vx, vy, U.Rnd(8, 13)); if (i >= 0) { P[i].C = c; P[i].A = U.Rnd(6.3); P[i].VA = U.Rnd(-18, 18); P[i].S = U.Rnd(3, 7); } }
        void Spark(double x, double y, double vx, double vy) { int i = Spawn(SPARK, x, y, vx, vy, U.Rnd(.2, .55)); if (i >= 0) P[i].Max = .55; }
        void Fire(double x, double y, double vx, double vy, double s) { int i = Spawn(FIRE, x, y, vx, vy, U.Rnd(.35, .75)); if (i >= 0) { P[i].S = s; P[i].Max = .75; } }
        void Smoke(double x, double y, double vx, double vy, double s) { int i = Spawn(SMOKE, x, y, vx, vy, U.Rnd(1.4, 2.6)); if (i >= 0) { P[i].S = s; P[i].Max = 2.6; } }
        void Ash(double x, double y, double vx, double vy) { int i = Spawn(ASH, x, y, vx, vy, U.Rnd(4, 8)); if (i >= 0) { P[i].C = U.Rgb(40, 36, 34); P[i].S = 2; } }
        void Ember(double x, double y, double vx, double vy) { int i = Spawn(EMBER, x, y, vx, vy, U.Rnd(.6, 1.4)); if (i >= 0) P[i].Max = 1.4; }
        void Ring(double x, double y, double life) { int i = Spawn(RING, x, y, 0, 0, life); if (i >= 0) P[i].S = 10; }
        void Flash(double x, double y, double s, double life) { int i = Spawn(FLASH, x, y, 0, 0, life); if (i >= 0) P[i].S = s; }

        double GroundAt(double x, double yPrev) { return (x > TX0 && x < TX1 && yPrev <= TTOP + 2) ? TTOP : FLOOR; }

        // ---------------- 파괴 ----------------
        void DestroyCell(Body b, int i, double wx, double wy, int type, double impulse)
        {
            byte m = b.Mat[i]; MatInfo mm = MatInfo.All[m];
            int x = i % b.W, y = i / b.W;
            double px, py; b.ToWorld(x + .5, y + .5, out px, out py);
            if (type == Dt.HEAT)
            {
                if (U.R.NextDouble() < .35) Ash(px, py, U.Rnd(-30, 30), U.Rnd(-60, 0));
                if (mm.Metal && U.R.NextDouble() < .6) Ember(px, py, U.Rnd(-60, 60), U.Rnd(-80, 10));
            }
            else
            {
                double dx = px - wx, dy = py - wy, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1e-6) d = 1; dx /= d; dy /= d;
                double sp = impulse * U.Rnd(.35, 1.1);
                double bvx = b.Pinned ? 0 : b.VX - b.VA * (py - b.Y), bvy = b.Pinned ? 0 : b.VY + b.VA * (px - b.X);
                double vx = bvx + dx * sp + U.Rnd(-.25, .25) * impulse, vy = bvy + dy * sp - U.Rnd(.35) * impulse;
                if (mm.Glass) { if (U.R.NextDouble() < .8) Shard(px, py, vx * 1.15, vy * 1.15, b.CellColor(i)); }
                else if (U.R.NextDouble() < (type == Dt.EXPLODE ? .5 : .72)) Debris(px, py, vx, vy, b.CellColor(i), U.R.NextDouble() < .12);
                if ((mm.Metal || mm.Spark) && U.R.NextDouble() < .45) Spark(px, py, vx * 1.4 + U.Rnd(-200, 200), vy * 1.4 - U.Rnd(200));
            }
            if (mm.Battery) TriggerBattery(b, x, y);
            if (b.IMat[i] != 0)
            {
                b.Mat[i] = b.IMat[i]; b.Col[i] = b.ICol[i]; b.Hp[i] = MatInfo.All[b.Mat[i]].Hp; b.IMat[i] = 0; b.Burn[i] = 0;
                if (b.Mat[i] == Mt.SENSOR && Cam != null && !Cam.Sensor && b.Cam == Cam) { Cam.Sensor = true; Toast("이미지 센서가 드러났다"); }
            }
            else { b.Mat[i] = 0; b.Heat[i] = 0; b.Burn[i] = 0; }
            if (Cam != null && b.Cam == Cam) Cam.Destroyed++;
        }

        struct Hit { public int N, Kill, Glass, Metal, Spark, BestN; public Body Best; }

        Hit Damage(double wx, double wy, double r, double power, int type, double impulse, Body only)
        {
            Hit res = new Hit();
            double rr = type == Dt.BLUNT ? r * 1.7 : r, reach = rr * CELL + 3;
            lhOk = true; lhX = wx; lhY = wy; lhK = impulse;
            foreach (Body b in Bodies.ToArray())
            {
                if (b.Dead || (only != null && b != only)) continue;
                double dxw = wx - b.X, dyw = wy - b.Y;
                if (dxw * dxw + dyw * dyw > (b.Radius + reach) * (b.Radius + reach)) continue;
                double lx, ly; b.ToLocal(wx, wy, out lx, out ly);
                int x0 = Math.Max(0, (int)Math.Floor(lx - rr)), x1 = Math.Min(b.W - 1, (int)Math.Ceiling(lx + rr));
                int y0 = Math.Max(0, (int)Math.Floor(ly - rr)), y1 = Math.Min(b.H - 1, (int)Math.Ceiling(ly + rr));
                int hit = 0;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        int i = y * b.W + x; byte m = b.Mat[i];
                        if (m == 0) continue;
                        double dx = x + .5 - lx, dy = y + .5 - ly, d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > rr) continue;
                        MatInfo mm = MatInfo.All[m];
                        if (d > r && !mm.Glass) continue;
                        double fall = 1 - Math.Min(1, d / rr) * .65;
                        b.Hp[i] -= power * fall * mm.Res[type] * U.Rnd(.75, 1.25);
                        hit++; res.N++;
                        if (mm.Glass) res.Glass++;
                        if (mm.Metal) res.Metal++;
                        if (mm.Spark) res.Spark++;
                        if (b.Hp[i] <= 0)
                        {
                            double over = -b.Hp[i];
                            DestroyCell(b, i, wx, wy, type, impulse); res.Kill++;
                            if (b.Mat[i] != 0 && type != Dt.BLUNT)
                            {
                                b.Hp[i] -= over * 1.2 + power * fall * .5;
                                if (b.Hp[i] <= 0) { DestroyCell(b, i, wx, wy, type, impulse); res.Kill++; }
                            }
                        }
                    }
                if (hit > 0)
                {
                    b.DirtyImg = true; b.DirtySplit = true; b.Wake();
                    if (hit > res.BestN) { res.BestN = hit; res.Best = b; }
                }
            }
            return res;
        }

        int HeatArea(double wx, double wy, double r, double amt, double cap)
        {
            int n = 0;
            foreach (Body b in Bodies)
            {
                if (b.Dead) continue;
                double dxw = wx - b.X, dyw = wy - b.Y, reach = b.Radius + r * CELL + 3;
                if (dxw * dxw + dyw * dyw > reach * reach) continue;
                double lx, ly; b.ToLocal(wx, wy, out lx, out ly);
                int x0 = Math.Max(0, (int)Math.Floor(lx - r)), x1 = Math.Min(b.W - 1, (int)Math.Ceiling(lx + r));
                int y0 = Math.Max(0, (int)Math.Floor(ly - r)), y1 = Math.Min(b.H - 1, (int)Math.Ceiling(ly + r));
                int hit = 0;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    {
                        int i = y * b.W + x; if (b.Mat[i] == 0) continue;
                        double dx = x + .5 - lx, dy = y + .5 - ly, d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > r) continue;
                        double nh = Math.Min(cap, b.Heat[i] + amt * (1 - d / r));
                        if (nh > b.Heat[i]) { b.Heat[i] = nh; b.Hot.Add(i); hit++; }
                    }
                if (hit > 0) { n += hit; b.DirtyImg = true; b.Wake(); }
            }
            return n;
        }

        void Crack(Body b, double lx, double ly)
        {
            for (int k = 0; k < 4; k++)
            {
                double x = lx, y = ly, ang = U.Rnd(Math.PI * 2), len = U.Rnd(9, 24);
                for (int s = 0; s < len; s++)
                {
                    ang += U.Rnd(-.45, .45); x += Math.Cos(ang); y += Math.Sin(ang);
                    int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                    if (ix < 0 || iy < 0 || ix >= b.W || iy >= b.H) break;
                    int i = iy * b.W + ix; if (b.Mat[i] == 0) continue;
                    b.Hp[i] -= MatInfo.All[b.Mat[i]].Hp * .42;
                    if (b.Hp[i] <= 0) { double wx, wy; b.ToWorld(lx, ly, out wx, out wy); DestroyCell(b, i, wx, wy, Dt.BLUNT, 120); }
                }
            }
            b.DirtyImg = true; b.DirtySplit = true;
        }

        void Explode(double x, double y, double R, double power)
        {
            Damage(x, y, R, power, Dt.EXPLODE, 1100, null);
            HeatArea(x, y, R * .9, .9, 1.3);
            double reach = R * CELL * 3;
            foreach (Body b in Bodies)
            {
                if (b.Pinned || b.Dead) continue;
                double dx = b.X - x, dy = b.Y - y, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1) d = 1;
                if (d > reach) continue;
                double f = 1 - d / reach;
                b.VX += dx / d * 1500 * f; b.VY += dy / d * 1500 * f - 350 * f; b.VA += U.Rnd(-9, 9) * f; b.Wake();
            }
            for (int k = 0; k < PN; k++)
            {
                double dx = P[k].X - x, dy = P[k].Y - y, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1) d = 1;
                if (d > reach) continue;
                double f = 1 - d / reach;
                P[k].VX += dx / d * 1300 * f; P[k].VY += dy / d * 1300 * f - 300 * f; P[k].Rest = false;
            }
            for (int k = 0; k < 90; k++) { double a = U.Rnd(Math.PI * 2), s = U.Rnd(120, 640); Fire(x, y, Math.Cos(a) * s, Math.Sin(a) * s - 80, U.Rnd(12, 26)); }
            for (int k = 0; k < 34; k++) { double a = U.Rnd(Math.PI * 2), s = U.Rnd(40, 220); Smoke(x, y, Math.Cos(a) * s, Math.Sin(a) * s - 60, U.Rnd(16, 32)); }
            for (int k = 0; k < 80; k++) { double a = U.Rnd(Math.PI * 2), s = U.Rnd(300, 1150); Spark(x, y, Math.Cos(a) * s, Math.Sin(a) * s); }
            Ring(x, y, .45); Flash(x, y, R * CELL * 2.6, .22);
            Shake = Math.Max(Shake, 26);
            Snd.Boom(1, Pan(x));
        }

        void TriggerBattery(Body b, int x, int y)
        {
            if (Cam == null || Cam.BattOn || b.Cam != Cam) return;
            Cam.BattOn = true; Cam.BattB = b; Cam.BattLX = x + .5; Cam.BattLY = y + .5; Cam.BattT = 1.2;
            Toast("배터리가 부풀어 오른다… 피해!");
            double wx, wy; b.ToWorld(x, y, out wx, out wy); Snd.Sizzle(Pan(wx));
        }

        // ---------------- 조각 분리 ----------------
        void Unpin(Body b)
        {
            if (!b.Pinned) return;
            b.Pinned = false; b.Anchor = null; b.VX = U.Rnd(-40, 40); b.VY = -30; b.VA = U.Rnd(-.6, .6); b.Wake();
            if (b.Cam == Cam) Toast("카메라가 작업대에서 떨어져 나갔다");
        }

        void Split(Body b)
        {
            int w = b.W, n = w * b.H; byte[] mat = b.Mat;
            int[] lab = new int[n]; for (int i = 0; i < n; i++) lab[i] = -1;
            int[] stack = new int[n];
            List<List<int>> comps = new List<List<int>>(); List<bool> anch = new List<bool>();
            for (int s = 0; s < n; s++)
            {
                if (mat[s] == 0 || lab[s] >= 0) continue;
                int id = comps.Count; List<int> cells = new List<int>(); bool an = false; int sp = 0;
                stack[sp++] = s; lab[s] = id;
                while (sp > 0)
                {
                    int k = stack[--sp]; cells.Add(k);
                    if (b.Anchor != null && b.Anchor[k] != 0) an = true;
                    int x = k % w;
                    if (x > 0 && mat[k - 1] != 0 && lab[k - 1] < 0) { lab[k - 1] = id; stack[sp++] = k - 1; }
                    if (x < w - 1 && mat[k + 1] != 0 && lab[k + 1] < 0) { lab[k + 1] = id; stack[sp++] = k + 1; }
                    if (k >= w && mat[k - w] != 0 && lab[k - w] < 0) { lab[k - w] = id; stack[sp++] = k - w; }
                    if (k < n - w && mat[k + w] != 0 && lab[k + w] < 0) { lab[k + w] = id; stack[sp++] = k + w; }
                }
                comps.Add(cells); anch.Add(an);
            }
            if (comps.Count == 0) { b.Dead = true; return; }
            int keep = -1;
            if (b.Pinned)
            {
                for (int c = 0; c < comps.Count; c++) if (anch[c] && (keep < 0 || comps[c].Count > comps[keep].Count)) keep = c;
                if (keep < 0) Unpin(b);
            }
            if (keep < 0) { keep = 0; for (int c = 1; c < comps.Count; c++) if (comps[c].Count > comps[keep].Count) keep = c; }
            if (comps.Count == 1) { if (b.Count != comps[0].Count) b.Recalc(); return; }
            for (int c = 0; c < comps.Count; c++)
            {
                if (c == keep) continue;
                List<int> cells = comps[c];
                if (cells.Count < 12) ToParticles(b, cells); else Extract(b, cells);
                foreach (int k in cells) { b.Mat[k] = 0; b.IMat[k] = 0; b.Heat[k] = 0; b.Burn[k] = 0; b.Hot.Remove(k); }
            }
            b.Recalc(); b.DirtyImg = true;
        }

        void Extract(Body b, List<int> cells)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            foreach (int k in cells) { int x = k % b.W, y = k / b.W; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            Body nb = new Body(x1 - x0 + 1, y1 - y0 + 1);
            foreach (int k in cells)
            {
                int x = k % b.W - x0, y = k / b.W - y0, j = y * nb.W + x;
                nb.Mat[j] = b.Mat[k]; nb.Col[j] = b.Col[k]; nb.Hp[j] = b.Hp[k]; nb.IMat[j] = b.IMat[k]; nb.ICol[j] = b.ICol[k];
                nb.Burn[j] = b.Burn[k]; nb.Heat[j] = b.Heat[k]; if (nb.Heat[j] > 0) nb.Hot.Add(j);
            }
            nb.ComputeProps(); nb.A = b.A;
            double wx, wy; b.ToWorld(nb.CX + x0, nb.CY + y0, out wx, out wy);
            nb.X = wx; nb.Y = wy;
            double rx = wx - b.X, ry = wy - b.Y;
            nb.VX = b.Pinned ? 0 : b.VX - b.VA * ry; nb.VY = b.Pinned ? 0 : b.VY + b.VA * rx; nb.VA = b.Pinned ? 0 : b.VA;
            if (lhOk)
            {
                double dx = wx - lhX, dy = wy - lhY, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1) d = 1;
                double k2 = Math.Min(320, lhK * .5 / (1 + d / 60));
                nb.VX += dx / d * k2; nb.VY += dy / d * k2 - 40; nb.VA += U.Rnd(-1.5, 1.5);
            }
            nb.Cam = b.Cam;
            Bodies.Add(nb);
        }

        void ToParticles(Body b, List<int> cells)
        {
            foreach (int k in cells)
            {
                if (b.Mat[k] == 0) continue;
                double px, py; b.ToWorld(k % b.W + .5, k / b.W + .5, out px, out py);
                double vx = (b.Pinned ? 0 : b.VX) + U.Rnd(-90, 90), vy = (b.Pinned ? 0 : b.VY) + U.Rnd(-120, 20);
                if (MatInfo.All[b.Mat[k]].Glass) Shard(px, py, vx, vy, b.CellColor(k)); else Debris(px, py, vx, vy, b.CellColor(k), false);
            }
        }

        // ---------------- 물리 ----------------
        static double Resolve(Body b, double rx, double ry, double nx, double ny, double im, double iI)
        {
            double vpx = b.VX - b.VA * ry, vpy = b.VY + b.VA * rx, vn = vpx * nx + vpy * ny;
            if (vn >= 0) return 0;
            double rn = rx * ny - ry * nx, e = vn < -260 ? .28 : 0;
            double j = -(1 + e) * vn / (im + rn * rn * iI);
            b.VX += j * nx * im; b.VY += j * ny * im; b.VA += rn * j * iI;
            double tx = -ny, ty = nx;
            double vpx2 = b.VX - b.VA * ry, vpy2 = b.VY + b.VA * rx, vt = vpx2 * tx + vpy2 * ty, rt = rx * ty - ry * tx;
            double jt = -vt / (im + rt * rt * iI), mu = .6;
            jt = Math.Max(-mu * j, Math.Min(mu * j, jt));
            b.VX += jt * tx * im; b.VY += jt * ty * im; b.VA += rt * jt * iI;
            return -vn;
        }

        void StepBody(Body b, double dt)
        {
            b.VY += G * dt; b.X += b.VX * dt; b.Y += b.VY * dt; b.A += b.VA * dt;
            double c = Math.Cos(b.A), s = Math.Sin(b.A), im = 1 / b.Mass, iI = 1 / b.Inertia;
            bool fl = false, wl = false; double flPen = 0, flRX = 0, flRY = 0, wlPen = 0, wlRX = 0, wlRY = 0, wlN = 0;
            for (int h = 0; h < b.HullX.Length; h++)
            {
                double rx = b.HullX[h] * c - b.HullY[h] * s, ry = b.HullX[h] * s + b.HullY[h] * c, wx = b.X + rx, wy = b.Y + ry;
                if (wx > TX0 && wx < TX1 && wy > TTOP)
                {
                    if (wy - TTOP < 28) { double pen = wy - TTOP; if (pen > flPen) { flPen = pen; fl = true; flRX = rx; flRY = ry; } }
                    else
                    {
                        double left = wx - TX0, right = TX1 - wx, pen = Math.Min(left, right);
                        if (pen > wlPen) { wlPen = pen; wl = true; wlRX = rx; wlRY = ry; wlN = left < right ? -1 : 1; }
                    }
                }
                if (wy > FLOOR) { double pen = wy - FLOOR; if (pen > flPen) { flPen = pen; fl = true; flRX = rx; flRY = ry; } }
                if (wx < 0 && -wx > wlPen) { wlPen = -wx; wl = true; wlRX = rx; wlRY = ry; wlN = 1; }
                if (wx > WW && wx - WW > wlPen) { wlPen = wx - WW; wl = true; wlRX = rx; wlRY = ry; wlN = -1; }
            }
            if (fl)
            {
                b.Y -= flPen;
                double imp = Resolve(b, flRX, flRY, 0, -1, im, iI);
                b.VA *= .985;
                if (imp > 720 && b.Mass > 12 && b.HitCool <= 0)
                {
                    b.HitCool = .15;
                    double px = b.X + flRX, py = b.Y + flRY;
                    Hit r = Damage(px, py, 3 + imp / 420, imp / 170, Dt.BLUNT, imp * .25, b);
                    Snd.Thud(Math.Min(1, imp / 1600), Pan(px));
                    if (r.Glass > 0) Snd.Glass(.7, Pan(px));
                    Shake = Math.Max(Shake, Math.Min(10, imp / 220));
                }
                else if (imp > 240 && b.HitCool <= 0) { b.HitCool = .12; Snd.Thud(Math.Min(.5, imp / 2000), Pan(b.X)); }
            }
            if (wl) { b.X += wlN * wlPen; Resolve(b, wlRX, wlRY, wlN, 0, im, iI); }
        }

        // ---------------- 소환 ----------------
        public double CurrentLoss()
        {
            if (Cam == null) return 0;
            return Cam.Value * Math.Min(1, (double)Cam.Destroyed / Cam.Total / .55);
        }
        public double Ratio { get { return Cam == null ? 0 : Math.Min(1, (double)Cam.Destroyed / Cam.Total); } }

        public void Summon()
        {
            if (Cam != null) TotalPrev += CurrentLoss();
            BrandDef br = Catalog.Brands[SelBrand]; BodyDef bd = br.Bodies[SelBody]; LensDef ld = br.Lenses[SelLens];
            Bodies.Clear(); PN = 0; Bombs.Clear(); Holes.Clear(); grabB = null;
            if (toolLoop != null) { toolLoop.Stop(); toolLoop = null; }
            Body b = Builder.Build(bd, ld);
            b.Pinned = true;
            b.X = WW / 2 + (b.CX - b.W / 2.0) * CELL;
            b.Y = TTOP - (b.MaxY + 1 - b.CY) * CELL;
            Bodies.Add(b);
            Cam c = new Cam(); c.Brand = br; c.BodyD = bd; c.Lens = ld; c.Value = bd.Price + ld.Price; c.Total = b.Total; c.B = b;
            Cam = c; b.Cam = c;
        }

        public void Clean()
        {
            PN = 0; Body keep = null;
            foreach (Body b in Bodies) if (keep == null || b.Count > keep.Count) keep = b;
            Bodies.Clear(); if (keep != null) Bodies.Add(keep);
            grabB = null;
        }

        public void SetTool(int t)
        {
            if (toolLoop != null) { toolLoop.Stop(); toolLoop = null; }
            grabB = null; PDown = false; Tool = t;
            if (ToolId == "hole" && Holes.Count > 0) toolLoop = Snd.Loop("hole");
        }

        // ---------------- 도구 ----------------
        void Nozzle(out double ox, out double oy)
        {
            ox = Math.Max(10, Math.Min(WW - 10, PX - 95)); oy = Math.Min(FLOOR - 10, PY + 70);
        }

        public void ToolDown()
        {
            if (Cam == null || Cam.Drop > 0) return;
            string t = ToolId;
            if (t == "hammer")
            {
                swing = 1;
                Hit r = Damage(PX, PY, 8, 8, Dt.BLUNT, 430, null);
                if (r.Best != null) { double lx, ly; r.Best.ToLocal(PX, PY, out lx, out ly); Crack(r.Best, lx, ly); }
                if (r.N > 0) { Snd.Thud(1, Pan(PX)); if (r.Glass > 0) Snd.Glass(1, Pan(PX)); if (r.Metal > 4) Snd.Clank(Pan(PX)); Shake = Math.Max(Shake, 9); }
                else Snd.Thud(.25, Pan(PX));
            }
            else if (t == "bomb")
            {
                Bomb bm = new Bomb(); bm.X = PX; bm.Y = PY; bm.T = 1.5;
                for (int k = Bodies.Count - 1; k >= 0; k--)
                {
                    Body b = Bodies[k]; if (b.Dead) continue;
                    if (b.CellAt(PX, PY) >= 0) { bm.AB = b; b.ToLocal(PX, PY, out bm.ALX, out bm.ALY); break; }
                }
                if (Bombs.Count < 12) Bombs.Add(bm);
                Snd.Pop(Pan(PX));
            }
            else if (t == "hole")
            {
                if (Holes.Count < 3) { Hole h = new Hole(); h.X = PX; h.Y = PY; h.T = 4.5; h.Max = 4.5; Holes.Add(h); }
                if (toolLoop == null) toolLoop = Snd.Loop("hole");
            }
            else if (t == "hand")
            {
                for (int k = Bodies.Count - 1; k >= 0; k--)
                {
                    Body b = Bodies[k]; if (b.Dead) continue;
                    if (b.CellAt(PX, PY) >= 0)
                    {
                        if (b.Pinned) Unpin(b);
                        b.ToLocal(PX, PY, out grabLX, out grabLY); grabB = b; b.Wake(); break;
                    }
                }
            }
            else
            {
                if (toolLoop != null) toolLoop.Stop();
                toolLoop = Snd.Loop(t);
            }
        }

        public void ToolUp()
        {
            grabB = null;
            if (toolLoop != null && ToolId != "hole") { toolLoop.Stop(); toolLoop = null; }
        }

        void UpdateTool(double dt)
        {
            if (Cam == null || Cam.Drop > 0) return;
            string t = ToolId; double x = PX, y = PY;
            if (t == "drill" && PDown)
            {
                Hit r = Damage(x, y, 2.5, 26 * dt, Dt.CUT, 130, null);
                if (r.N > 0)
                {
                    if (U.R.NextDouble() < .6) Debris(x, y, U.Rnd(-160, 160), U.Rnd(-260, -60), U.Rgb(90, 90, 92), false);
                    if (r.Metal + r.Spark > 0) { Spark(x, y, U.Rnd(-500, 500), U.Rnd(-600, -100)); Spark(x, y, U.Rnd(-500, 500), U.Rnd(-600, -100)); Snd.Spark(Pan(x)); }
                }
                if (toolLoop != null) { toolLoop.Level(r.N > 0 ? .55 : .32); toolLoop.Pitch(r.N > 0 ? .72 : 1); }
            }
            if (t == "saw" && PDown)
            {
                double dx = x - PPX, dy = y - PPY, dist = Math.Sqrt(dx * dx + dy * dy);
                int steps = Math.Max(1, (int)Math.Ceiling(dist / (CELL * .8)));
                int n = 0, metal = 0;
                for (int s = 1; s <= steps; s++)
                {
                    Hit r = Damage(PPX + dx * s / steps, PPY + dy * s / steps, 3, 9, Dt.CUT, 170, null);
                    n += r.N; metal += r.Metal + r.Spark;
                }
                Hit r0 = Damage(x, y, 3, 30 * dt, Dt.CUT, 170, null); n += r0.N; metal += r0.Metal;
                if (n > 0)
                {
                    for (int k = 0; k < (metal > 0 ? 6 : 2); k++) Spark(x, y, U.Rnd(-700, 200), U.Rnd(-700, -100));
                    if (U.R.NextDouble() < .5) Debris(x, y, U.Rnd(-300, 0), U.Rnd(-300, -50), U.Rgb(110, 110, 110), false);
                    Shake = Math.Max(Shake, 2);
                }
                if (toolLoop != null) { toolLoop.Level(n > 0 ? .6 : .35); toolLoop.Pitch(n > 0 ? .8 : 1); }
            }
            if (t == "flame" && PDown)
            {
                double ox, oy; Nozzle(out ox, out oy);
                double dx = x - ox, dy = y - oy, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1) d = 1;
                for (int k = 0; k < 8; k++)
                {
                    double sp = U.Rnd(520, 760), spread = U.Rnd(-.18, .18), ux = dx / d, uy = dy / d, c = Math.Cos(spread), s = Math.Sin(spread);
                    Fire(ox + ux * 12, oy + uy * 12, (ux * c - uy * s) * sp, (ux * s + uy * c) * sp, U.Rnd(8, 15));
                }
                int n = HeatArea(x, y, 11, 1.6 * dt, 1.3);
                if (toolLoop != null) toolLoop.Level(n > 0 ? .55 : .4);
            }
            if (t == "laser" && PDown)
            {
                double dx = x - PPX, dy = y - PPY, dist = Math.Sqrt(dx * dx + dy * dy);
                int steps = Math.Max(1, (int)Math.Ceiling(dist / (CELL * .7))), n = 0;
                for (int s = 1; s <= steps; s++)
                {
                    double px = PPX + dx * s / steps, py = PPY + dy * s / steps;
                    n += Damage(px, py, 1.8, 3.2, Dt.LASER, 60, null).N;
                    HeatArea(px, py, 3.2, .35, 1.3);
                }
                n += Damage(x, y, 1.8, 55 * dt, Dt.LASER, 60, null).N;
                HeatArea(x, y, 3.2, 2.6 * dt, 1.4);
                if (n > 0) { Spark(x, y, U.Rnd(-300, 300), U.Rnd(-400, 0)); Spark(x, y, U.Rnd(-300, 300), U.Rnd(-400, 0)); if (U.R.NextDouble() < .3) Smoke(x, y, U.Rnd(-20, 20), U.Rnd(-60, -20), 6); }
                if (toolLoop != null) { toolLoop.Level(n > 0 ? .5 : .3); toolLoop.Pitch(n > 0 ? 1.15 : 1); }
            }
            if (grabB != null)
            {
                Body b = grabB;
                if (b.Dead) { grabB = null; return; }
                double gx, gy; b.ToWorld(grabLX, grabLY, out gx, out gy);
                double rx = gx - b.X, ry = gy - b.Y;
                double vpx = b.VX - b.VA * ry, vpy = b.VY + b.VA * rx;
                double dvx = (x - gx) * 14 - vpx, dvy = (y - gy) * 14 - vpy;
                double im = 1 / b.Mass, iI = 1 / b.Inertia;
                double Px = dvx / (im + ry * ry * iI) * .5, Py = dvy / (im + rx * rx * iI) * .5;
                b.VX += Px * im; b.VY += Py * im; b.VA += (rx * Py - ry * Px) * iI;
                b.VA *= .92;
                double sp = Math.Sqrt(b.VX * b.VX + b.VY * b.VY); if (sp > 3200) { b.VX *= 3200 / sp; b.VY *= 3200 / sp; }
                b.Wake();
            }
        }

        // ---------------- 갱신 ----------------
        public void Update(double dt)
        {
            Time += dt;
            spin += dt * (PDown && ToolId == "saw" ? 40 : 6);
            if (swing > 0) swing = Math.Max(0, swing - dt * 7);
            if (ToastT > 0) ToastT -= dt;

            if (Cam != null && Cam.Drop > 0)
            {
                Cam.Drop = Math.Max(0, Cam.Drop - dt / .55);
                if (Cam.Drop == 0)
                {
                    Snd.Thud(.8, 0); Shake = Math.Max(Shake, 6);
                    for (int k = 0; k < 24; k++) Smoke(Cam.B.X + U.Rnd(-200, 200), TTOP - 4, U.Rnd(-120, 120), U.Rnd(-40, -5), U.Rnd(8, 16));
                }
            }

            UpdateTool(dt);
            PPX = PX; PPY = PY;

            // 폭탄
            for (int k = Bombs.Count - 1; k >= 0; k--)
            {
                Bomb bm = Bombs[k];
                if (bm.AB != null) { if (bm.AB.Dead) bm.AB = null; else bm.AB.ToWorld(bm.ALX, bm.ALY, out bm.X, out bm.Y); }
                if (bm.AB == null)
                {
                    bm.VY += G * dt; double py0 = bm.Y; bm.Y += bm.VY * dt;
                    double gy = GroundAt(bm.X, py0) - 10; if (bm.Y > gy) { bm.Y = gy; bm.VY = 0; }
                }
                bm.T -= dt;
                if (U.R.NextDouble() < .6) Spark(bm.X + 9, bm.Y - 14, U.Rnd(-80, 80), U.Rnd(-160, -20));
                if (bm.T <= 0) { Bombs.RemoveAt(k); Explode(bm.X, bm.Y, 26, 34); }
            }

            // 배터리
            if (Cam != null && Cam.BattOn && !Cam.BattDone)
            {
                if (!Cam.BattB.Dead) Cam.BattB.ToWorld(Cam.BattLX, Cam.BattLY, out Cam.BattWX, out Cam.BattWY);
                Cam.BattT -= dt;
                if (U.R.NextDouble() < .5) Smoke(Cam.BattWX, Cam.BattWY, U.Rnd(-30, 30), U.Rnd(-90, -30), 10);
                if (U.R.NextDouble() < .5) Spark(Cam.BattWX, Cam.BattWY, U.Rnd(-300, 300), U.Rnd(-400, -50));
                Snd.Sizzle(Pan(Cam.BattWX));
                if (Cam.BattT <= 0)
                {
                    Cam.BattDone = true;
                    Explode(Cam.BattWX, Cam.BattWY, 17, 26); HeatArea(Cam.BattWX, Cam.BattWY, 20, 1.2, 1.3); Toast("배터리 폭발!");
                }
            }

            // 블랙홀
            for (int k = Holes.Count - 1; k >= 0; k--)
            {
                Hole h = Holes[k];
                h.T -= dt;
                double life = 1 - h.T / h.Max, rr = 4 + life * 13;
                Damage(h.X, h.Y, rr, 30 * dt, Dt.SUCK, -260, null);
                foreach (Body b in Bodies)
                {
                    if (b.Pinned || b.Dead) continue;
                    double dx = h.X - b.X, dy = h.Y - b.Y, d = Math.Sqrt(dx * dx + dy * dy); if (d < 1) d = 1;
                    if (d > 520) continue;
                    double f = 2400 * (1 - d / 520);
                    b.VX += dx / d * f * dt; b.VY += dy / d * f * dt - G * dt * .5; b.VA *= .99; b.Wake();
                }
                for (int q = PN - 1; q >= 0; q--)
                {
                    if (P[q].K == RING || P[q].K == FLASH) continue;
                    double dx = h.X - P[q].X, dy = h.Y - P[q].Y, d2 = dx * dx + dy * dy, d = Math.Sqrt(d2); if (d < 1) d = 1;
                    if (d < 14) { P[q] = P[--PN]; continue; }
                    if (d > 600) continue;
                    double a = Math.Min(9000, 5.5e6 / (d2 + 3000));
                    P[q].VX += (dx / d * a - dy / d * a * .35) * dt; P[q].VY += (dy / d * a + dx / d * a * .35) * dt; P[q].Rest = false;
                }
                if (h.T <= 0)
                {
                    Holes.RemoveAt(k);
                    Flash(h.X, h.Y, 200, .25); Ring(h.X, h.Y, .5);
                    Snd.Boom(.5, Pan(h.X)); Shake = Math.Max(Shake, 10);
                    if (Holes.Count == 0 && toolLoop != null && ToolId == "hole") { toolLoop.Stop(); toolLoop = null; }
                }
            }
            if (toolLoop != null && ToolId == "hole") toolLoop.Level(Holes.Count > 0 ? .5 : 0);

            // 열과 불
            foreach (Body b in Bodies.ToArray())
            {
                if (b.Dead || b.Hot.Count == 0) continue;
                bool changed = false;
                int[] hot = new int[b.Hot.Count]; b.Hot.CopyTo(hot);
                foreach (int i in hot)
                {
                    byte m = b.Mat[i];
                    if (m == 0) { b.Heat[i] = 0; b.Hot.Remove(i); continue; }
                    double h = b.Heat[i] - dt * .22;
                    MatInfo mm = MatInfo.All[m]; int x = i % b.W, y = i / b.W;
                    if (mm.Burn > 0 && h > .35)
                    {
                        b.Burn[i] += dt * h * mm.Burn;
                        if (h > .55)
                        {
                            int[] nb = { x > 0 ? i - 1 : -1, x < b.W - 1 ? i + 1 : -1, y > 0 ? i - b.W : -1, y < b.H - 1 ? i + b.W : -1 };
                            foreach (int j in nb)
                            {
                                if (j < 0 || b.Mat[j] == 0 || MatInfo.All[b.Mat[j]].Burn <= 0) continue;
                                double nh = Math.Min(b.Heat[j] + dt * .9 * h, h * .86);
                                if (nh > b.Heat[j]) { b.Heat[j] = nh; b.Hot.Add(j); }
                            }
                        }
                        if (U.R.NextDouble() < dt * h * 2.2) { double wx, wy; b.ToWorld(x + .5, y + .5, out wx, out wy); Fire(wx, wy, U.Rnd(-20, 20), U.Rnd(-90, -30), U.Rnd(5, 10)); }
                        if (U.R.NextDouble() < dt * h * .5) { double wx, wy; b.ToWorld(x + .5, y + .5, out wx, out wy); Smoke(wx, wy, U.Rnd(-15, 15), U.Rnd(-70, -30), 8); }
                        if (b.Burn[i] >= 1) { double wx, wy; b.ToWorld(x + .5, y + .5, out wx, out wy); DestroyCell(b, i, wx, wy, Dt.HEAT, 30); b.DirtySplit = true; }
                    }
                    if (mm.Metal && h > 1.15) { double wx, wy; b.ToWorld(x + .5, y + .5, out wx, out wy); DestroyCell(b, i, wx, wy, Dt.HEAT, 30); b.DirtySplit = true; }
                    else if (mm.Glass && h > .95) { double wx, wy; b.ToWorld(x + .5, y + .5, out wx, out wy); DestroyCell(b, i, wx, wy - 2, Dt.BLUNT, 90); b.DirtySplit = true; Snd.Tink(Pan(wx)); }
                    else if (mm.Battery && h > .5) TriggerBattery(b, x, y);
                    if (h <= .01) { b.Heat[i] = 0; b.Hot.Remove(i); } else b.Heat[i] = h;
                    changed = true;
                }
                if (changed) b.DirtyImg = true;
            }

            // 분리
            foreach (Body b in Bodies.ToArray())
            {
                b.SplitT -= dt; b.HitCool -= dt;
                if (b.DirtySplit && b.SplitT <= 0 && !b.Dead) { b.DirtySplit = false; b.SplitT = .05; Split(b); }
            }

            // 강체
            double hs = dt / 2;
            foreach (Body b in Bodies)
            {
                if (b.Pinned || b.Dead) continue;
                if (b.Asleep && grabB != b) continue;
                StepBody(b, hs); StepBody(b, hs);
                if (Math.Abs(b.VX) < 14 && Math.Abs(b.VY) < 14 && Math.Abs(b.VA) < .2 && grabB != b)
                {
                    b.SleepT += dt; if (b.SleepT > .7) { b.Asleep = true; b.VX = 0; b.VY = 0; b.VA = 0; }
                }
                else b.SleepT = 0;
                if (b.Y > WH + 600) b.Dead = true;
            }
            Bodies.RemoveAll(bb => bb.Dead);
            if (Bodies.Count > 90)
            {
                int si = -1;
                for (int k = 0; k < Bodies.Count; k++) if (!Bodies[k].Pinned && (si < 0 || Bodies[k].Count < Bodies[si].Count)) si = k;
                if (si >= 0)
                {
                    Body b = Bodies[si]; List<int> all = new List<int>();
                    for (int i = 0; i < b.W * b.H; i++) if (b.Mat[i] != 0) all.Add(i);
                    ToParticles(b, all); Bodies.RemoveAt(si);
                }
            }

            // 파티클
            for (int k = PN - 1; k >= 0; k--)
            {
                P[k].Life -= dt;
                if (P[k].Life <= 0) { P[k] = P[--PN]; continue; }
                int kind = P[k].K;
                if (kind == DEB || kind == SHARD || kind == ASH || kind == EMBER)
                {
                    if (P[k].Rest) continue;
                    double gk = kind == ASH ? .15 : 1;
                    P[k].VY += G * gk * dt;
                    if (kind == ASH) P[k].VX += U.Rnd(-40, 40) * dt;
                    double py0 = P[k].Y;
                    P[k].X += P[k].VX * dt; P[k].Y += P[k].VY * dt; P[k].A += P[k].VA * dt;
                    if (P[k].X > TX0 && P[k].X < TX1 && py0 > TTOP + 2 && P[k].Y > TTOP)
                    { P[k].X = (P[k].X - TX0 < TX1 - P[k].X) ? TX0 - 1 : TX1 + 1; P[k].VX *= -.4; }
                    double gy = GroundAt(P[k].X, py0);
                    if (P[k].Y > gy)
                    {
                        P[k].Y = gy;
                        if (Math.Abs(P[k].VY) < 70) { P[k].VY = 0; P[k].VX *= .5; if (Math.Abs(P[k].VX) < 12) P[k].Rest = true; }
                        else { P[k].VY *= -.32; P[k].VX *= .7; P[k].VA *= .6; if (kind == SHARD && U.R.NextDouble() < .25) Snd.Tink(Pan(P[k].X)); }
                    }
                    if (P[k].X < 0) { P[k].X = 0; P[k].VX = Math.Abs(P[k].VX) * .5; }
                    if (P[k].X > WW) { P[k].X = WW; P[k].VX = -Math.Abs(P[k].VX) * .5; }
                }
                else if (kind == SPARK)
                {
                    P[k].VY += G * .6 * dt; double py0 = P[k].Y;
                    P[k].X += P[k].VX * dt; P[k].Y += P[k].VY * dt;
                    double gy = GroundAt(P[k].X, py0); if (P[k].Y > gy) { P[k].Y = gy; P[k].VY *= -.35; P[k].VX *= .6; }
                }
                else if (kind == FIRE)
                {
                    double drag = Math.Pow(.08, dt); P[k].VX *= drag; P[k].VY = P[k].VY * drag - 420 * dt;
                    P[k].X += P[k].VX * dt; P[k].Y += P[k].VY * dt; P[k].S += dt * 14;
                }
                else if (kind == SMOKE)
                {
                    P[k].VX *= Math.Pow(.5, dt); P[k].VY = P[k].VY * Math.Pow(.6, dt) - 40 * dt;
                    P[k].X += P[k].VX * dt; P[k].Y += P[k].VY * dt; P[k].S += dt * 20;
                }
                else if (kind == RING) P[k].S += 1100 * dt;
            }

            Shake *= Math.Pow(.004, dt);
            if (Shake < .1) Shake = 0;

            // 판정
            if (Cam != null)
            {
                double ratio = Ratio;
                if (ratio >= .05 && !Cam.Counted) { Cam.Counted = true; Sacrificed++; }
                if (ratio >= .55 && !Cam.Wrecked) { Cam.Wrecked = true; Toast("수리 불가 판정 · " + U.Won(Cam.Value)); }
            }
        }

        // =============================================================
        //  그리기
        // =============================================================
        int[] stageBg; int bgW, bgH;

        public void Layout(Rectangle stage)
        {
            Stage = stage;
            double visW = stage.Width < stage.Height ? 700 : WW;
            S = Math.Min(stage.Width / visW, stage.Height / WH);
            OX = stage.X + stage.Width / 2.0 - WW / 2 * S;
            OY = stage.Bottom - WH * S;
            BuildBg();
        }

        void BuildBg()
        {
            bgW = Math.Max(1, Stage.Width); bgH = Math.Max(1, Stage.Height);
            using (Bitmap bmp = new Bitmap(bgW, bgH, PixelFormat.Format32bppRgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush wall = new LinearGradientBrush(new Rectangle(0, 0, bgW, bgH), Color.FromArgb(43, 45, 49), Color.FromArgb(27, 28, 31), 90f))
                    g.FillRectangle(wall, 0, 0, bgW, bgH);
                g.TranslateTransform((float)(OX - Stage.X), (float)(OY - Stage.Y));
                g.ScaleTransform((float)S, (float)S);
                double x0 = -(OX - Stage.X) / S - 20, x1 = (Stage.Width - (OX - Stage.X)) / S + 20, y0 = -(OY - Stage.Y) / S - 20;
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    for (double y = Math.Floor(y0 / 26) * 26 + 13; y < FLOOR - 60; y += 26)
                        for (double x = Math.Floor(x0 / 26) * 26 + 13; x < x1; x += 26)
                            g.FillEllipse(dot, (float)(x - 2.2), (float)(y - 2.2), 4.4f, 4.4f);
                using (GraphicsPath lamp = new GraphicsPath())
                {
                    lamp.AddEllipse(500 - 760, -60 - 760, 1520, 1520);
                    using (PathGradientBrush pg = new PathGradientBrush(lamp))
                    {
                        pg.CenterPoint = new PointF(500, -60);
                        pg.CenterColor = Color.FromArgb(58, 255, 214, 150);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 214, 150) };
                        g.FillRectangle(pg, (float)x0, (float)y0, (float)(x1 - x0), (float)(FLOOR - y0));
                    }
                }
                using (Font f = new Font("Malgun Gothic", 26f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(28, 242, 194, 48)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    g.DrawString("파괴 실험실 · 보호안경 착용", f, sb, 500, 52, sf);
                }
                using (LinearGradientBrush fl = new LinearGradientBrush(new RectangleF(0, (float)FLOOR - 1, 10, (float)(WH - FLOOR + 402)), Color.FromArgb(29, 30, 33), Color.FromArgb(12, 13, 15), 90f))
                    g.FillRectangle(fl, (float)x0, (float)FLOOR, (float)(x1 - x0), (float)(WH - FLOOR + 400));
                using (SolidBrush ln = new SolidBrush(Color.FromArgb(14, 255, 255, 255))) g.FillRectangle(ln, (float)x0, (float)FLOOR, (float)(x1 - x0), 1.5f);
                // 작업대
                float tx0 = (float)TX0, tx1 = (float)TX1, tt = (float)TTOP;
                using (SolidBrush leg = new SolidBrush(Color.FromArgb(38, 40, 44)))
                {
                    g.FillRectangle(leg, tx0 + 26, tt + 30, 22, (float)FLOOR - tt - 30);
                    g.FillRectangle(leg, tx1 - 48, tt + 30, 22, (float)FLOOR - tt - 30);
                    g.FillRectangle(leg, tx0 + 26, (float)FLOOR - 40, tx1 - tx0 - 52, 10);
                }
                using (LinearGradientBrush top = new LinearGradientBrush(new RectangleF(tx0, tt - 1, 10, 18), Color.FromArgb(116, 119, 124), Color.FromArgb(70, 73, 78), 90f))
                    g.FillRectangle(top, tx0, tt, tx1 - tx0, 16);
                using (SolidBrush hl = new SolidBrush(Color.FromArgb(64, 255, 255, 255))) g.FillRectangle(hl, tx0, tt, tx1 - tx0, 1.2f);
                using (SolidBrush ap = new SolidBrush(Color.FromArgb(30, 32, 35))) g.FillRectangle(ap, tx0 + 6, tt + 16, tx1 - tx0 - 12, 16);
                g.SetClip(new RectangleF(tx0 + 6, tt + 19, tx1 - tx0 - 12, 10));
                using (SolidBrush yb = new SolidBrush(Color.FromArgb(225, 180, 42))) g.FillRectangle(yb, tx0 + 6, tt + 19, tx1 - tx0 - 12, 10);
                using (SolidBrush kb = new SolidBrush(Color.FromArgb(24, 24, 26)))
                    for (float x = tx0 - 20; x < tx1 + 20; x += 22)
                        g.FillPolygon(kb, new PointF[] { new PointF(x, tt + 29), new PointF(x + 10, tt + 19), new PointF(x + 20, tt + 19), new PointF(x + 10, tt + 29) });
                g.ResetClip();
                g.Flush();
                // 그린 배경을 픽셀 배열로 복사
                int[] px = new int[bgW * bgH];
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, bgW, bgH), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                for (int y = 0; y < bgH; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), px, y * bgW, bgW);
                bmp.UnlockBits(d);
                for (int i = 0; i < px.Length; i++) px[i] &= 0xFFFFFF;
                stageBg = px;
            }
        }

        public void Render(Fb fb)
        {
            fb.SetClip(Stage);
            double sk = Shake;
            int sx = (int)((U.R.NextDouble() - .5) * sk * S), sy = (int)((U.R.NextDouble() - .5) * sk * S);
            // 배경
            if (stageBg != null)
            {
                for (int y = 0; y < bgH; y++)
                {
                    int srcY = Math.Min(bgH - 1, Math.Max(0, y - sy));
                    int dstY = Stage.Y + y; if (dstY < 0 || dstY >= fb.H) continue;
                    int dst = dstY * fb.W + Stage.X, src = srcY * bgW;
                    if (sx == 0) { Array.Copy(stageBg, src, fb.Px, dst, bgW); continue; }
                    for (int x = 0; x < bgW; x++)
                    {
                        int srcX = x - sx; if (srcX < 0) srcX = 0; else if (srcX >= bgW) srcX = bgW - 1;
                        fb.Px[dst + x] = stageBg[src + srcX];
                    }
                }
            }
            fb.ClearGlow();
            double ox = OX + sx, oy = OY + sy;

            // 카메라 그림자
            if (Cam != null && Cam.B != null && Cam.B.Pinned && !Cam.B.Dead)
            {
                double k = 1 - Cam.Drop, wdt = Cam.B.W * CELL * .5 * (.6 + .4 * k) * S;
                double cx = ox + Cam.B.X * S, cy = oy + (TTOP + 2) * S;
                for (int y = (int)(cy - 7 * S); y <= (int)(cy + 7 * S); y++)
                    for (int x = (int)(cx - wdt); x <= (int)(cx + wdt); x++)
                    {
                        double dx = (x - cx) / wdt, dy = (y - cy) / (7 * S), d = dx * dx + dy * dy;
                        if (d < 1) fb.Blend(x, y, 0, .35 * k * (1 - d));
                    }
            }

            // 몸체
            foreach (Body b in Bodies)
            {
                if (b.Dead) continue;
                if (b.DirtyImg) b.Redraw();
                double off = (Cam != null && b == Cam.B && Cam.Drop > 0) ? -Cam.Drop * Cam.Drop * 560 : 0;
                DrawBody(fb, b, ox, oy + off * S);
            }

            // 파티클 (일반)
            for (int k = 0; k < PN; k++)
            {
                int kind = P[k].K;
                double px = ox + P[k].X * S, py = oy + P[k].Y * S;
                if (kind == DEB || kind == ASH)
                {
                    int sz = Math.Max(1, (int)Math.Round(P[k].S * S));
                    double a = P[k].Life < 1 ? P[k].Life : 1;
                    if (a >= 1) fb.FillRect((int)(px - sz / 2.0), (int)(py - sz / 2.0), sz, sz, P[k].C);
                    else fb.BlendRect((int)(px - sz / 2.0), (int)(py - sz / 2.0), sz, sz, P[k].C, a);
                }
                else if (kind == SHARD)
                {
                    double a = (P[k].Life < 1 ? P[k].Life : 1) * .92, c = Math.Cos(P[k].A), s = Math.Sin(P[k].A), z = P[k].S * S;
                    fb.Poly(new double[] { px + c * z, px + (-c * .6 - s * .7) * z, px + (-c * .4 + s * .8) * z },
                            new double[] { py + s * z, py + (-s * .6 + c * .7) * z, py + (-s * .4 - c * .8) * z }, P[k].C, a);
                    if ((((int)(P[k].X + P[k].Y + Time * 30)) % 9) == 0) { fb.Add((int)px, (int)py, 160, 170, 190); fb.Glow(px, py, 30, 34, 40); }
                }
                else if (kind == SMOKE)
                {
                    double a = Math.Min(1, P[k].Life / P[k].Max * 1.4) * .45;
                    fb.SoftDisc(px, py, P[k].S * S, U.Rgb(64, 64, 68), a, false);
                }
            }

            // 폭탄
            foreach (Bomb bm in Bombs)
            {
                double bx = ox + bm.X * S, by = oy + bm.Y * S;
                bool blink = bm.T < .5 && ((int)(Time * 16)) % 2 == 0;
                fb.Disc(bx, by, 13 * S, blink ? U.Rgb(230, 90, 60) : U.Rgb(36, 37, 40), 1);
                fb.Disc(bx - 4 * S, by - 4 * S, 4 * S, U.Rgb(110, 112, 118), .5);
                fb.FillRect((int)(bx + 3 * S), (int)(by - 16 * S), (int)(8 * S), (int)(6 * S), U.Rgb(58, 60, 64));
                fb.Glow(bx + 9 * S, by - 24 * S, 120, 90, 30);
                double frac = bm.T / 1.5;
                fb.Ring(bx, by, 19 * S, U.Rgb(242, 194, 48), .9, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * frac, 2);
                if (blink) fb.GlowDisc(bx, by, 40 * S, 60, 15, 5);
            }

            // 블랙홀
            foreach (Hole h in Holes)
            {
                double hx = ox + h.X * S, hy = oy + h.Y * S, life = 1 - h.T / h.Max, R = (18 + life * 34) * S;
                for (int k = 0; k < 3; k++)
                {
                    int col = k == 1 ? U.Rgb(255, 150, 70) : U.Rgb(150, 90, 255);
                    double st = Time * (3 + k) + k * 2;
                    fb.Ring(hx, hy, R * (1.2 + k * .35), col, .6, st, st + Math.PI * 1.3, 3 - k);
                    for (int q = 0; q < 10; q++) { double t = st + Math.PI * 1.3 * q / 10, rr = R * (1.2 + k * .35); fb.Glow(hx + Math.Cos(t) * rr, hy + Math.Sin(t) * rr, (col >> 16 & 255) * .25, (col >> 8 & 255) * .25, (col & 255) * .25); }
                }
                fb.Disc(hx, hy, R, 0, 1);
            }

            // 발광 파티클
            for (int k = 0; k < PN; k++)
            {
                int kind = P[k].K;
                double px = ox + P[k].X * S, py = oy + P[k].Y * S;
                if (kind == FIRE)
                {
                    double a = Math.Max(0, P[k].Life / P[k].Max), r = P[k].S * S;
                    fb.SoftDisc(px, py, r * .55, U.Rgb(255, 196, 110), a * .9, true);
                    fb.GlowDisc(px, py, r * 1.5, 26 * a, 11 * a, 2 * a);
                }
                else if (kind == SPARK)
                {
                    double a = Math.Max(0, P[k].Life / P[k].Max);
                    fb.Line(px, py, px - P[k].VX * .018 * S, py - P[k].VY * .018 * S, U.Rgb(255, 214, 120), a, true, 1);
                    fb.Glow(px, py, 240 * a, 150 * a, 40 * a);
                }
                else if (kind == EMBER)
                {
                    double a = Math.Max(0, P[k].Life / P[k].Max);
                    fb.Add((int)px, (int)py, 255 * a, 150 * a, 50 * a); fb.Add((int)px + 1, (int)py, 255 * a, 150 * a, 50 * a);
                    fb.Glow(px, py, 160 * a, 70 * a, 16 * a);
                }
                else if (kind == FLASH)
                {
                    double a = Math.Max(0, P[k].Life / P[k].Max), r = P[k].S * S;
                    fb.SoftDisc(px, py, r * .6, U.Rgb(255, 236, 200), a * .8, true);
                    fb.GlowDisc(px, py, r * 1.4, 90 * a, 66 * a, 36 * a);
                }
                else if (kind == RING)
                {
                    double a = Math.Max(0, P[k].Life / P[k].Max) * .7;
                    fb.Ring(px, py, P[k].S * S, U.Rgb(255, 226, 168), a, 0, Math.PI * 2, 2);
                }
            }

            // 레이저
            double lsx = Math.Min(Stage.Right - 30, ox + (WW - 28) * S), lsy = Stage.Y + 30;
            if (ToolId == "laser")
            {
                if (PDown && Cam != null && Cam.Drop <= 0)
                {
                    double ex = ox + PX * S, ey = oy + PY * S, fl = .85 + U.R.NextDouble() * .15;
                    fb.Line(lsx, lsy, ex, ey, U.Rgb(255, 70, 70), .55 * fl, true, 3);
                    fb.Line(lsx, lsy, ex, ey, U.Rgb(255, 230, 230), .9, true, 1);
                    double dx = ex - lsx, dy = ey - lsy; int n = (int)(Math.Sqrt(dx * dx + dy * dy) / 6);
                    for (int i = 0; i <= n; i++) { double t = (double)i / Math.Max(1, n); fb.Glow(lsx + dx * t, lsy + dy * t, 70 * fl, 10, 10); }
                    fb.SoftDisc(ex, ey, 14 * S, U.Rgb(255, 200, 190), .9, true);
                    fb.GlowDisc(ex, ey, 30 * S, 80, 20, 14);
                }
                fb.FillRect((int)(lsx - 22 * S), (int)(lsy - 18 * S), (int)(44 * S), (int)(22 * S), U.Rgb(58, 61, 66));
                fb.FillRect((int)(lsx - 22 * S), (int)(lsy - 18 * S), (int)(44 * S), (int)(4 * S), U.Rgb(225, 180, 42));
                fb.Disc(lsx, lsy + 4 * S, 5 * S, PDown ? U.Rgb(255, 90, 74) : U.Rgb(122, 42, 38), 1);
            }

            // 손으로 잡은 줄
            if (grabB != null && !grabB.Dead)
            {
                double gx, gy; grabB.ToWorld(grabLX, grabLY, out gx, out gy);
                double ax = ox + gx * S, ay = oy + gy * S, bx = ox + PX * S, by = oy + PY * S;
                double dx = bx - ax, dy = by - ay; int n = (int)(Math.Sqrt(dx * dx + dy * dy) / 3);
                for (int i = 0; i < n; i++) if ((i / 2) % 2 == 0) { double t = (double)i / n; fb.Blend((int)(ax + dx * t), (int)(ay + dy * t), U.Rgb(242, 194, 48), .8); }
            }

            // 빛 번짐
            fb.BlurGlow();
            fb.CompositeGlow(1.0);

            if (POver || PDown) DrawCursor(fb, ox, oy);
            fb.ResetClip();
        }

        void DrawBody(Fb fb, Body b, double ox, double oy)
        {
            double sc = S;
            double c = Math.Cos(b.A), s = Math.Sin(b.A);
            // 화면 경계 상자
            double mnx = 1e9, mny = 1e9, mxx = -1e9, mxy = -1e9;
            double[] cxs = { 0, b.W, 0, b.W }, cys = { 0, 0, b.H, b.H };
            for (int i = 0; i < 4; i++)
            {
                double wx, wy; b.ToWorld(cxs[i], cys[i], out wx, out wy);
                double px = ox + wx * sc, py = oy + wy * sc;
                mnx = Math.Min(mnx, px); mxx = Math.Max(mxx, px); mny = Math.Min(mny, py); mxy = Math.Max(mxy, py);
            }
            int x0 = Math.Max(fb.CX0, (int)Math.Floor(mnx)), x1 = Math.Min(fb.CX1 - 1, (int)Math.Ceiling(mxx));
            int y0 = Math.Max(fb.CY0, (int)Math.Floor(mny)), y1 = Math.Min(fb.CY1 - 1, (int)Math.Ceiling(mxy));
            if (x0 > x1 || y0 > y1) return;
            double inv = 1.0 / (CELL * sc);
            double dlx = c * inv, dly = -s * inv;
            int W = b.W, H = b.H; int[] img = b.Img; double[] heat = b.Heat;
            for (int y = y0; y <= y1; y++)
            {
                double wx0 = (x0 + .5 - ox) / sc - b.X, wy0 = (y + .5 - oy) / sc - b.Y;
                double lx = (wx0 * c + wy0 * s) / CELL + b.CX, ly = (-wx0 * s + wy0 * c) / CELL + b.CY;
                int o = y * fb.W;
                for (int x = x0; x <= x1; x++, lx += dlx, ly += dly)
                {
                    if (lx < 0 || ly < 0) continue;
                    int ix = (int)lx, iy = (int)ly;
                    if (ix >= W || iy >= H) continue;
                    int idx = iy * W + ix, v = img[idx];
                    if (v == 0) continue;
                    fb.Px[o + x] = v & 0xFFFFFF;
                    double h = heat[idx];
                    if (h > .3 && ((x | y) & 1) == 0) fb.Glow(x, y, 9 * h, 3.5 * h, .4 * h);
                }
            }
        }

        void DrawCursor(Fb fb, double ox, double oy)
        {
            double x = ox + PX * S, y = oy + PY * S, k = S;
            string t = ToolId;
            if (t == "hammer")
            {
                double ang = -.55 + swing * .9, c = Math.Cos(ang), s = Math.Sin(ang);
                double bx = x + 34 * k, by = y + 46 * k;
                RotRect(fb, bx, by, c, s, -4 * k, -62 * k, 8 * k, 66 * k, U.Rgb(138, 90, 52));
                RotRect(fb, bx, by, c, s, -4 * k, -14 * k, 8 * k, 18 * k, U.Rgb(91, 47, 26));
                RotRect(fb, bx, by, c, s, -22 * k, -78 * k, 44 * k, 20 * k, U.Rgb(150, 153, 158));
                RotRect(fb, bx, by, c, s, -22 * k, -78 * k, 44 * k, 5 * k, U.Rgb(205, 208, 213));
            }
            else if (t == "drill")
            {
                double j = PDown ? U.Rnd(-1.5, 1.5) : 0, c = Math.Cos(-Math.PI / 4), s = Math.Sin(-Math.PI / 4);
                double bx = x + j, by = y + j;
                fb.Poly(new double[] { bx, bx + (4 * c - 22 * s) * k, bx + (-4 * c - 22 * s) * k }, new double[] { by, by + (4 * s + 22 * c) * k, by + (-4 * s + 22 * c) * k }, U.Rgb(184, 188, 194), 1);
                RotRect(fb, bx, by, c, s, -11 * k, 22 * k, 22 * k, 30 * k, U.Rgb(225, 180, 42));
                RotRect(fb, bx, by, c, s, -7 * k, 52 * k, 14 * k, 26 * k, U.Rgb(42, 43, 46));
            }
            else if (t == "saw")
            {
                double[] xs = new double[24], ys = new double[24];
                for (int i = 0; i < 24; i++) { double a = i / 24.0 * Math.PI * 2 + spin, r = (i % 2 != 0 ? 21 : 26) * k; xs[i] = x + Math.Cos(a) * r; ys[i] = y + Math.Sin(a) * r; }
                fb.Poly(xs, ys, U.Rgb(200, 204, 210), .92);
                fb.Disc(x, y, 6 * k, U.Rgb(58, 60, 64), 1);
            }
            else if (t == "flame")
            {
                double nx, ny; Nozzle(out nx, out ny);
                double sx = ox + nx * S, sy = oy + ny * S, a = Math.Atan2(y - sy, x - sx), c = Math.Cos(a), s = Math.Sin(a);
                RotRect(fb, sx, sy, c, s, -58 * k, -10 * k, 22 * k, 20 * k, U.Rgb(192, 57, 43));
                RotRect(fb, sx, sy, c, s, -40 * k, -6 * k, 52 * k, 12 * k, U.Rgb(85, 88, 94));
                fb.Ring(x, y, 30 * k, U.Rgb(255, 170, 80), .5, 0, Math.PI * 2, 1);
            }
            else if (t == "laser")
            {
                int col = U.Rgb(255, 90, 80);
                fb.Ring(x, y, 9 * k, col, .9, 0, Math.PI * 2, 1);
                fb.Line(x - 16 * k, y, x - 5 * k, y, col, .9, false, 1); fb.Line(x + 5 * k, y, x + 16 * k, y, col, .9, false, 1);
                fb.Line(x, y - 16 * k, x, y - 5 * k, col, .9, false, 1); fb.Line(x, y + 5 * k, x, y + 16 * k, col, .9, false, 1);
            }
            else if (t == "bomb")
            {
                fb.Disc(x, y, 13 * k, U.Rgb(42, 43, 46), .6);
                fb.Ring(x, y, 26 * CELL * .9 * k, U.Rgb(242, 194, 48), .35, 0, Math.PI * 2, 1);
            }
            else if (t == "hole")
            {
                fb.Ring(x, y, 18 * k, U.Rgb(160, 110, 255), .8, Time * 4, Time * 4 + Math.PI * 1.4, 2);
                fb.Ring(x, y, 9 * k, U.Rgb(160, 110, 255), .8, -Time * 6, -Time * 6 + Math.PI * 1.2, 2);
            }
            else if (t == "hand")
            {
                double r = (grabB != null ? 10 : 14) * k;
                fb.Disc(x, y, r, grabB != null ? U.Rgb(242, 194, 48) : U.Rgb(255, 255, 255), grabB != null ? .35 : .08);
                fb.Ring(x, y, r, U.Rgb(255, 255, 255), .85, 0, Math.PI * 2, 2);
            }
        }
        // 회전한 사각형 (원점 bx,by 기준, 로컬 좌표 x,y,w,h)
        static void RotRect(Fb fb, double bx, double by, double c, double s, double x, double y, double w, double h, int col)
        {
            double[] lx = { x, x + w, x + w, x }, ly = { y, y, y + h, y + h };
            double[] xs = new double[4], ys = new double[4];
            for (int i = 0; i < 4; i++) { xs[i] = bx + lx[i] * c - ly[i] * s; ys[i] = by + lx[i] * s + ly[i] * c; }
            fb.Poly(xs, ys, col, 1);
        }
    }

    // =================================================================
    //  창 + UI
    // =================================================================
    sealed class MainForm : Form
    {
        readonly Audio audio = new Audio();
        readonly Game game;
        readonly Fb fb = new Fb();
        IntPtr memDC = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero, bits = IntPtr.Zero;
        int bw, bh, k = 1; double u = 1, dpi = 1;
        readonly Stopwatch clock = Stopwatch.StartNew(); double lastT;
        bool slow, fullscreen, cursorHidden; FormWindowState prevState; FormBorderStyle prevBorder;
        int mouseX, mouseY;

        // 색
        const int BENCH = 0x131416, PANEL = 0x1C1E21, PANEL2 = 0x25282C, LINE = 0x34383E, TEXT = 0xECE8DF, MUTED = 0x9A968D;
        const int HAZARD = 0xF2C230, INK = 0x1B1606, REC = 0xE8473A, SELBG = 0x2A2718;

        Font fTitle, fH2, fLabel, fBody, fBodyB, fSmall, fMono, fSummon, fTool, fKbd;
        Rectangle panelR, hudR, toolsR, stageR;

        sealed class HitBox { public Rectangle R; public Action Click; public string Id; }
        readonly List<HitBox> hits = new List<HitBox>();
        struct TextCmd { public string S; public Font F; public Rectangle R; public int C; public TextFormatFlags Fl; }
        readonly List<TextCmd> texts = new List<TextCmd>();
        struct IconCmd { public int Tool; public Rectangle R; public int C; }
        readonly List<IconCmd> icons = new List<IconCmd>();

        public MainForm()
        {
            game = new Game(audio);
            Text = "카메라 파괴소";
            BackColor = Color.FromArgb(19, 20, 22);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            KeyPreview = true;
            using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96.0;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(wa.Width, (int)(1440 * dpi)), h = Math.Min(wa.Height, (int)(880 * dpi));
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
            MinimumSize = new Size(Math.Min(wa.Width, (int)(1180 * dpi)), Math.Min(wa.Height, (int)(720 * dpi)));
            audio.Start();
            game.Summon();
            Application.Idle += OnIdle;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; if (Native.DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) Native.DwmSetWindowAttribute(Handle, 19, ref on, 4); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.Idle -= OnIdle;
            audio.Stop();
            FreeSurface();
            base.OnFormClosed(e);
        }

        // ---------------- 화면 버퍼 ----------------
        void FreeSurface()
        {
            if (memDC != IntPtr.Zero) { Native.SelectObject(memDC, oldBmp); Native.DeleteDC(memDC); memDC = IntPtr.Zero; }
            if (hBmp != IntPtr.Zero) { Native.DeleteObject(hBmp); hBmp = IntPtr.Zero; }
        }

        void EnsureSurface()
        {
            int cw = Math.Max(1, ClientSize.Width), ch = Math.Max(1, ClientSize.Height);
            int kk = (long)cw * ch > 2400000 ? 2 : 1;
            int w = Math.Max(1, cw / kk), h = Math.Max(1, ch / kk);
            if (memDC != IntPtr.Zero && w == bw && h == bh && kk == k) return;
            FreeSurface();
            k = kk; bw = w; bh = h; u = dpi / k;
            Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
            bi.biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = bw; bi.biHeight = -bh; bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
            memDC = Native.CreateCompatibleDC(IntPtr.Zero);
            hBmp = Native.CreateDIBSection(memDC, ref bi, 0, out bits, IntPtr.Zero, 0);
            oldBmp = Native.SelectObject(memDC, hBmp);
            fb.Resize(bw, bh);
            MakeFonts();
            DoLayout();
        }

        void MakeFonts()
        {
            Font[] old = { fTitle, fH2, fLabel, fBody, fBodyB, fSmall, fMono, fSummon, fTool, fKbd };
            foreach (Font f in old) if (f != null) f.Dispose();
            fTitle = new Font("Malgun Gothic", (float)(24 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fH2 = new Font("Malgun Gothic", (float)(19 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fLabel = new Font("Malgun Gothic", (float)(11.5 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fBody = new Font("Malgun Gothic", (float)(13.5 * u), FontStyle.Regular, GraphicsUnit.Pixel);
            fBodyB = new Font("Malgun Gothic", (float)(13.5 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fSmall = new Font("Malgun Gothic", (float)(11.5 * u), FontStyle.Regular, GraphicsUnit.Pixel);
            fMono = new Font("Consolas", (float)(16 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fSummon = new Font("Malgun Gothic", (float)(20 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fTool = new Font("Malgun Gothic", (float)(12.5 * u), FontStyle.Bold, GraphicsUnit.Pixel);
            fKbd = new Font("Consolas", (float)(10.5 * u), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        int Uu(double v) { return (int)Math.Round(v * u); }

        void DoLayout()
        {
            int pad = Uu(12);
            panelR = new Rectangle(pad, pad, Uu(300), bh - pad * 2);
            int rx = panelR.Right + pad, rw = bw - rx - pad;
            hudR = new Rectangle(rx, pad, rw, Uu(66));
            toolsR = new Rectangle(rx, bh - pad - Uu(66), rw, Uu(66));
            stageR = new Rectangle(rx, hudR.Bottom + Uu(10), rw, toolsR.Y - Uu(10) - (hudR.Bottom + Uu(10)));
            game.Layout(stageR);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated && ClientSize.Width > 0 && ClientSize.Height > 0) { EnsureSurface(); DoLayout(); } }
        protected override void OnPaint(PaintEventArgs e) { if (memDC != IntPtr.Zero) Present(); }

        // ---------------- 메인 루프 ----------------
        bool AppIdle() { Native.MSG m; return !Native.PeekMessage(out m, IntPtr.Zero, 0, 0, 0); }

        void OnIdle(object sender, EventArgs e)
        {
            while (AppIdle() && !IsDisposed)
            {
                double now = clock.Elapsed.TotalSeconds;
                if (now - lastT < 1.0 / 62) { Thread.Sleep(1); continue; }
                double dt = Math.Min(.033, now - lastT); lastT = now;
                if (WindowState == FormWindowState.Minimized) continue;
                EnsureSurface();
                game.Update(slow ? dt * .3 : dt);
                RenderFrame();
                Present();
            }
        }

        void Present()
        {
            IntPtr hdc = Native.GetDC(Handle);
            if (k == 1) Native.BitBlt(hdc, 0, 0, bw, bh, memDC, 0, 0, Native.SRCCOPY);
            else { Native.SetStretchBltMode(hdc, 3); Native.StretchBlt(hdc, 0, 0, bw * k, bh * k, memDC, 0, 0, bw, bh, Native.SRCCOPY); }
            Native.ReleaseDC(Handle, hdc);
        }

        // ---------------- 그리기 ----------------
        void T(string s, Font f, Rectangle r, int c, TextFormatFlags fl)
        {
            TextCmd t = new TextCmd(); t.S = s; t.F = f; t.R = r; t.C = c; t.Fl = fl | TextFormatFlags.NoPadding; texts.Add(t);
        }
        void Btn(Rectangle r, string id, Action click) { HitBox h = new HitBox(); h.R = r; h.Id = id; h.Click = click; hits.Add(h); }
        bool Hover(Rectangle r) { return r.Contains(mouseX, mouseY); }

        void RenderFrame()
        {
            texts.Clear(); icons.Clear(); hits.Clear();
            fb.ResetClip();
            fb.Fill(BENCH);

            DrawPanel();
            DrawHud();
            DrawTools();

            // 무대
            game.Render(fb);
            // 둥근 모서리 다시 그리기 (무대 바깥 모서리)
            CornerMask(stageR, Uu(14));
            if (game.ToastT > 0)
            {
                Size ts = TextRenderer.MeasureText(game.ToastMsg, fBodyB);
                Rectangle tr = new Rectangle(stageR.X + stageR.Width / 2 - ts.Width / 2 - Uu(16), stageR.Y + Uu(14), ts.Width + Uu(32), Uu(34));
                fb.FillRRect(tr.X, tr.Y, tr.Width, tr.Height, tr.Height / 2, 0x5C4D18, Math.Min(1, game.ToastT * 3));
                fb.FillRRect(tr.X + 1, tr.Y + 1, tr.Width - 2, tr.Height - 2, tr.Height / 2, 0x101113, .92 * Math.Min(1, game.ToastT * 3));
                T(game.ToastMsg, fBodyB, tr, TEXT, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            string hint = "1–8 도구 · R 다시 소환 · C 잔해 치우기 · S 슬로모션 · M 소리 · F11 전체화면";
            Size hs = TextRenderer.MeasureText(hint, fSmall);
            Rectangle hr = new Rectangle(stageR.X + Uu(12), stageR.Bottom - Uu(12) - hs.Height - Uu(8), hs.Width + Uu(18), hs.Height + Uu(8));
            fb.FillRRect(hr.X, hr.Y, hr.Width, hr.Height, Uu(6), 0x0A0A0C, .55);
            T(hint, fSmall, hr, 0xB9B4A8, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (slow)
            {
                Rectangle sr = new Rectangle(stageR.Right - Uu(130), stageR.Y + Uu(14), Uu(116), Uu(30));
                fb.FillRRect(sr.X, sr.Y, sr.Width, sr.Height, Uu(15), HAZARD, .95);
                T("슬로모션", fBodyB, sr, INK, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 픽셀 → DIB, 그 위에 글자·아이콘
            Native.GdiFlush();
            Marshal.Copy(fb.Px, 0, bits, bw * bh);
            using (Graphics g = Graphics.FromHdc(memDC))
            {
                foreach (TextCmd t in texts) TextRenderer.DrawText(g, t.S, t.F, t.R, U.Col(t.C), t.Fl);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (IconCmd ic in icons) DrawIcon(g, ic.Tool, ic.R, U.Col(ic.C));
            }
            Native.GdiFlush();
        }

        // 무대 바깥 둥근 모서리를 배경색으로 덮기
        void CornerMask(Rectangle r, int rad)
        {
            fb.ResetClip();
            for (int y = 0; y < rad; y++) for (int x = 0; x < rad; x++)
                {
                    double dx = rad - x - .5, dy = rad - y - .5, d = Math.Sqrt(dx * dx + dy * dy);
                    double a = U.Clamp(d - rad + .5, 0, 1);
                    if (a <= 0) continue;
                    fb.Blend(r.X + x, r.Y + y, BENCH, a); fb.Blend(r.Right - 1 - x, r.Y + y, BENCH, a);
                    fb.Blend(r.X + x, r.Bottom - 1 - y, BENCH, a); fb.Blend(r.Right - 1 - x, r.Bottom - 1 - y, BENCH, a);
                }
        }

        void DrawPanel()
        {
            Rectangle p = panelR;
            fb.Box(p, Uu(14), PANEL, LINE);
            int x = p.X + Uu(16), w = p.Width - Uu(32), y = p.Y + Uu(16);
            T("카메라 소환", fH2, new Rectangle(x, y, w, Uu(28)), TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            y += Uu(40);
            T("브랜드", fLabel, new Rectangle(x, y, w, Uu(16)), MUTED, TextFormatFlags.Left); y += Uu(22);
            int bwid = (w - Uu(6)) / 2;
            for (int i = 0; i < 4; i++)
            {
                BrandDef br = Catalog.Brands[i];
                Rectangle r = new Rectangle(x + (i % 2) * (bwid + Uu(6)), y + (i / 2) * Uu(42), bwid, Uu(36));
                bool sel = game.SelBrand == i;
                fb.Box(r, Uu(9), sel ? SELBG : (Hover(r) ? 0x2E3136 : PANEL2), sel ? HAZARD : LINE);
                fb.Disc(r.X + Uu(16), r.Y + r.Height / 2.0, Uu(4), br.Dot, 1);
                T(br.Name, fBodyB, new Rectangle(r.X + Uu(28), r.Y, r.Width - Uu(30), r.Height), TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                int idx = i;
                Btn(r, "brand", delegate { game.SelBrand = idx; game.SelBody = 0; game.SelLens = 0; });
            }
            y += Uu(42) * 2 + Uu(10);
            BrandDef b = Catalog.Brands[game.SelBrand];
            T("바디", fLabel, new Rectangle(x, y, w, Uu(16)), MUTED, TextFormatFlags.Left); y += Uu(22);
            for (int i = 0; i < 4; i++)
            {
                Rectangle r = new Rectangle(x, y, w, Uu(34));
                OptRow(r, b.Bodies[i].Name, b.Bodies[i].Price, game.SelBody == i);
                int idx = i; Btn(r, "body", delegate { game.SelBody = idx; });
                y += Uu(36);
            }
            y += Uu(8);
            T("렌즈", fLabel, new Rectangle(x, y, w, Uu(16)), MUTED, TextFormatFlags.Left); y += Uu(22);
            for (int i = 0; i < 4; i++)
            {
                Rectangle r = new Rectangle(x, y, w, Uu(42));
                OptRow(r, b.Lenses[i].Name, b.Lenses[i].Price, game.SelLens == i);
                int idx = i; Btn(r, "lens", delegate { game.SelLens = idx; });
                y += Uu(44);
            }
            y += Uu(6);
            for (int xx = x; xx < x + w; xx += Uu(6)) fb.FillRect(xx, y, Uu(3), 1, LINE);
            y += Uu(10);
            T("조합 가격 (게임용 추정)", fSmall, new Rectangle(x, y, w, Uu(20)), MUTED, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            T(U.Won(b.Bodies[game.SelBody].Price + b.Lenses[game.SelLens].Price), fBodyB, new Rectangle(x, y, w, Uu(20)), TEXT, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            y += Uu(30);
            Rectangle sb = new Rectangle(x, y, w, Uu(50));
            fb.FillRRect(sb.X, sb.Y + Uu(3), sb.Width, sb.Height, Uu(10), 0x9C7A12, 1);
            fb.FillRRect(sb.X, sb.Y, sb.Width, sb.Height, Uu(10), Hover(sb) ? 0xF7D04F : HAZARD, 1);
            T("소환하기", fSummon, sb, INK, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Btn(sb, "summon", delegate { game.Summon(); });
            y += Uu(64);
            Rectangle nr = new Rectangle(x, y, w, p.Bottom - Uu(12) - y);
            if (nr.Height > Uu(30))
                T("브랜드와 모델명은 어떤 카메라인지 알려 주는 표기일 뿐이며 각 제조사와 관계가 없습니다. 카메라 그림은 실제 디자인을 옮긴 것이 아니라 단순화한 일러스트이고, 가격은 게임용 대략값입니다.",
                    fSmall, nr, MUTED, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        void OptRow(Rectangle r, string name, int price, bool sel)
        {
            if (sel) fb.Box(r, Uu(8), SELBG, HAZARD);
            else if (Hover(r)) fb.FillRRect(r.X, r.Y, r.Width, r.Height, Uu(8), PANEL2, 1);
            int pw = Uu(92);
            T(name, fBody, new Rectangle(r.X + Uu(10), r.Y, r.Width - pw - Uu(14), r.Height), TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            T(U.Won(price), fKbd, new Rectangle(r.Right - pw - Uu(8), r.Y, pw, r.Height), MUTED, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        void DrawHud()
        {
            Rectangle h = hudR;
            Cam c = game.Cam;
            T("카메라", fTitle, new Rectangle(h.X, h.Y + Uu(4), Uu(90), Uu(32)), TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            Size s1 = TextRenderer.MeasureText("카메라 ", fTitle);
            T("파괴소", fTitle, new Rectangle(h.X + s1.Width - Uu(4), h.Y + Uu(4), Uu(120), Uu(32)), HAZARD, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (c != null)
                T(c.Brand.Name + " " + c.BodyD.Name + " + " + c.Lens.Name, fSmall, new Rectangle(h.X, h.Y + Uu(38), Uu(380), Uu(20)), MUTED, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 버튼
            int bx = h.Right;
            string[] labels = { audio.Muted ? "소리 꺼짐" : "소리 켜짐", slow ? "슬로모션 끄기" : "슬로모션", "잔해 치우기" };
            Action[] acts = { delegate { ToggleMute(); }, delegate { slow = !slow; audio.Slow = slow; }, delegate { game.Clean(); } };
            for (int i = 0; i < 3; i++)
            {
                int bwid = TextRenderer.MeasureText(labels[i], fBodyB).Width + Uu(26);
                bx -= bwid;
                Rectangle r = new Rectangle(bx, h.Y + Uu(4), bwid, Uu(36));
                bool on = (i == 1 && slow) || (i == 0 && audio.Muted);
                fb.Box(r, Uu(9), on ? SELBG : (Hover(r) ? PANEL2 : PANEL), on ? HAZARD : LINE);
                T(labels[i], fBodyB, r, TEXT, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Btn(r, "hudbtn", acts[i]);
                bx -= Uu(6);
            }

            // 통계
            double ratio = game.Ratio, loss = game.CurrentLoss();
            int sw = Uu(150), sx = bx - Uu(12) - sw * 3 - Uu(16);
            if (sx < h.X + Uu(400)) sx = h.X + Uu(400);
            for (int i = 0; i < 3; i++)
            {
                Rectangle r = new Rectangle(sx + i * (sw + Uu(8)), h.Y, sw, h.Height);
                fb.Box(r, Uu(10), PANEL, LINE);
                Rectangle lr = new Rectangle(r.X + Uu(12), r.Y + Uu(6), r.Width - Uu(20), Uu(16));
                Rectangle vr = new Rectangle(r.X + Uu(12), r.Y + Uu(22), r.Width - Uu(20), Uu(22));
                if (i == 0)
                {
                    T("파괴율", fLabel, lr, MUTED, TextFormatFlags.Left);
                    T((ratio * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", fMono, vr, TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    int on = (int)Math.Ceiling(Math.Min(1, ratio / .55) * 10);
                    for (int q = 0; q < 10; q++) fb.FillRect(r.X + Uu(12) + q * Uu(11), r.Y + Uu(50), Uu(9), Uu(5), q < on ? (q >= 7 ? REC : HAZARD) : LINE);
                }
                else if (i == 1)
                {
                    T("이번 피해액", fLabel, lr, MUTED, TextFormatFlags.Left);
                    T(U.Won(loss), fMono, vr, TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    string st = "멀쩡함"; int pc = MUTED, pb = LINE; bool dead = false;
                    if (ratio >= .55) { st = "수리 불가"; dead = true; }
                    else if (ratio >= .3) { st = "대파"; pc = HAZARD; pb = 0x6B5A1D; }
                    else if (ratio >= .1) { st = "파손"; pc = HAZARD; pb = 0x6B5A1D; }
                    else if (ratio >= .01) st = "흠집";
                    Size ps = TextRenderer.MeasureText(st, fKbd);
                    Rectangle pr = new Rectangle(r.X + Uu(12), r.Y + Uu(46), ps.Width + Uu(14), Uu(16));
                    if (dead) fb.FillRRect(pr.X, pr.Y, pr.Width, pr.Height, pr.Height / 2, REC, 1);
                    else fb.Box(pr, pr.Height / 2, PANEL, pb);
                    T(st, fKbd, pr, dead ? 0xFFFFFF : pc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    T("누적 피해액", fLabel, lr, MUTED, TextFormatFlags.Left);
                    T(U.Won(game.TotalPrev + loss), fMono, vr, TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    T("희생된 카메라 " + game.Sacrificed + "대", fKbd, new Rectangle(r.X + Uu(12), r.Y + Uu(46), r.Width - Uu(16), Uu(16)), MUTED, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }
        }

        void DrawTools()
        {
            Rectangle t = toolsR; int n = 8, gap = Uu(6);
            int tw = (t.Width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                Rectangle r = new Rectangle(t.X + i * (tw + gap), t.Y, tw, t.Height);
                bool sel = game.Tool == i;
                if (sel) fb.FillRRect(r.X, r.Y, r.Width, r.Height, Uu(11), HAZARD, 1);
                else fb.Box(r, Uu(11), Hover(r) ? PANEL2 : PANEL, LINE);
                int fg = sel ? INK : TEXT;
                IconCmd ic = new IconCmd(); ic.Tool = i; ic.C = fg; ic.R = new Rectangle(r.X + r.Width / 2 - Uu(13), r.Y + Uu(8), Uu(26), Uu(26)); icons.Add(ic);
                T(Catalog.ToolNames[i], fTool, new Rectangle(r.X, r.Y + Uu(38), r.Width, Uu(20)), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                T((i + 1).ToString(), fKbd, new Rectangle(r.Right - Uu(18), r.Y + Uu(4), Uu(12), Uu(14)), sel ? 0x5D4B0E : MUTED, TextFormatFlags.Right);
                int idx = i; Btn(r, "tool", delegate { game.SetTool(idx); });
            }
        }

        // SVG 아이콘을 24칸 격자 기준으로 옮겨 그림
        void DrawIcon(Graphics g, int tool, Rectangle r, Color c)
        {
            float s = r.Width / 24f;
            GraphicsState st = g.Save();
            g.TranslateTransform(r.X, r.Y); g.ScaleTransform(s, s);
            using (Pen p = new Pen(c, 1.7f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                switch (tool)
                {
                    case 0:
                        g.DrawPolygon(p, new PointF[] { new PointF(13, 3), new PointF(21, 11), new PointF(18, 14), new PointF(10, 6) });
                        g.DrawLines(p, new PointF[] { new PointF(11.5f, 9.5f), new PointF(3, 18), new PointF(6, 21), new PointF(14.5f, 12.5f) });
                        break;
                    case 1:
                        g.DrawRectangle(p, 3, 7, 11, 6);
                        g.DrawLines(p, new PointF[] { new PointF(14, 9), new PointF(18, 9), new PointF(21, 10), new PointF(18, 11), new PointF(14, 11) });
                        g.DrawLines(p, new PointF[] { new PointF(6, 13), new PointF(6, 19), new PointF(10, 19), new PointF(10, 13) });
                        break;
                    case 2:
                        g.DrawEllipse(p, 5, 5, 14, 14); g.DrawEllipse(p, 10, 10, 4, 4);
                        g.DrawLine(p, 12, 2, 12, 5); g.DrawLine(p, 12, 19, 12, 22); g.DrawLine(p, 2, 12, 5, 12); g.DrawLine(p, 19, 12, 22, 12);
                        g.DrawLine(p, 5, 5, 7, 7); g.DrawLine(p, 17, 17, 19, 19); g.DrawLine(p, 5, 19, 7, 17); g.DrawLine(p, 17, 7, 19, 5);
                        break;
                    case 3:
                        using (GraphicsPath gp = new GraphicsPath())
                        {
                            gp.AddBezier(12, 3, 13, 7, 17, 8, 17, 13);
                            gp.AddArc(7, 8, 10, 10, 0, 180);
                            gp.AddBezier(7, 13, 7, 10, 9, 9, 9, 6);
                            gp.AddBezier(9, 6, 10, 7, 11, 8, 11, 10);
                            gp.AddBezier(11, 10, 12, 8, 12, 6, 12, 3);
                            g.DrawPath(p, gp);
                        }
                        break;
                    case 4:
                        g.DrawEllipse(p, 9, 9, 6, 6);
                        g.DrawLine(p, 12, 2, 12, 7); g.DrawLine(p, 12, 17, 12, 22); g.DrawLine(p, 2, 12, 7, 12); g.DrawLine(p, 17, 12, 22, 12);
                        break;
                    case 5:
                        g.DrawEllipse(p, 4, 8, 12, 12); g.DrawLine(p, 14, 9.5f, 17, 6.5f);
                        g.DrawArc(p, 17, 4.5f, 4, 4, 180, 180);
                        break;
                    case 6:
                        g.DrawEllipse(p, 9.5f, 9.5f, 5, 5);
                        g.DrawArc(p, 5, 5, 14, 14, 270, 270);
                        g.DrawArc(p, 8.5f, 8.5f, 7, 7, 0, -270);
                        break;
                    default:
                        g.DrawLine(p, 8, 13, 8, 6); g.DrawLine(p, 11, 11, 11, 4.5f); g.DrawLine(p, 14, 11, 14, 6.5f); g.DrawLine(p, 17, 14, 17, 8);
                        g.DrawArc(p, 5, 9, 12, 12, 0, 180);
                        g.DrawLine(p, 17, 14, 17, 15); g.DrawLine(p, 5, 15, 3.8f, 13); g.DrawLine(p, 8, 13, 6.3f, 12.4f);
                        break;
                }
            }
            g.Restore(st);
        }

        // ---------------- 입력 ----------------
        void ToggleMute() { audio.Muted = !audio.Muted; }

        void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                prevState = WindowState; prevBorder = FormBorderStyle;
                FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Normal; WindowState = FormWindowState.Maximized;
                fullscreen = true;
            }
            else
            {
                FormBorderStyle = prevBorder; WindowState = prevState; fullscreen = false;
            }
        }

        void SetPtr(int cx, int cy)
        {
            mouseX = cx / k; mouseY = cy / k;
            game.PX = (mouseX - game.OX) / game.S; game.PY = (mouseY - game.OY) / game.S;
            bool inStage = stageR.Contains(mouseX, mouseY);
            game.POver = inStage;
            if (inStage && !cursorHidden) { Cursor.Hide(); cursorHidden = true; }
            else if (!inStage && cursorHidden && !game.PDown) { Cursor.Show(); cursorHidden = false; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SetPtr(e.X, e.Y);
            if (e.Button != MouseButtons.Left) return;
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].R.Contains(mouseX, mouseY)) { hits[i].Click(); return; }
            if (stageR.Contains(mouseX, mouseY))
            {
                Capture = true;
                game.PPX = game.PX; game.PPY = game.PY; game.PDown = true;
                game.ToolDown();
            }
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); SetPtr(e.X, e.Y); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            SetPtr(e.X, e.Y);
            if (e.Button == MouseButtons.Left && game.PDown) { game.PDown = false; Capture = false; game.ToolUp(); }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!game.PDown) { game.POver = false; if (cursorHidden) { Cursor.Show(); cursorHidden = false; } }
        }
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (game.PDown) { game.PDown = false; game.ToolUp(); }
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Keys kc = e.KeyCode;
            if (kc >= Keys.D1 && kc <= Keys.D8) game.SetTool(kc - Keys.D1);
            else if (kc >= Keys.NumPad1 && kc <= Keys.NumPad8) game.SetTool(kc - Keys.NumPad1);
            else if (kc == Keys.R) game.Summon();
            else if (kc == Keys.C) game.Clean();
            else if (kc == Keys.S) { slow = !slow; audio.Slow = slow; }
            else if (kc == Keys.M) ToggleMute();
            else if (kc == Keys.F11) ToggleFullscreen();
            else if (kc == Keys.Escape && fullscreen) ToggleFullscreen();
        }
    }
}
