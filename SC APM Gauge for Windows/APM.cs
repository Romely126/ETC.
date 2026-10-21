// =====================================================================
//  APM Meter - 윈도우 업무용 실시간 APM 계기판
//  빌드: build.bat (Windows 내장 .NET Framework 4.x csc.exe 사용, C# 5 문법)
//
//  [투명성 원칙]
//   - 어떤 키를 눌렀는지 기록하지 않습니다. 입력 "시각"만 최근 5초치를 메모리에 보관합니다.
//   - 파일 쓰기, 레지스트리 접근, 네트워크 통신 코드가 전혀 없습니다.
//   - vkCode는 "키를 꾹 누를 때 생기는 자동반복(auto-repeat)"을 걸러내는 용도로만 읽고 즉시 버립니다.
//
//  [조작]
//   - 좌클릭 드래그: 이동
//   - 더블클릭: 종료
// =====================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("APM Meter")]
[assembly: AssemblyDescription("Real-time APM widget. Counts input events only; no logging, no network, no file I/O.")]
[assembly: AssemblyProduct("APM Meter")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ApmMeter
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            // 중복 실행 방지 (계기판이 여러 개 뜨는 것을 막음)
            using (Mutex mutex = new Mutex(true, "Local\\ApmMeter_SingleInstance", out created))
            {
                if (!created) return;
                try { NativeMethods.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ApmForm());
            }
        }
    }

    // ------------------------------------------------------------------
    //  Win32 API 선언
    // ------------------------------------------------------------------
    static class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;

        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCLBUTTONDBLCLK = 0x00A3;
        public const int WM_NCRBUTTONDOWN = 0x00A4;
        public const int WM_NCRBUTTONUP = 0x00A5;
        public const int HTCLIENT = 1;
        public const int HTCAPTION = 2;

        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    }

    // ------------------------------------------------------------------
    //  APM 계산 엔진 (속도계 방식)
    //
    //  자동차 속도계처럼 "지금 이 페이스로 1분 동안 계속하면 몇 번인가"를 보여줌.
    //
    //  1) 최근 WindowSec(5초) 동안의 입력 시각만 보관
    //  2) 현재 페이스 = 창 안의 입력 횟수 ÷ 측정 시간 × 60
    //     - 측정 시간 = "치기 시작한 순간부터 지금까지"(최소 1초, 최대 5초)
    //       → 막 치기 시작해도 바로 반응: 1초에 5번 누르면 곧바로 300
    //       → 계속 치면 최근 5초 평균으로 안정: 300 페이스면 정확히 300
    //  3) 손을 떼면 창에서 입력이 하나씩 빠져나가면서 평균 페이스가 내려감
    //       예) 300으로 치다 멈추면 1초 후 약 240, 3초 후 약 120, 5초 후 0
    //       예) 1초에 5번 치고 멈추면 2초 후 150, 3초 후 100, 약 6초 후 0
    //  4) 화면 숫자는 계산값을 부드럽게 따라가며 최소 1씩 움직임 (튀지 않게)
    // ------------------------------------------------------------------
    sealed class ApmEngine
    {
        const double WindowSec = 10.0;      // 평균을 내는 최근 시간 (5~10초 권장)
        const double MinSpanSec = 1.0;     // 막 치기 시작했을 때의 최소 측정 시간
        const double FollowTau = 0.25;     // 화면 숫자가 따라가는 속도 (작을수록 즉각적)
        const int MaxEvents = 8192;        // 버퍼 상한 (안전장치)

        readonly Queue<long> events = new Queue<long>();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly double freq = Stopwatch.Frequency;
        readonly long windowTicks;
        long sessionStart;                 // 쉬었다가 다시 치기 시작한 시각
        long lastUpdate;
        double shown;

        public ApmEngine() { windowTicks = (long)(WindowSec * freq); }

        public long Now { get { return clock.ElapsedTicks; } }

        public long SecondsToTicks(double sec) { return (long)(sec * freq); }

        public void Hit(long t)
        {
            if (events.Count == 0) sessionStart = t;
            if (events.Count < MaxEvents) events.Enqueue(t);
        }

        public int Update()
        {
            long now = clock.ElapsedTicks;

            // 창 밖으로 나간 입력 제거
            while (events.Count > 0 && now - events.Peek() >= windowTicks) events.Dequeue();

            // 현재 페이스 (분당 환산)
            double target = 0;
            if (events.Count > 0)
            {
                double span = (now - sessionStart) / freq;
                if (span < MinSpanSec) span = MinSpanSec;
                if (span > WindowSec) span = WindowSec;
                target = events.Count / span * 60.0;
            }

            // 화면 숫자는 부드럽게 따라감
            double dt = (lastUpdate == 0) ? 0.05 : (now - lastUpdate) / freq;
            lastUpdate = now;
            if (dt > 0.5) dt = 0.5;
            double diff = target - shown, absDiff = Math.Abs(diff);
            double step = absDiff * (1.0 - Math.Exp(-dt / FollowTau));
            if (step < 1.0) step = Math.Min(1.0, absDiff);
            shown += Math.Sign(diff) * step;
            if (target == 0 && shown < 1.0) shown = 0;

            return (int)Math.Round(shown);
        }
    }

    // ------------------------------------------------------------------
    //  계기판 창
    // ------------------------------------------------------------------
    sealed class ApmForm : Form
    {
        // 색상: 스타크래프트풍 네온 연두 / 고대비 황색
        static readonly Color Lime = Color.FromArgb(57, 255, 20);
        static readonly Color LimeDim = Color.FromArgb(30, 120, 20);
        static readonly Color Amber = Color.FromArgb(255, 214, 0);
        static readonly Color Hot = Color.FromArgb(255, 90, 40);

        readonly ApmEngine engine = new ApmEngine();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly float scale;      // DPI 배율 (100% = 1.0)
        readonly Font bigFont;
        readonly Font smallFont;

        // 훅 델리게이트는 GC 수거를 막기 위해 필드로 보관해야 함
        NativeMethods.HookProc keyboardProc;
        NativeMethods.HookProc mouseProc;
        IntPtr keyboardHook = IntPtr.Zero;
        IntPtr mouseHook = IntPtr.Zero;

        // 자동반복 필터: 키별 눌림 상태와 마지막 이벤트 시각 (키 "내용"은 저장하지 않음)
        readonly bool[] keyDown = new bool[256];
        readonly long[] keyLast = new long[256];
        readonly long stuckResetTicks;

        int shown = -1;
        int peak = 0;
        int tickCount = 0;

        public ApmForm()
        {
            Text = "APM Meter";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.88;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            // 고해상도(125%/150%) 모니터에서도 글자가 잘리지 않도록 DPI 배율 적용
            using (Graphics sg = Graphics.FromHwnd(IntPtr.Zero)) scale = sg.DpiX / 96f;
            bigFont = new Font("Consolas", 40f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            smallFont = new Font("Consolas", 12f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ClientSize = new Size(S(196), S(72));
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(24), wa.Top + S(24));

            stuckResetTicks = engine.SecondsToTicks(1.0);

            timer.Interval = 50;   // 요구사항: 최소 50ms 갱신
            timer.Tick += OnTimerTick;
        }

        int S(int px) { return (int)Math.Round(px * scale); }

        // 작업표시줄·Alt+Tab에 나타나지 않는 위젯 스타일
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
                return cp;
            }
        }

        // 실행 시 업무 창의 포커스를 뺏지 않음
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InstallHooks();
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            RemoveHooks();
            bigFont.Dispose();
            smallFont.Dispose();
            base.OnFormClosed(e);
        }

        // ---------------- 글로벌 입력 훅 ----------------
        void InstallHooks()
        {
            keyboardProc = KeyboardHookCallback;
            mouseProc = MouseHookCallback;
            IntPtr hMod = NativeMethods.GetModuleHandle(null);
            keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, keyboardProc, hMod, 0);
            mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseProc, hMod, 0);
        }

        void RemoveHooks()
        {
            if (keyboardHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(keyboardHook); keyboardHook = IntPtr.Zero; }
            if (mouseHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        }

        // 훅 콜백은 반드시 짧게: 시각만 기록하고 즉시 다음 훅으로 넘김
        IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    int vk = Marshal.ReadInt32(lParam) & 0xFF;
                    long now = engine.Now;
                    // 새로 누른 키만 1타 인정. 꾹 누르고 있을 때의 자동반복은 제외.
                    // (KeyUp을 놓친 경우를 대비해 1초 이상 공백 후 다시 오면 새 입력으로 간주)
                    if (!keyDown[vk] || now - keyLast[vk] > stuckResetTicks)
                        engine.Hit(now);
                    keyDown[vk] = true;
                    keyLast[vk] = now;
                }
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                {
                    int vk = Marshal.ReadInt32(lParam) & 0xFF;
                    keyDown[vk] = false;
                }
            }
            return NativeMethods.CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                // 좌·우 클릭 ButtonDown만 인정 (이동, 휠은 제외)
                if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN)
                    engine.Hit(engine.Now);
            }
            return NativeMethods.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        // ---------------- 갱신 ----------------
        void OnTimerTick(object sender, EventArgs e)
        {
            int v = engine.Update();
            if (v != shown)
            {
                shown = v;
                if (v > peak) peak = v;
                Invalidate();   // 값이 바뀔 때만 다시 그림 → CPU 절약
            }

            // 일부 전체화면 앱·작업표시줄 클릭 후에도 최상단 유지 (약 3초마다)
            if (++tickCount >= 60)
            {
                tickCount = 0;
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        static Color ColorFor(int apm)
        {
            if (apm >= 400) return Hot;
            if (apm >= 250) return Amber;
            return Lime;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            int v = shown < 0 ? 0 : shown;
            Color main = ColorFor(v);

            using (Pen border = new Pen(LimeDim, 1f))
                g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding;
            TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, "APM", smallFont, new Rectangle(S(8), S(6), S(60), S(16)), LimeDim, left);
            TextRenderer.DrawText(g, "PEAK " + peak.ToString().PadLeft(4), smallFont,
                new Rectangle(ClientSize.Width - S(108), S(6), S(100), S(16)), LimeDim, right);

            // 고정폭 폰트 + 4자리 패딩 → 자릿수가 바뀌어도 흔들리지 않음
            string num = v.ToString().PadLeft(4);
            TextRenderer.DrawText(g, num, bigFont,
                new Rectangle(0, S(18), ClientSize.Width - S(8), ClientSize.Height - S(20)), main,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // ---------------- 이동 / 종료 ----------------
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeMethods.WM_NCHITTEST:
                    base.WndProc(ref m);
                    // 창 내부 어디를 잡아도 타이틀바처럼 드래그 이동
                    if (m.Result.ToInt32() == NativeMethods.HTCLIENT)
                        m.Result = (IntPtr)NativeMethods.HTCAPTION;
                    return;

                case NativeMethods.WM_NCLBUTTONDBLCLK:
                    // 더블클릭 → 훅 해제 후 안전 종료
                    Close();
                    return;

                case NativeMethods.WM_NCRBUTTONDOWN:
                case NativeMethods.WM_NCRBUTTONUP:
                    // 우클릭 시 시스템 메뉴가 뜨지 않도록 무시
                    return;
            }
            base.WndProc(ref m);
        }
    }
}
