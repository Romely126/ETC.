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
using System.Globalization;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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
        double windowSec = 5.0;            // 평균을 내는 최근 시간 (설정 창에서 변경)
        double minSpanSec = 2.5;           // 막 치기 시작했을 때의 최소 측정 시간 (설정 창의 "시작 완충")
        const double FollowTau = 0.25;     // 화면 숫자가 따라가는 속도 (작을수록 즉각적)
        const int MaxEvents = 8192;        // 버퍼 상한 (안전장치)

        readonly Queue<long> events = new Queue<long>();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly double freq = Stopwatch.Frequency;
        long windowTicks;
        long sessionStart;                 // 쉬었다가 다시 치기 시작한 시각
        long lastUpdate;
        double shown;

        public ApmEngine() { windowTicks = (long)(windowSec * freq); }

        public double WindowSec
        {
            get { return windowSec; }
            set { windowSec = Math.Max(1.0, Math.Min(30.0, value)); windowTicks = (long)(windowSec * freq); }
        }

        // 쉬었다가 치기 시작할 때 첫 몇 타로 수치가 튀지 않게 하는 완충 시간
        //   1초 → 키 1번에 60, 2.5초 → 키 1번에 24
        public double MinSpanSec
        {
            get { return minSpanSec; }
            set { minSpanSec = Math.Max(0.5, Math.Min(windowSec, value)); }
        }

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
                if (span < minSpanSec) span = minSpanSec;
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
    //  설정 (설정 창에서 바꾸고, APM.ini 파일로 저장·불러오기)
    //  ※ 이 파일에는 화면 설정만 들어감. 키 입력·클릭 같은 사용 기록은 저장하지 않음.
    // ------------------------------------------------------------------
    sealed class Options
    {
        public bool Rank, Led, Messages, Info, Graph, BreakReminder;
        public int OffMinutes, LunchMinutes, BreakMinutes, Opacity, MaxGauge;
        public double WindowSec, StartSec;
        public bool HasPos; public int PosX, PosY;
        public string Theme, PortraitImage;   // 테마 키, 사용자 초상화 이미지 경로
        public int Portrait;                  // 0 끄기, 1 테마 애니메이션, 2 사용자 이미지
        public bool AutoSave;                 // 종료할 때 자동 저장

        public Options() { Reset(); }

        public void Reset()
        {
            Rank = true;           // 등급 문구
            Led = true;            // 입력 LED
            Messages = true;       // 상황 메시지
            Info = true;           // 근무 정보 줄 (퇴근 카운트다운 · 총 명령)
            Graph = false;         // 최근 60초 APM 그래프
            BreakReminder = true;  // 연속 작업 시 휴식 알림
            BreakMinutes = 50;     // 휴식 알림까지 연속 작업 시간 (분)
            LunchMinutes = 12 * 60; // 점심 시각 (분)
            OffMinutes = 18 * 60;  // 퇴근 시각 (분)
            Opacity = 94;          // 투명도 (%)
            MaxGauge = 400;        // 게이지가 가득 차는 APM
            WindowSec = 5.0;       // APM 평균 시간 (초)
            StartSec = 2.5;        // 시작 완충 (초)
            HasPos = false;
            Theme = "classic";     // 테마 (클래식이 기본)
            Portrait = 1;          // 초상화: 테마 애니메이션
            PortraitImage = "";    // 사용자 초상화 이미지 경로
            AutoSave = true;       // 종료할 때 자동 저장
        }

        public void CopyFrom(Options o)
        {
            Rank = o.Rank; Led = o.Led; Messages = o.Messages; Info = o.Info; Graph = o.Graph; BreakReminder = o.BreakReminder;
            OffMinutes = o.OffMinutes; LunchMinutes = o.LunchMinutes; BreakMinutes = o.BreakMinutes; Opacity = o.Opacity; MaxGauge = o.MaxGauge; WindowSec = o.WindowSec; StartSec = o.StartSec;
            HasPos = o.HasPos; PosX = o.PosX; PosY = o.PosY;
            Theme = o.Theme; Portrait = o.Portrait; PortraitImage = o.PortraitImage; AutoSave = o.AutoSave;
        }

        static string B(bool v) { return v ? "1" : "0"; }
        // Options 안에서는 Theme가 문자열 필드라서 클래스는 네임스페이스로 지정
        static string CamTheme(string v) { return ApmMeter.Theme.Get(v.Trim()).Key; }
        static string HM(int m) { return string.Format("{0:00}:{1:00}", m / 60, m % 60); }
        static bool ParseHM(string v, out int minutes)
        {
            minutes = 0; string[] hm = v.Split(':'); int h, m;
            if (hm.Length == 2 && int.TryParse(hm[0], out h) && int.TryParse(hm[1], out m) && h >= 0 && h < 24 && m >= 0 && m < 60) { minutes = h * 60 + m; return true; }
            return false;
        }

        // 메모장으로도 고칠 수 있는 INI 형식. 파일이 없으면 만들고, 있으면 덮어씀.
        public void Save(string path)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; 출근 APM 설정 파일");
            sb.AppendLine("; 메모장으로 직접 고쳐도 됩니다. 1 = 켜기, 0 = 끄기");
            sb.AppendLine("; 화면 설정만 들어 있습니다. 키 입력·클릭 같은 사용 기록은 저장하지 않습니다.");
            sb.AppendLine();
            sb.AppendLine("[디자인]");
            sb.AppendLine("Theme=" + Theme + "      ; classic(클래식) · terran(테란) · zerg(저그) · protoss(프로토스)");
            sb.AppendLine("Portrait=" + Portrait.ToString(ci) + "          ; 초상화 0 = 끄기, 1 = 테마 애니메이션, 2 = 사용자 이미지");
            sb.AppendLine("; 사용자 초상화 이미지 경로 (PNG·JPG·BMP·GIF). 이 줄에는 주석을 붙이지 마세요.");
            sb.AppendLine("PortraitImage=" + (PortraitImage ?? ""));
            sb.AppendLine();
            sb.AppendLine("[표시]");
            sb.AppendLine("Rank=" + B(Rank) + "              ; 등급 문구");
            sb.AppendLine("Led=" + B(Led) + "               ; 입력 LED");
            sb.AppendLine("Messages=" + B(Messages) + "          ; 상황 메시지");
            sb.AppendLine("Info=" + B(Info) + "              ; 근무 정보 줄 (퇴근 카운트다운 · 총 명령)");
            sb.AppendLine("Graph=" + B(Graph) + "             ; APM 그래프 (최근 60초)");
            sb.AppendLine();
            sb.AppendLine("[알림]");
            sb.AppendLine("BreakReminder=" + B(BreakReminder) + "     ; 연속 작업 시 휴식 알림");
            sb.AppendLine("BreakMinutes=" + BreakMinutes.ToString(ci) + "     ; 휴식 알림까지 연속 작업 시간 10~180 (분)");
            sb.AppendLine("LunchTime=" + HM(LunchMinutes) + "     ; 점심 시각 (HH:mm)");
            sb.AppendLine("OffTime=" + HM(OffMinutes) + "       ; 퇴근 시각 (HH:mm)");
            sb.AppendLine();
            sb.AppendLine("[계기판]");
            sb.AppendLine("Opacity=" + Opacity.ToString(ci) + "         ; 투명도 30~100 (%)");
            sb.AppendLine("MaxGauge=" + MaxGauge.ToString(ci) + "       ; 게이지가 가득 차는 APM");
            sb.AppendLine("WindowSec=" + WindowSec.ToString("0.#", ci) + "         ; APM 평균 시간 1~30 (초)");
            sb.AppendLine("StartSec=" + StartSec.ToString("0.#", ci) + "        ; 시작 완충 0.5~10 (초). 클수록 첫 몇 타에 덜 튐");
            sb.AppendLine();
            sb.AppendLine("[저장]");
            sb.AppendLine("AutoSave=" + B(AutoSave) + "          ; 종료할 때 자동 저장");
            if (HasPos)
            {
                sb.AppendLine();
                sb.AppendLine("[위치]");
                sb.AppendLine("PosX=" + PosX.ToString(ci));
                sb.AppendLine("PosY=" + PosY.ToString(ci));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        // 모르는 줄·잘못된 값은 건너뛰고 기본값을 씀
        public static bool TryLoad(string path, Options into, out string error)
        {
            try
            {
                Options t = new Options();
                bool hasX = false, hasY = false;
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1);
                    if (k != "portraitimage") { int cm = v.IndexOf(';'); if (cm >= 0) v = v.Substring(0, cm); }   // 경로에는 ; 가 있을 수 있음
                    v = v.Trim();
                    int iv; double dv;
                    switch (k)
                    {
                        case "theme": t.Theme = CamTheme(v); break;
                        case "portrait": if (int.TryParse(v, out iv)) t.Portrait = Math.Max(0, Math.Min(2, iv)); break;
                        case "portraitimage": t.PortraitImage = v; break;
                        case "autosave": t.AutoSave = v != "0"; break;
                        case "rank": t.Rank = v != "0"; break;
                        case "led": t.Led = v != "0"; break;
                        case "messages": t.Messages = v != "0"; break;
                        case "info": t.Info = v != "0"; break;
                        case "graph": t.Graph = v != "0"; break;
                        case "breakreminder": t.BreakReminder = v != "0"; break;
                        case "offtime": if (ParseHM(v, out iv)) t.OffMinutes = iv; break;
                        case "lunchtime": if (ParseHM(v, out iv)) t.LunchMinutes = iv; break;
                        case "breakminutes": if (int.TryParse(v, out iv)) t.BreakMinutes = Math.Max(10, Math.Min(180, iv)); break;
                        case "opacity": if (int.TryParse(v, out iv)) t.Opacity = Math.Max(30, Math.Min(100, iv)); break;
                        case "maxgauge": if (int.TryParse(v, out iv)) t.MaxGauge = Math.Max(100, Math.Min(2000, iv)); break;
                        case "startsec": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) t.StartSec = Math.Max(0.5, Math.Min(10, dv)); break;
                        case "windowsec": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) t.WindowSec = Math.Max(1, Math.Min(30, dv)); break;
                        case "posx": if (int.TryParse(v, out iv)) { t.PosX = iv; hasX = true; } break;
                        case "posy": if (int.TryParse(v, out iv)) { t.PosY = iv; hasY = true; } break;
                    }
                }
                t.HasPos = hasX && hasY;
                into.CopyFrom(t);
                error = null;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }

    // ------------------------------------------------------------------
    //  테마 (색·테두리 무늬·초상화 종류)
    //  ※ 원작 게임의 그림·로고는 쓰지 않고, 종족 분위기만 참고해 새로 만든 디자인
    // ------------------------------------------------------------------
    sealed class Theme
    {
        public string Key, Name;
        public Color F0, F1, F2, Hi, Hi2, Lo;          // 테두리 금속(위·가운데·아래), 베벨
        public Color RivetC, Sc0, Sc1, ScBorder, Bracket; // 모서리 장식, 화면(가운데·가장자리), 화면 테두리, 괄호
        public Color Lab, Dim, Off;                    // 라벨, 흐린 글자, 꺼진 게이지
        public Color T1, T2, T3;                       // 숫자 색 (보통 / 250+ / 400+)
        public Color LedOn, LedOff, Notice;
        public int Rivet;        // 0 금속 리벳, 1 생체 돌기, 2 보석
        public int Pattern;      // 0 브러시드 금속, 1 경고 줄무늬, 2 핏줄, 3 새김 무늬
        public int ScanAlpha;    // 주사선 진하기
        public int Portrait;     // 0 레이더, 1 통신 헬멧, 2 생체 눈, 3 에너지 결정

        static Color H(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static readonly Theme[] All = new Theme[]
        {
            new Theme { Key = "classic", Name = "클래식", F0 = H(0x2A312F), F1 = H(0x1A201E), F2 = H(0x0D1110), Hi = H(0x66716C), Hi2 = H(0x3B4441), Lo = H(0x030404),
                RivetC = H(0x7D8883), Sc0 = H(0x04160A), Sc1 = H(0x010603), ScBorder = H(0x1C4A24), Bracket = H(0x3A9A3C),
                Lab = H(0x4CC046), Dim = H(0x2F8A30), Off = H(0x0E2A12), T1 = H(0x39FF14), T2 = H(0xFFD600), T3 = H(0xFF5A28),
                LedOn = H(0x9DFF7A), LedOff = H(0x123A16), Notice = H(0xFFC83C), Rivet = 0, Pattern = 0, ScanAlpha = 70, Portrait = 0 },
            new Theme { Key = "terran", Name = "테란", F0 = H(0x4A5056), F1 = H(0x2C3136), F2 = H(0x15181B), Hi = H(0x8A939B), Hi2 = H(0x50585F), Lo = H(0x050607),
                RivetC = H(0x9AA3AA), Sc0 = H(0x081A14), Sc1 = H(0x020806), ScBorder = H(0x2C5A48), Bracket = H(0x5FBF9A),
                Lab = H(0x7FE0B6), Dim = H(0x3F8F70), Off = H(0x10261E), T1 = H(0xB6FFD6), T2 = H(0xFFD23F), T3 = H(0xFF6A3A),
                LedOn = H(0xC8FFE6), LedOff = H(0x14332A), Notice = H(0xFFD23F), Rivet = 0, Pattern = 1, ScanAlpha = 76, Portrait = 1 },
            new Theme { Key = "zerg", Name = "저그", F0 = H(0x4A1E2C), F1 = H(0x2C0F1A), F2 = H(0x14060C), Hi = H(0x7A3A4C), Hi2 = H(0x4A2030), Lo = H(0x050103),
                RivetC = H(0xFF9A3C), Sc0 = H(0x1E0610), Sc1 = H(0x070105), ScBorder = H(0x5A1A30), Bracket = H(0xB04A6A),
                Lab = H(0xD8A03A), Dim = H(0x9A5A3A), Off = H(0x2A0C16), T1 = H(0xC8E83C), T2 = H(0xFFB02A), T3 = H(0xFF4A3A),
                LedOn = H(0xE8FF7A), LedOff = H(0x2A1A0C), Notice = H(0xFFB02A), Rivet = 1, Pattern = 2, ScanAlpha = 56, Portrait = 2 },
            new Theme { Key = "protoss", Name = "프로토스", F0 = H(0xB8954E), F1 = H(0x7A5F2C), F2 = H(0x3E2F14), Hi = H(0xECD28E), Hi2 = H(0xA5844A), Lo = H(0x1A1308),
                RivetC = H(0x5AD2FF), Sc0 = H(0x06163A), Sc1 = H(0x01040E), ScBorder = H(0x2A5A9A), Bracket = H(0x5AB4FF),
                Lab = H(0x8AD8FF), Dim = H(0x4A8AC8), Off = H(0x0C1A36), T1 = H(0x6AD8FF), T2 = H(0xFFF0A0), T3 = H(0xFF8A5A),
                LedOn = H(0xBFF0FF), LedOff = H(0x10284A), Notice = H(0xFFE08A), Rivet = 2, Pattern = 3, ScanAlpha = 50, Portrait = 3 },
        };

        public static Theme Get(string key)
        {
            foreach (Theme t in All) if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) return t;
            return All[0];
        }
    }

    // ------------------------------------------------------------------
    //  초상화 애니메이션 (코드로 그림, 이미지 파일 없음)
    // ------------------------------------------------------------------
    static class Portraits
    {
        static double Hash(int n) { double x = Math.Sin(n * 127.1) * 43758.5453; return x - Math.Floor(x); }
        static Color A(int a, Color c) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        static void Radial(Graphics g, RectangleF fill, float cx, float cy, float r, Color inner, Color outer)
        {
            using (SolidBrush ob = new SolidBrush(outer)) g.FillRectangle(ob, fill);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterPoint = new PointF(cx, cy); pg.CenterColor = inner; pg.SurroundColors = new Color[] { outer };
                    g.FillPath(pg, p);
                }
            }
        }
        static void Glow(Graphics g, float cx, float cy, float r, Color inner)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterPoint = new PointF(cx, cy); pg.CenterColor = inner; pg.SurroundColors = new Color[] { Color.FromArgb(0, inner) };
                    g.FillPath(pg, p);
                }
            }
        }
        // 2차 곡선을 3차 베지어로
        static void Quad(Graphics g, Pen p, float x0, float y0, float qx, float qy, float x1, float y1)
        {
            g.DrawBezier(p, x0, y0, x0 + 2f / 3 * (qx - x0), y0 + 2f / 3 * (qy - y0), x1 + 2f / 3 * (qx - x1), y1 + 2f / 3 * (qy - y1), x1, y1);
        }
        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        static void Caption(Graphics g, string text, RectangleF r, Color c, float s)
        {
            using (Font f = new Font("Consolas", 8f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Far;
                g.DrawString(text, f, b, new RectangleF(r.X, r.Y, r.Width, r.Height - 2 * s), sf);
            }
        }

        public static void Draw(Graphics g, Theme th, RectangleF r, double t, float s)
        {
            GraphicsState st = g.Save();
            g.SetClip(r);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            switch (th.Portrait)
            {
                case 0: Radar(g, th, r, t, s); break;
                case 1: Comm(g, th, r, t, s); break;
                case 2: Eye(g, th, r, t, s); break;
                default: Crystal(g, th, r, t, s); break;
            }
            g.Restore(st);
        }

        // 클래식: 레이더 스캔
        static void Radar(Graphics g, Theme th, RectangleF r, double t, float s)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(2, 12, 5))) g.FillRectangle(bg, r);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 - 4 * s, R = Math.Min(r.Width, r.Height) * .42f;
            using (Pen ring = new Pen(Color.FromArgb(90, 60, 180, 60), 1f))
            {
                foreach (float k in new float[] { 1f, .66f, .33f }) g.DrawEllipse(ring, cx - R * k, cy - R * k, R * k * 2, R * k * 2);
                g.DrawLine(ring, cx - R, cy, cx + R, cy); g.DrawLine(ring, cx, cy - R, cx, cy + R);
            }
            double a = t * 2.2;
            for (int i = 0; i < 14; i++)
            {
                double aa = a - i * .07;
                using (SolidBrush w = new SolidBrush(A((int)(72 * (1 - i / 14.0)), th.T1)))
                    g.FillPie(w, cx - R, cy - R, R * 2, R * 2, (float)((aa - .07) * 180 / Math.PI), (float)(.07 * 180 / Math.PI));
            }
            using (Pen sweep = new Pen(th.LedOn, 1f)) g.DrawLine(sweep, cx, cy, cx + (float)Math.Cos(a) * R, cy + (float)Math.Sin(a) * R);
            double[][] blips = { new double[] { .8, .6 }, new double[] { 2.6, .4 }, new double[] { 4.1, .8 }, new double[] { 5.4, .3 } };
            foreach (double[] bp in blips)
            {
                double da = ((a - bp[0]) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);
                double br = Math.Max(0, 1 - da / 2.5);
                float bx = cx + (float)(Math.Cos(bp[0]) * R * bp[1]), by = cy + (float)(Math.Sin(bp[0]) * R * bp[1]), rr = 1.8f * s;
                using (SolidBrush b = new SolidBrush(A((int)(255 * (.15 + .85 * br)), th.LedOn))) g.FillEllipse(b, bx - rr, by - rr, rr * 2, rr * 2);
            }
            Caption(g, "SCAN", r, th.Bracket, s);
        }

        // 테란: 통신 화면 속 헬멧 실루엣 (일반적인 SF 헬멧)
        static void Comm(Graphics g, Theme th, RectangleF r, double t, float s)
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.FromArgb(12, 42, 36), Color.FromArgb(4, 16, 12), 90f))
                g.FillRectangle(bg, r);
            double ph = t % 4.5; bool roll = ph < .25;
            float oy = (roll ? (float)(ph * 40 - 5) : 0) * s + (float)Math.Sin(t * 1.3) * 1.2f * s;
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 + oy, w = r.Width, h = r.Height;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 74, 80))) g.FillEllipse(sh, cx - w * .48f, r.Bottom + 6 * s + oy - h * .28f, w * .96f, h * .56f);
            using (SolidBrush strap = new SolidBrush(Color.FromArgb(85, 101, 108))) g.FillRectangle(strap, cx - w * .33f, r.Y + h * .78f + oy, w * .66f, 3 * s);
            RectangleF helm = new RectangleF(cx - w * .3f, cy - 4 * s - h * .3f, w * .6f, h * .6f);
            using (GraphicsPath hp = new GraphicsPath())
            {
                hp.AddEllipse(helm);
                using (PathGradientBrush pg = new PathGradientBrush(hp))
                {
                    pg.CenterPoint = new PointF(cx - 6 * s, cy - 12 * s); pg.CenterColor = Color.FromArgb(154, 168, 174); pg.SurroundColors = new Color[] { Color.FromArgb(58, 70, 76) };
                    g.FillPath(pg, hp);
                }
            }
            RectangleF vis = new RectangleF(cx - w * .24f, cy - 10 * s, w * .48f, 11 * s);
            using (GraphicsPath vp = RoundRect(vis, 5 * s))
            {
                using (LinearGradientBrush vb = new LinearGradientBrush(new RectangleF(vis.X - 1, vis.Y, vis.Width + 2, vis.Height), Color.Black, Color.Black, 0f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { Color.FromArgb(26, 58, 48), Color.FromArgb(63, 191, 143), Color.FromArgb(18, 48, 38) };
                    cb.Positions = new float[] { 0f, .5f, 1f };
                    vb.InterpolationColors = cb;
                    g.FillPath(vb, vp);
                }
                // 바이저 반사광 (바이저 안에서만)
                GraphicsState vs = g.Save();
                g.SetClip(vp, CombineMode.Intersect);
                float gx = vis.X + (float)((t * 18 * s) % (w * .8));
                using (SolidBrush gl = new SolidBrush(Color.FromArgb(140, 220, 255, 240)))
                    g.FillPolygon(gl, new PointF[] { new PointF(gx, vis.Y), new PointF(gx + 4 * s, vis.Y), new PointF(gx - 1 * s, vis.Bottom), new PointF(gx - 5 * s, vis.Bottom) });
                g.Restore(vs);
            }
            using (SolidBrush mouth = new SolidBrush(Color.FromArgb(42, 52, 56))) g.FillRectangle(mouth, cx - 3 * s, cy + 4 * s, 6 * s, 5 * s);
            int lines = roll ? 30 : 6, fr = (int)(t * 12);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(roll ? 64 : 20, 180, 255, 220)))
                for (int i = 0; i < lines; i++) g.FillRectangle(nb, r.X, r.Y + (float)Hash(i + fr * 31) * r.Height, r.Width, Math.Max(1f, s));
            Caption(g, "COMM", r, th.Bracket, s);
        }

        // 저그: 맥동하는 생체 눈 (일반적인 외계 생물 눈)
        static void Eye(Graphics g, Theme th, RectangleF r, double t, float s)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 - 4 * s, w = r.Width, h = r.Height;
            Radial(g, r, cx, cy + 4 * s, w * .8f, Color.FromArgb(58, 12, 26), Color.FromArgb(14, 3, 6));
            using (Pen ten = new Pen(Color.FromArgb(90, 26, 42), 3 * s))
            {
                ten.StartCap = LineCap.Round; ten.EndCap = LineCap.Round;
                for (int i = 0; i < 4; i++)
                {
                    float bx = r.X + 8 * s + i * 14 * s;
                    Quad(g, ten, bx, r.Bottom, bx + (float)Math.Sin(t * 1.5 + i) * 6 * s, r.Bottom - 14 * s, bx + (float)Math.Sin(t * 1.2 + i * 2) * 8 * s, r.Bottom - 24 * s);
                }
            }
            float R = w * .3f * (float)(1 + .05 * Math.Sin(t * 3));
            using (Pen vein = new Pen(Color.FromArgb(150, 160, 40, 70), 1f))
                for (int i = 0; i < 7; i++)
                {
                    double a = i / 7.0 * Math.PI * 2 + .3;
                    Quad(g, vein, cx + (float)Math.Cos(a) * R * .9f, cy + (float)Math.Sin(a) * R * .9f,
                        cx + (float)Math.Cos(a + .3) * R * 1.4f, cy + (float)Math.Sin(a + .3) * R * 1.4f,
                        cx + (float)Math.Cos(a) * R * 1.9f, cy + (float)Math.Sin(a) * R * 1.9f);
                }
            using (GraphicsPath eye = new GraphicsPath())
            {
                eye.AddEllipse(cx - R, cy - R * .9f, R * 2, R * 1.8f);
                using (PathGradientBrush pg = new PathGradientBrush(eye))
                {
                    pg.CenterPoint = new PointF(cx - 5 * s, cy - 10 * s); pg.CenterColor = Color.FromArgb(176, 64, 90); pg.SurroundColors = new Color[] { Color.FromArgb(74, 16, 32) };
                    g.FillPath(pg, eye);
                }
                float look = (float)Math.Sin(t * .7) * 4 * s;
                using (GraphicsPath iris = new GraphicsPath())
                {
                    iris.AddEllipse(cx + look - R * .55f, cy - R * .5f, R * 1.1f, R);
                    using (PathGradientBrush ig = new PathGradientBrush(iris))
                    {
                        ig.CenterPoint = new PointF(cx + look, cy); ig.CenterColor = Color.FromArgb(255, 226, 90); ig.SurroundColors = new Color[] { Color.FromArgb(200, 86, 10) };
                        g.FillPath(ig, iris);
                    }
                }
                using (SolidBrush pupil = new SolidBrush(Color.FromArgb(18, 2, 6))) g.FillEllipse(pupil, cx + look - R * .1f, cy - R * .42f, R * .2f, R * .84f);
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(90, 255, 220, 180))) g.FillEllipse(shine, cx - R * .35f - 2 * s, cy - R * .35f - 2 * s, 4 * s, 4 * s);
                // 깜빡임: 눈 모양 안에서 위아래 눈꺼풀이 닫힘
                double bt = t % 5.2;
                float close = bt < .25 ? (float)Math.Sin(bt / .25 * Math.PI) : 0f;
                if (close > 0)
                {
                    GraphicsState es = g.Save();
                    g.SetClip(eye, CombineMode.Intersect);
                    using (SolidBrush lid = new SolidBrush(Color.FromArgb(90, 20, 40)))
                    {
                        g.FillEllipse(lid, cx - R * 1.2f, cy - R * .9f - R * 1.8f + R * 1.8f * close, R * 2.4f, R * 1.8f);
                        g.FillEllipse(lid, cx - R * 1.2f, cy + R * .9f - R * 1.8f * close, R * 2.4f, R * 1.8f);
                    }
                    g.Restore(es);
                }
            }
        }

        // 프로토스: 회전하는 에너지 결정
        static void Crystal(Graphics g, Theme th, RectangleF r, double t, float s)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 - 2 * s, w = r.Width, h = r.Height;
            Radial(g, r, cx, cy + 2 * s, w * .8f, Color.FromArgb(12, 36, 86), Color.FromArgb(2, 6, 22));
            float ph = (float)((t % 2.2) / 2.2), rr = 6 * s + ph * w * .45f;
            using (Pen ring = new Pen(A((int)(150 * (1 - ph)), Color.FromArgb(90, 180, 255)), 1.5f * s)) g.DrawEllipse(ring, cx - rr, cy - rr * .45f, rr * 2, rr * .9f);
            Glow(g, cx, cy, w * .38f, Color.FromArgb((int)(255 * (.45 + .15 * Math.Sin(t * 3))), 120, 210, 255));
            float k = (float)Math.Abs(Math.Cos(t * 1.4)), hw = w * .16f, hh = h * .28f, fl = (float)Math.Sin(t * 2) * 2 * s;
            float L = cx - hw * k, Rr = cx + hw * k, top = cy - hh + fl, bot = cy + hh + fl, mid = cy + fl, midX = cx + hw * .6f * (float)Math.Sin(t * 1.4);
            using (SolidBrush lit = new SolidBrush(Color.FromArgb(191, 239, 255)))
                g.FillPolygon(lit, new PointF[] { new PointF(cx, top), new PointF(Rr, mid), new PointF(cx, bot), new PointF(midX, mid) });
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(74, 168, 232)))
                g.FillPolygon(dark, new PointF[] { new PointF(cx, top), new PointF(L, mid), new PointF(cx, bot), new PointF(midX, mid) });
            using (Pen edge = new Pen(Color.FromArgb(205, 230, 250, 255), 1f))
                g.DrawLines(edge, new PointF[] { new PointF(cx, top), new PointF(midX, mid), new PointF(cx, bot) });
            for (int i = 0; i < 3; i++)
            {
                double a = t * 1.8 + i * 2.1;
                float px = cx + (float)Math.Cos(a) * w * .36f, py = cy + (float)Math.Sin(a) * w * .14f, pr = 1.6f * s;
                using (SolidBrush pb = new SolidBrush(Math.Sin(a) > 0 ? Color.FromArgb(232, 251, 255) : Color.FromArgb(128, 160, 220, 255)))
                    g.FillEllipse(pb, px - pr, py - pr, pr * 2, pr * 2);
            }
            Caption(g, "PSI", r, th.Bracket, s);
        }
    }

    // ------------------------------------------------------------------
    //  사용자 초상화 이미지 (PNG·JPG·BMP·GIF, 움직이는 GIF는 프레임 재생)
    //  파일을 메모리로 읽어 두므로 원본 파일을 잠그지 않음
    // ------------------------------------------------------------------
    sealed class PortraitImage : IDisposable
    {
        readonly MemoryStream ms; public readonly Image Img;
        readonly FrameDimension dim; readonly int frames; readonly int[] delays;
        int idx; long nextTick;
        public readonly string FilePath;

        public PortraitImage(string path)
        {
            FilePath = path;
            ms = new MemoryStream(File.ReadAllBytes(path));
            Img = Image.FromStream(ms);
            dim = new FrameDimension(Img.FrameDimensionsList[0]);
            frames = Math.Max(1, Img.GetFrameCount(dim));
            delays = new int[frames];
            for (int i = 0; i < frames; i++) delays[i] = 100;
            if (frames > 1)
            {
                try
                {
                    PropertyItem pi = Img.GetPropertyItem(0x5100);   // 프레임 지연 (1/100초)
                    for (int i = 0; i < frames && i * 4 + 3 < pi.Value.Length; i++)
                    {
                        int d = BitConverter.ToInt32(pi.Value, i * 4) * 10;
                        delays[i] = d < 20 ? 100 : d;
                    }
                }
                catch { }
            }
        }

        public bool Animated { get { return frames > 1; } }

        // 다음 프레임으로 넘어갔으면 true
        public bool Advance(long now, double ticksPerMs)
        {
            if (frames <= 1) return false;
            if (nextTick == 0) { nextTick = now + (long)(delays[0] * ticksPerMs); return false; }
            if (now < nextTick) return false;
            idx = (idx + 1) % frames;
            Img.SelectActiveFrame(dim, idx);
            nextTick = now + (long)(delays[idx] * ticksPerMs);
            return true;
        }

        // 칸을 꽉 채우도록 가운데를 잘라서 그림
        public void Draw(Graphics g, RectangleF r)
        {
            float ir = (float)Img.Width / Img.Height, rr = r.Width / r.Height;
            RectangleF src = ir > rr
                ? new RectangleF((Img.Width - Img.Height * rr) / 2f, 0, Img.Height * rr, Img.Height)
                : new RectangleF(0, (Img.Height - Img.Width / rr) / 2f, Img.Width, Img.Width / rr);
            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(Img, r, src, GraphicsUnit.Pixel);
            g.InterpolationMode = old;
        }

        public void Dispose() { Img.Dispose(); ms.Dispose(); }
    }

    // ------------------------------------------------------------------
    //  계기판 창
    // ------------------------------------------------------------------
    sealed class ApmForm : Form
    {
        // 색상은 현재 테마에서 가져옴 (페이스별 경고색: 보통 → 250+ → 400+)
        Theme th = Theme.All[0];
        Color Lime { get { return th.T1; } }
        Color Amber { get { return th.T2; } }
        Color Hot { get { return th.T3; } }
        Color Label { get { return th.Lab; } }
        Color LabelDim { get { return th.Dim; } }
        Color SegOff { get { return th.Off; } }
        Color Notice { get { return th.Notice; } }

        // 초상화
        const int PortW = 70;                  // 초상화 칸이 켜질 때 늘어나는 폭
        PortraitImage custom;                  // 사용자 이미지 (없으면 null)
        public string PortraitError;           // 사용자 이미지를 열지 못한 이유
        int animTick;
        bool PortraitOn { get { return opt.Portrait == 1 || (opt.Portrait == 2 && custom != null); } }
        RectangleF PortraitRect { get { return new RectangleF(F(11), F(9), F(62), F(BaseH - 18)); } }

        const int BaseW = 232, BaseH = 96;     // 100% 배율 기준 크기 (기본 모양)
        const int InfoH = 16, GraphH = 28;     // 근무 정보 줄 · 그래프 줄이 켜질 때 늘어나는 높이
        static readonly int[] Milestones = { 1000, 5000, 10000, 20000, 30000, 50000, 100000 };

        readonly Options opt = new Options();
        readonly ApmEngine engine = new ApmEngine();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly float scale;                  // DPI 배율 (100% = 1.0)
        readonly Font numFont, labelFont, rankFont, infoFont;
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly string settingsPath;          // exe와 같은 폴더의 APM.ini
        string startupStatus;                  // 설정 창에 보여 줄 시작 시 상태
        SettingsForm settingsForm;
        Bitmap background;                     // 금속 테두리·화면·주사선 (크기가 바뀔 때만 다시 그림)
        Bitmap glowBmp;                        // 숫자 빛 번짐용 축소 버퍼

        // 훅 델리게이트는 GC 수거를 막기 위해 필드로 보관해야 함
        NativeMethods.HookProc keyboardProc;
        NativeMethods.HookProc mouseProc;
        IntPtr keyboardHook = IntPtr.Zero;
        IntPtr mouseHook = IntPtr.Zero;

        // 자동반복 필터: 키별 눌림 상태와 마지막 이벤트 시각 (키 "내용"은 저장하지 않음)
        readonly bool[] keyDown = new bool[256];
        readonly long[] keyLast = new long[256];
        readonly long stuckResetTicks, ledTicks, flashTicks, secTicks;

        int shown = -1;
        int peak = 0;
        int tickCount = 0;
        long lastInput = long.MinValue / 2;    // 입력 LED · 자리 비움 판단용 (마지막 입력 시각)
        long peakFlashUntil = long.MinValue / 2;
        bool ledOn, flashOn, peakMsgShown;

        // 재미 요소 상태 (모두 메모리에만 있음)
        int totalActions;                      // 오늘 누른 횟수 (어떤 키인지는 모름)
        int nextMilestone;
        string msgText; Color msgColor; long msgUntil = long.MinValue / 2;
        long hotSince = -1; bool hotFired;     // 250 이상 유지 시간
        long activeSince = -1; bool breakFired; // 쉬지 않고 일한 시간
        bool wasIdle;
        DateTime lastClockCheck = DateTime.MinValue; DateTime firedDay = DateTime.MinValue;
        bool lunch10, lunch0, off10, off0;
        readonly double[] history = new double[60]; int histPos; double histSum; int histN; long lastSample;
        string infoLeft = "", infoRight = "";

        public ApmForm()
        {
            Text = "APM Meter";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            // 고해상도(125%/150%) 모니터에서도 글자가 잘리지 않도록 DPI 배율 적용
            using (Graphics sg = Graphics.FromHwnd(IntPtr.Zero)) scale = sg.DpiX / 96f;
            numFont = new Font("Consolas", 38f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            labelFont = new Font("Consolas", 11f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            rankFont = new Font("Malgun Gothic", 11f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            infoFont = new Font("Malgun Gothic", 10.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);

            // 실행 시 exe와 같은 폴더의 APM.ini를 찾아서 적용
            settingsPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "APM.ini");
            if (File.Exists(settingsPath))
            {
                string err;
                if (Options.TryLoad(settingsPath, opt, out err)) startupStatus = "실행할 때 불러온 설정:\n" + settingsPath;
                else startupStatus = "설정 파일을 읽지 못해 기본값을 씁니다: " + err;
            }
            else startupStatus = "설정 파일이 없어 기본값을 씁니다.\n[설정 저장]을 누르면 여기에 만들어집니다:\n" + settingsPath;

            th = Theme.Get(opt.Theme);
            LoadCustomPortrait();
            ClientSize = new Size(S(TargetWidth()), S(TargetHeight()));
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(24), wa.Top + S(24));
            ApplyPosition();
            Opacity = opt.Opacity / 100.0;
            engine.WindowSec = opt.WindowSec;
            engine.MinSpanSec = opt.StartSec;

            stuckResetTicks = engine.SecondsToTicks(1.0);
            ledTicks = engine.SecondsToTicks(0.09);
            flashTicks = engine.SecondsToTicks(0.8);
            secTicks = engine.SecondsToTicks(1.0);
            nextMilestone = 0;

            BuildMenu();
            BuildBackground();
            timer.Interval = 50;   // 요구사항: 최소 50ms 갱신
            timer.Tick += OnTimerTick;
        }

        int S(double px) { return (int)Math.Round(px * scale); }
        float F(double px) { return (float)(px * scale); }
        int TargetHeight() { return BaseH + (opt.Info ? InfoH : 0) + (opt.Graph ? GraphH : 0); }
        int TargetWidth() { return BaseW + (PortraitOn ? PortW : 0); }

        // 화면 영역 (테두리 안쪽의 녹색 모니터)
        RectangleF Screen0
        {
            get
            {
                float off = PortraitOn ? F(PortW) : 0f;
                return new RectangleF(F(11) + off, F(9), ClientSize.Width - F(22) - off, ClientSize.Height - F(18));
            }
        }

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
            // 종료할 때 자동 저장 (실패해도 조용히 넘어감)
            if (opt.AutoSave) { string m; SaveSettings(out m); }
            timer.Stop();
            RemoveHooks();
            numFont.Dispose(); labelFont.Dispose(); rankFont.Dispose(); infoFont.Dispose();
            if (background != null) background.Dispose();
            if (glowBmp != null) glowBmp.Dispose();
            menu.Dispose();
            if (custom != null) custom.Dispose();
            base.OnFormClosed(e);
        }

        // ---------------- 우클릭 메뉴: 설정 · 종료 ----------------
        void BuildMenu()
        {
            ToolStripMenuItem set = new ToolStripMenuItem("설정…");
            set.Font = new Font(set.Font, FontStyle.Bold);
            set.Click += delegate { OpenSettings(); };
            menu.Items.Add(set);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("종료");
            exit.Click += delegate { BeginInvoke(new MethodInvoker(Close)); };
            menu.Items.Add(exit);
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this, opt, startupStatus);
                settingsForm.Show(this);
            }
            else settingsForm.Activate();
        }

        // ---------------- 설정 적용 · 저장 · 불러오기 ----------------
        public void ApplyOptions()
        {
            Opacity = opt.Opacity / 100.0;
            engine.WindowSec = opt.WindowSec;
            engine.MinSpanSec = opt.StartSec;
            off10 = false; off0 = false; lunch10 = false; lunch0 = false;
            th = Theme.Get(opt.Theme);
            LoadCustomPortrait();
            Relayout();   // 테마·크기가 바뀌었을 수 있으므로 배경을 다시 그림
            UpdateInfo(false);
            Invalidate();
        }

        // 사용자 초상화 이미지: 경로가 바뀌었을 때만 다시 읽음
        void LoadCustomPortrait()
        {
            string path = opt.Portrait == 2 ? (opt.PortraitImage ?? "") : "";
            if (custom != null && path.Length > 0 && string.Equals(custom.FilePath, path, StringComparison.OrdinalIgnoreCase)) return;
            if (custom != null) { custom.Dispose(); custom = null; }
            PortraitError = null;
            if (path.Length == 0) return;
            try { custom = new PortraitImage(path); }
            catch (Exception ex) { custom = null; PortraitError = ex.Message; }
        }

        void ApplyPosition()
        {
            if (!opt.HasPos) return;
            foreach (Screen sc in Screen.AllScreens)
                if (sc.WorkingArea.Contains(opt.PosX + S(20), opt.PosY + S(20))) { Location = new Point(opt.PosX, opt.PosY); return; }
        }

        // exe 폴더의 APM.ini에 저장. 없으면 만들고, 있으면 덮어씀. 현재 위치도 함께 저장.
        public bool SaveSettings(out string message)
        {
            try
            {
                opt.HasPos = true; opt.PosX = Left; opt.PosY = Top;
                bool existed = File.Exists(settingsPath);
                opt.Save(settingsPath);
                message = (existed ? "설정 파일을 덮어썼습니다:\n" : "설정 파일을 새로 만들었습니다:\n") + settingsPath;
                return true;
            }
            catch (Exception ex)
            {
                message = "저장하지 못했습니다: " + ex.Message + "\nexe가 쓰기 금지된 폴더(예: Program Files)에 있다면 다른 폴더로 옮겨 주세요.";
                return false;
            }
        }

        public bool LoadSettings(string path, out string message)
        {
            string err;
            if (!Options.TryLoad(path, opt, out err)) { message = "불러오지 못했습니다: " + err; return false; }
            ApplyOptions();
            ApplyPosition();
            message = "불러와서 적용했습니다:\n" + path + "\n다음 실행 때도 쓰려면 [설정 저장]을 누르세요.";
            return true;
        }

        public void ResetSettings()
        {
            opt.Reset();
            ApplyOptions();
        }

        void Relayout()
        {
            // 폭이 바뀌면(초상화 켜고 끔) 오른쪽 끝을 기준으로 맞춤 → 화면 밖으로 밀려나지 않게
            int oldRight = Right, oldW = Width;
            ClientSize = new Size(S(TargetWidth()), S(TargetHeight()));
            if (IsHandleCreated && Width != oldW) Left = oldRight - Width;
            if (background != null) background.Dispose();
            if (glowBmp != null) glowBmp.Dispose();
            BuildBackground();
            Invalidate();
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
                    {
                        engine.Hit(now);
                        lastInput = now;
                        totalActions++;
                    }
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
                {
                    long now = engine.Now;
                    engine.Hit(now);
                    lastInput = now;
                    totalActions++;
                }
            }
            return NativeMethods.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        // ---------------- 갱신 ----------------
        void ShowMessage(string text, Color color, double seconds)
        {
            if (!opt.Messages) return;
            msgText = text; msgColor = color; msgUntil = engine.Now + engine.SecondsToTicks(seconds);
            Invalidate();
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            int v = engine.Update();
            long now = engine.Now;
            bool dirty = false;

            if (v != shown)
            {
                shown = v;
                if (v > peak)
                {
                    // 의미 있는 기록(30 이상)을 넘어설 때만 PEAK 반짝임
                    if (peak >= 30) peakFlashUntil = now + flashTicks;
                    // 기록을 넘어서는 순간 한 번만 알림 (크게 떨어졌다가 다시 넘으면 또 알림)
                    if (peak >= 100 && !peakMsgShown) { peakMsgShown = true; ShowMessage("최고 기록 갱신!", Notice, 2.5); }
                    peak = v;
                }
                else if (v < peak * .7) peakMsgShown = false;
                dirty = true;
            }

            bool led = opt.Led && now - lastInput < ledTicks;
            bool flash = now < peakFlashUntil;
            if (led != ledOn || flash != flashOn) { ledOn = led; flashOn = flash; dirty = true; }

            CheckFunEvents(now);

            // 1초마다: 그래프 표본, 근무 정보 줄
            histSum += v; histN++;
            if (now - lastSample >= secTicks)
            {
                lastSample = now;
                history[histPos] = histN > 0 ? histSum / histN : 0; histPos = (histPos + 1) % history.Length;
                histSum = 0; histN = 0;
                if (opt.Graph) dirty = true;
                if (UpdateInfo(false)) dirty = true;
            }
            if (msgText != null && now >= msgUntil) { msgText = null; dirty = true; }

            // 초상화: 테마 애니메이션은 초당 10장, GIF는 파일에 적힌 속도대로
            if (PortraitOn && !dirty)
            {
                bool step = false;
                if (opt.Portrait == 1) step = (++animTick % 2) == 0;
                else if (custom != null) step = custom.Advance(now, secTicks / 1000.0);
                if (step) Invalidate(Rectangle.Ceiling(PortraitRect));
            }

            if (dirty) Invalidate();   // 바뀐 게 있을 때만 다시 그림 → CPU 절약

            // 일부 전체화면 앱·작업표시줄 클릭 후에도 최상단 유지 (약 3초마다)
            if (++tickCount >= 60)
            {
                tickCount = 0;
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        // 근무 정보 줄 글자 갱신. 바뀌었으면 true
        bool UpdateInfo(bool force)
        {
            DateTime n = DateTime.Now;
            DateTime off = n.Date.AddMinutes(opt.OffMinutes);
            string left;
            if (n < off) { TimeSpan r = off - n; left = string.Format("퇴근까지 {0:00}:{1:00}:{2:00}", (int)r.TotalHours, r.Minutes, r.Seconds); }
            else { TimeSpan r = n - off; left = string.Format("야근 +{0:00}:{1:00}:{2:00}", (int)r.TotalHours, r.Minutes, r.Seconds); }
            string right = "명령 " + totalActions.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            bool changed = left != infoLeft || right != infoRight;
            infoLeft = left; infoRight = right;
            if (force) Invalidate();
            return changed && opt.Info;
        }

        // 상황 메시지 판정
        void CheckFunEvents(long now)
        {
            double idleSec = (now - lastInput) / (double)secTicks;

            // 자리 비움 → 복귀
            if (idleSec >= 600) wasIdle = true;
            else if (wasIdle && idleSec < 1) { wasIdle = false; ShowMessage("사령관 복귀", Label, 3); }

            // 쉬지 않고 일한 시간 (5분 이상 멈추면 초기화)
            if (idleSec >= 300) { activeSince = -1; breakFired = false; }
            else if (activeSince < 0 && idleSec < 1) activeSince = now;
            if (opt.BreakReminder && activeSince >= 0 && !breakFired && (now - activeSince) / (double)secTicks >= opt.BreakMinutes * 60)
            {
                breakFired = true;
                ShowMessage(opt.BreakMinutes + "분째 교전 중 · 휴식!", Notice, 8);
            }

            // 250 이상 10초 유지
            if (shown >= 250)
            {
                if (hotSince < 0) { hotSince = now; hotFired = false; }
                else if (!hotFired && (now - hotSince) / (double)secTicks >= 10) { hotFired = true; ShowMessage("컨트롤 폭발 중", Hot, 3); }
            }
            else hotSince = -1;

            // 총 명령 마일스톤
            if (nextMilestone < Milestones.Length && totalActions >= Milestones[nextMilestone])
            {
                ShowMessage("명령 " + Milestones[nextMilestone].ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "회 달성", Label, 3);
                while (nextMilestone < Milestones.Length && totalActions >= Milestones[nextMilestone]) nextMilestone++;
            }

            // 시계 기반 알림 (1초에 한 번만 확인)
            DateTime n = DateTime.Now;
            if ((n - lastClockCheck).TotalSeconds < 1) return;
            lastClockCheck = n;
            if (n.Date != firedDay) { firedDay = n.Date; lunch10 = lunch0 = off10 = off0 = false; totalActions = 0; nextMilestone = 0; }
            int m = n.Hour * 60 + n.Minute;
            if (!lunch10 && m == opt.LunchMinutes - 10) { lunch10 = true; ShowMessage("보급 10분 전 · 점심", Notice, 5); }
            if (!lunch0 && m == opt.LunchMinutes) { lunch0 = true; ShowMessage("보급 시간 · 점심!", Notice, 8); }
            if (!off10 && m == opt.OffMinutes - 10) { off10 = true; ShowMessage("철수 10분 전", Notice, 6); }
            if (!off0 && m == opt.OffMinutes) { off0 = true; ShowMessage("GG · 퇴근하세요", Lime, 10); }
        }

        Color ColorFor(int apm)
        {
            if (apm >= 400) return Hot;
            if (apm >= 250) return Amber;
            return Lime;
        }

        // 페이스별 한마디 (가볍게 재미로)
        static string RankFor(int apm)
        {
            if (apm == 0) return "대기";
            if (apm < 60) return "워밍업";
            if (apm < 150) return "업무 중";
            if (apm < 250) return "집중 모드";
            if (apm < 350) return "손이 안 보임";
            return "프로게이머";
        }

        // ---------------- 고정 배경 (테마·크기가 바뀔 때만 다시 그림) ----------------
        static double Hash(int n) { double x = Math.Sin(n * 127.1) * 43758.5453; return x - Math.Floor(x); }

        void BuildBackground()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            background = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(background))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 테두리 바탕
                using (LinearGradientBrush metal = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Black, Color.Black, 90f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { th.F0, th.F1, th.F2 };
                    cb.Positions = new float[] { 0f, .5f, 1f };
                    metal.InterpolationColors = cb;
                    g.FillRectangle(metal, 0, 0, w, h);
                }

                // 테마별 무늬
                if (th.Pattern == 0 || th.Pattern == 1)
                {
                    for (int y = 0; y < h; y += 2)
                    {
                        int a = (int)(3 + 3 * Math.Sin(y * 1.7));
                        using (SolidBrush line = new SolidBrush(Color.FromArgb(Math.Max(0, a), 255, 255, 255))) g.FillRectangle(line, 0, y, w, 1);
                    }
                }
                if (th.Pattern == 1)
                {
                    // 아래쪽 경고 줄무늬 + 위쪽 홈
                    RectangleF hz = new RectangleF(F(14), h - F(6), w - F(28), Math.Max(2f, F(3)));
                    GraphicsState hs = g.Save();
                    g.SetClip(hz);
                    using (SolidBrush yb = new SolidBrush(Color.FromArgb(201, 162, 39))) g.FillRectangle(yb, hz);
                    using (SolidBrush kb = new SolidBrush(Color.FromArgb(22, 24, 26)))
                        for (float x = hz.X - F(8); x < hz.Right + F(8); x += F(8))
                            g.FillPolygon(kb, new PointF[] { new PointF(x, hz.Bottom), new PointF(x + F(3), hz.Y), new PointF(x + F(6), hz.Y), new PointF(x + F(3), hz.Bottom) });
                    g.Restore(hs);
                    using (SolidBrush slot = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillRectangle(slot, w / 2f - F(20), F(1), F(40), F(3));
                }
                if (th.Pattern == 2)
                {
                    // 생체 핏줄
                    using (Pen vein = new Pen(Color.FromArgb(140, 120, 30, 50), Math.Max(1f, F(1.2))))
                        for (int i = 0; i < 9 + (w > S(260) ? 4 : 0); i++)
                        {
                            float sx = (float)Hash(i) * w, sy = (float)Hash(i + 20) * h;
                            g.DrawBezier(vein, sx, sy,
                                sx + (float)(Hash(i + 40) * 60 - 30) * scale, sy + (float)(Hash(i + 60) * 30 - 15) * scale,
                                sx + (float)(Hash(i + 80) * 80 - 40) * scale, sy + (float)(Hash(i + 90) * 40 - 20) * scale,
                                sx + (float)(Hash(i + 99) * 90 - 45) * scale, sy + (float)(Hash(i + 7) * 40 - 20) * scale);
                        }
                }
                if (th.Pattern == 3)
                {
                    // 새김 무늬 + 모서리 판
                    using (SolidBrush eng = new SolidBrush(Color.FromArgb(20, 255, 240, 200)))
                        for (float y = F(3); y < h; y += Math.Max(3f, F(4))) g.FillRectangle(eng, 0, y, w, 1);
                    float c = F(16);
                    using (SolidBrush plate = new SolidBrush(Color.FromArgb(90, 40, 25, 5)))
                    {
                        g.FillPolygon(plate, new PointF[] { new PointF(0, 0), new PointF(c, 0), new PointF(0, c) });
                        g.FillPolygon(plate, new PointF[] { new PointF(w, 0), new PointF(w - c, 0), new PointF(w, c) });
                        g.FillPolygon(plate, new PointF[] { new PointF(0, h), new PointF(c, h), new PointF(0, h - c) });
                        g.FillPolygon(plate, new PointF[] { new PointF(w, h), new PointF(w - c, h), new PointF(w, h - c) });
                    }
                }

                // 베벨 (위·왼쪽 밝게, 아래·오른쪽 어둡게)
                g.SmoothingMode = SmoothingMode.None;
                using (SolidBrush hi = new SolidBrush(th.Hi)) { g.FillRectangle(hi, 0, 0, w, 1); g.FillRectangle(hi, 0, 0, 1, h); }
                using (SolidBrush hi2 = new SolidBrush(th.Hi2)) { g.FillRectangle(hi2, 1, 1, w - 2, 1); g.FillRectangle(hi2, 1, 1, 1, h - 2); }
                using (SolidBrush lo = new SolidBrush(th.Lo)) { g.FillRectangle(lo, 0, h - 1, w, 1); g.FillRectangle(lo, w - 1, 0, 1, h); }

                // 모서리 장식: 리벳 / 생체 돌기 / 보석
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] corners = { new PointF(F(5), F(5)), new PointF(w - F(6), F(5)), new PointF(F(5), h - F(6)), new PointF(w - F(6), h - F(6)) };
                foreach (PointF p in corners) DrawCorner(g, p);

                // 초상화 칸 (내용은 OnPaint에서 매 프레임 그림)
                if (PortraitOn)
                {
                    RectangleF pr = PortraitRect;
                    using (SolidBrush black = new SolidBrush(Color.Black)) g.FillRectangle(black, pr.X - 1, pr.Y - 1, pr.Width + 2, pr.Height + 2);
                }

                // 화면
                RectangleF sc = Screen0;
                g.SmoothingMode = SmoothingMode.None;
                using (SolidBrush black = new SolidBrush(Color.Black)) g.FillRectangle(black, sc.X - 1, sc.Y - 1, sc.Width + 2, sc.Height + 2);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    float cx = sc.X + sc.Width * .6f, cy = sc.Y + sc.Height * .45f, rr = sc.Width * .8f;
                    gp.AddEllipse(cx - rr, cy - rr, rr * 2, rr * 2);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterPoint = new PointF(cx, cy);
                        pg.CenterColor = th.Sc0;
                        pg.SurroundColors = new Color[] { th.Sc1 };
                        g.FillRectangle(pg, sc);
                    }
                }
                using (Pen frame = new Pen(th.ScBorder)) g.DrawRectangle(frame, sc.X - 1, sc.Y - 1, sc.Width + 1, sc.Height + 1);
                using (SolidBrush scan = new SolidBrush(Color.FromArgb(th.ScanAlpha, 0, 0, 0)))
                    for (float y = sc.Y; y < sc.Bottom; y += Math.Max(2f, F(2)))
                        g.FillRectangle(scan, sc.X, y, sc.Width, Math.Max(1f, F(1)));
                using (Pen br = new Pen(th.Bracket, Math.Max(1f, F(1))))
                {
                    float L = F(6), x0 = sc.X + F(2), y0 = sc.Y + F(2), x1 = sc.Right - F(3), y1 = sc.Bottom - F(3);
                    g.DrawLines(br, new PointF[] { new PointF(x0 + L, y0), new PointF(x0, y0), new PointF(x0, y0 + L) });
                    g.DrawLines(br, new PointF[] { new PointF(x1 - L, y0), new PointF(x1, y0), new PointF(x1, y0 + L) });
                    g.DrawLines(br, new PointF[] { new PointF(x0 + L, y1), new PointF(x0, y1), new PointF(x0, y1 - L) });
                    g.DrawLines(br, new PointF[] { new PointF(x1 - L, y1), new PointF(x1, y1), new PointF(x1, y1 - L) });
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush dia = new SolidBrush(th.Lab))
                    g.FillPolygon(dia, new PointF[] {
                        new PointF(sc.X + F(10), sc.Y + F(7)), new PointF(sc.X + F(13.5), sc.Y + F(10.5)),
                        new PointF(sc.X + F(10), sc.Y + F(14)), new PointF(sc.X + F(6.5), sc.Y + F(10.5)) });
            }
            glowBmp = new Bitmap(Math.Max(1, w / 4), Math.Max(1, h / 4), PixelFormat.Format32bppPArgb);
        }

        void DrawCorner(Graphics g, PointF p)
        {
            float rv = F(1.7);
            if (th.Rivet == 0)
            {
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(10, 12, 12))) g.FillEllipse(sh, p.X - rv + .6f, p.Y - rv + .6f, rv * 2.4f, rv * 2.4f);
                using (SolidBrush body = new SolidBrush(th.RivetC)) g.FillEllipse(body, p.X - rv, p.Y - rv, rv * 2, rv * 2);
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(225, 232, 228))) g.FillRectangle(shine, p.X - rv * .6f, p.Y - rv * .6f, Math.Max(1f, rv * .6f), Math.Max(1f, rv * .6f));
            }
            else if (th.Rivet == 1)
            {
                float r = F(2.6);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(p.X - r, p.Y - r, r * 2, r * 2);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterPoint = new PointF(p.X - F(.5), p.Y - F(.5));
                        pg.CenterColor = Color.FromArgb(255, 224, 160);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, th.RivetC) };
                        g.FillPath(pg, gp);
                    }
                }
                using (SolidBrush core = new SolidBrush(Color.FromArgb(200, th.RivetC))) g.FillEllipse(core, p.X - r * .5f, p.Y - r * .5f, r, r);
            }
            else
            {
                float r = F(2.2);
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(90, th.RivetC))) g.FillEllipse(halo, p.X - F(3.2), p.Y - F(3.2), F(6.4), F(6.4));
                using (SolidBrush gem = new SolidBrush(th.RivetC))
                    g.FillPolygon(gem, new PointF[] { new PointF(p.X, p.Y - r), new PointF(p.X + r, p.Y), new PointF(p.X, p.Y + r), new PointF(p.X - r, p.Y) });
                using (SolidBrush shine = new SolidBrush(Color.White)) g.FillRectangle(shine, p.X - F(.5), p.Y - F(1), Math.Max(1f, F(1)), Math.Max(1f, F(1)));
            }
        }

        // ---------------- 그리기 ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImage(background, new Rectangle(0, 0, background.Width, background.Height), new Rectangle(0, 0, background.Width, background.Height), GraphicsUnit.Pixel);

            // 초상화
            if (PortraitOn)
            {
                RectangleF pr = PortraitRect;
                double t = engine.Now / (double)secTicks;
                if (opt.Portrait == 2 && custom != null)
                {
                    GraphicsState ps = g.Save();
                    g.SetClip(pr);
                    custom.Draw(g, pr);
                    g.Restore(ps);
                }
                else Portraits.Draw(g, th, pr, t, scale);
                // 초상화 위 주사선과 테두리
                using (SolidBrush scan = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
                    for (float y = pr.Y; y < pr.Bottom; y += Math.Max(2f, F(2))) g.FillRectangle(scan, pr.X, y, pr.Width, Math.Max(1f, F(1)));
                using (Pen pb = new Pen(th.ScBorder)) g.DrawRectangle(pb, pr.X - 1, pr.Y - 1, pr.Width + 1, pr.Height + 1);
                // 초상화만 다시 그리는 경우엔 여기서 끝 (CPU 절약)
                if (Rectangle.Ceiling(pr).Contains(e.ClipRectangle)) return;
            }

            int v = shown < 0 ? 0 : shown;
            Color main = ColorFor(v);
            RectangleF sc = Screen0;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using (StringFormat left = new StringFormat())
            using (StringFormat right = new StringFormat())
            {
                left.Alignment = StringAlignment.Near; left.LineAlignment = StringAlignment.Center;
                right.Alignment = StringAlignment.Far; right.LineAlignment = StringAlignment.Center;
                left.FormatFlags = StringFormatFlags.NoWrap; right.FormatFlags = StringFormatFlags.NoWrap;

                // 윗줄: APM / PEAK / 입력 LED
                RectangleF top = new RectangleF(sc.X + F(17), sc.Y + F(3), sc.Width - F(36), F(16));
                using (SolidBrush lb = new SolidBrush(Label)) g.DrawString("APM", labelFont, lb, top, left);
                using (SolidBrush pb = new SolidBrush(flashOn ? Amber : LabelDim))
                    g.DrawString("PEAK " + peak.ToString().PadLeft(4), labelFont, pb, top, right);

                RectangleF led = new RectangleF(sc.Right - F(12), sc.Y + F(8.5), F(5), F(5));
                if (ledOn)
                {
                    using (SolidBrush halo = new SolidBrush(Color.FromArgb(90, th.LedOn))) g.FillRectangle(halo, led.X - F(2), led.Y - F(2), led.Width + F(4), led.Height + F(4));
                    using (SolidBrush on = new SolidBrush(th.LedOn)) g.FillRectangle(on, led);
                }
                else using (SolidBrush off = new SolidBrush(th.LedOff)) g.FillRectangle(off, led);

                // 등급 자리: 상황 메시지 > 자리 비움 > 등급 문구
                RectangleF rankR = new RectangleF(sc.X + F(8), sc.Y + F(27), sc.Width * .68f, F(18));
                long nowT = engine.Now;
                if (msgText != null && nowT < msgUntil)
                {
                    using (SolidBrush mb = new SolidBrush(msgColor)) g.DrawString(msgText, rankFont, mb, rankR, left);
                }
                else if (opt.Messages && (nowT - lastInput) / (double)secTicks >= 300)
                {
                    using (SolidBrush ib = new SolidBrush(Color.FromArgb(170, Notice))) g.DrawString("놀고 있는 일꾼 감지", rankFont, ib, rankR, left);
                }
                else if (opt.Rank)
                {
                    using (SolidBrush rb = new SolidBrush(Color.FromArgb(190, main))) g.DrawString(RankFor(v), rankFont, rb, rankR, left);
                }

                // 큰 숫자: 고정폭 + 4자리 패딩 → 자릿수가 바뀌어도 흔들리지 않음
                string num = v.ToString().PadLeft(4);
                RectangleF numR = new RectangleF(sc.X, sc.Y + F(15), sc.Width - F(6), F(44));

                // 빛 번짐: 1/4 크기로 그린 숫자를 부드럽게 확대해서 반투명하게 겹침
                using (Graphics gg = Graphics.FromImage(glowBmp))
                {
                    gg.Clear(Color.Transparent);
                    gg.TextRenderingHint = TextRenderingHint.AntiAlias;
                    gg.ScaleTransform(.25f, .25f);
                    using (SolidBrush gb = new SolidBrush(main)) gg.DrawString(num, numFont, gb, numR, right);
                }
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = .55f;
                    ia.SetColorMatrix(cm);
                    InterpolationMode old = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    Rectangle dst = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                    g.DrawImage(glowBmp, dst, 0, 0, glowBmp.Width, glowBmp.Height, GraphicsUnit.Pixel, ia);
                    g.InterpolationMode = old;
                }
                using (SolidBrush nb = new SolidBrush(main)) g.DrawString(num, numFont, nb, numR, right);
            }

            // 아래: 체력바처럼 칸이 나뉜 게이지
            int n = 24;
            float gx = sc.X + F(8), gy = sc.Y + F(67), gw = sc.Width - F(16), gap = F(2);
            float segW = (gw - (n - 1) * gap) / n, segH = Math.Max(2f, F(4));
            int lit = (int)Math.Round(Math.Min(1.0, v / (double)opt.MaxGauge) * n);
            using (SolidBrush onB = new SolidBrush(main))
            using (SolidBrush offB = new SolidBrush(SegOff))
                for (int i = 0; i < n; i++)
                    g.FillRectangle(i < lit ? onB : offB, gx + i * (segW + gap), gy, segW, segH);

            float rowY = sc.Y + F(76);

            // 근무 정보 줄: 퇴근 카운트다운 · 오늘 총 명령
            if (opt.Info)
            {
                using (StringFormat lf = new StringFormat())
                using (StringFormat rf = new StringFormat())
                using (SolidBrush ib = new SolidBrush(LabelDim))
                {
                    lf.Alignment = StringAlignment.Near; lf.LineAlignment = StringAlignment.Center; lf.FormatFlags = StringFormatFlags.NoWrap;
                    rf.Alignment = StringAlignment.Far; rf.LineAlignment = StringAlignment.Center; rf.FormatFlags = StringFormatFlags.NoWrap;
                    RectangleF ir = new RectangleF(gx, rowY, gw, F(14));
                    g.DrawString(infoLeft, infoFont, ib, ir, lf);
                    g.DrawString(infoRight, infoFont, ib, ir, rf);
                }
                rowY += F(InfoH);
            }

            // 최근 60초 APM 그래프 (리플레이 그래프처럼)
            if (opt.Graph)
            {
                RectangleF gr = new RectangleF(gx, rowY + F(2), gw, F(GraphH - 6));
                using (Pen grid = new Pen(Color.FromArgb(22, 60, 26), 1f))
                {
                    g.DrawLine(grid, gr.X, gr.Bottom, gr.Right, gr.Bottom);
                    g.DrawLine(grid, gr.X, gr.Y + gr.Height / 2, gr.Right, gr.Y + gr.Height / 2);
                }
                double mx = 150;
                for (int i = 0; i < history.Length; i++) if (history[i] > mx) mx = history[i];
                int cnt = history.Length;
                PointF[] pts = new PointF[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    double hv = history[(histPos + i) % cnt];
                    pts[i] = new PointF(gr.X + gr.Width * i / (cnt - 1), gr.Bottom - (float)(hv / mx) * gr.Height);
                }
                SmoothingMode sm = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath area = new GraphicsPath())
                {
                    area.AddLine(gr.X, gr.Bottom, pts[0].X, pts[0].Y);
                    area.AddLines(pts);
                    area.AddLine(pts[cnt - 1].X, pts[cnt - 1].Y, gr.Right, gr.Bottom);
                    area.CloseFigure();
                    using (SolidBrush fillB = new SolidBrush(Color.FromArgb(40, Lime))) g.FillPath(fillB, area);
                }
                using (Pen lineP = new Pen(Color.FromArgb(200, Lime), Math.Max(1f, F(1.2)))) g.DrawLines(lineP, pts);
                g.SmoothingMode = sm;
                using (StringFormat rf2 = new StringFormat())
                using (SolidBrush lb2 = new SolidBrush(Color.FromArgb(150, LabelDim)))
                {
                    rf2.Alignment = StringAlignment.Far; rf2.LineAlignment = StringAlignment.Near;
                    g.DrawString(((int)mx).ToString(), labelFont, lb2, new RectangleF(gr.X, gr.Y - F(3), gr.Width, F(12)), rf2);
                }
            }
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

                case NativeMethods.WM_NCRBUTTONUP:
                    // 우클릭 → 설정 메뉴 (시스템 메뉴 대신)
                    menu.Show(Cursor.Position);
                    return;

                case NativeMethods.WM_NCRBUTTONDOWN:
                    // 우클릭 시 시스템 메뉴가 뜨지 않도록 무시
                    return;
            }
            base.WndProc(ref m);
        }
    }

    // ------------------------------------------------------------------
    //  설정 창 (바꾸는 즉시 계기판에 반영)
    // ------------------------------------------------------------------
    sealed class SettingsForm : Form
    {
        static readonly Color Bg = Color.FromArgb(22, 27, 25);
        static readonly Color Field = Color.FromArgb(32, 39, 36);
        static readonly Color Fg = Color.FromArgb(222, 232, 222);
        static readonly Color Green = Color.FromArgb(76, 192, 70);
        static readonly Color Dim = Color.FromArgb(140, 160, 145);
        static readonly Color Warn = Color.FromArgb(255, 120, 80);
        const int RX = 372;   // 오른쪽 칸 시작 위치

        readonly ApmForm owner;
        readonly Options opt;
        ComboBox cTheme, cPortrait;
        Label lImage;
        CheckBox cRank, cLed, cMsg, cInfo, cGraph, cBreak, cAuto;
        DateTimePicker dOff, dLunch;
        NumericUpDown nBreak, nGauge, nWindow, nStart;
        TrackBar tOpacity;
        Label lOpacity, lStatus;
        bool loading;

        public SettingsForm(ApmForm owner, Options opt, string status)
        {
            this.owner = owner; this.opt = opt;
            loading = true;   // 컨트롤을 만드는 동안 변경 이벤트 무시
            SuspendLayout();
            Font = new Font("Malgun Gothic", 9f);
            Text = "출근 APM 설정";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg; ForeColor = Fg;
            ClientSize = new Size(744, 466);

            // ---- 왼쪽: 디자인 · 표시 ----
            Head("디자인", 16, 14);
            Lbl("테마", 26, 46, Fg);
            cTheme = Combo(130, 42, 220);
            foreach (Theme t in Theme.All) cTheme.Items.Add(t.Name + (t.Key == "classic" ? " (기본)" : ""));
            Lbl("초상화", 26, 80, Fg);
            cPortrait = Combo(130, 76, 220);
            cPortrait.Items.AddRange(new object[] { "끄기", "테마 애니메이션", "사용자 이미지 / GIF" });
            Lbl("이미지", 26, 114, Fg);
            lImage = Lbl("", 130, 114, Dim);
            lImage.AutoSize = false; lImage.SetBounds(130, 114, 140, 20); lImage.AutoEllipsis = true;
            Button bImg = Btn("이미지 선택…", 274, 108, 92, false);
            bImg.Height = 28;
            bImg.Click += delegate { PickImage(); };

            Head("표시", 16, 150);
            cRank = Check("등급 문구 (대기 · 업무 중 · 집중 모드 …)", 26, 176, 330);
            cLed = Check("입력 LED", 26, 200, 330);
            cMsg = Check("상황 메시지 (점심 · 퇴근 · 기록 갱신 등)", 26, 224, 330);
            cInfo = Check("근무 정보 줄 (퇴근 카운트다운 · 총 명령)", 26, 248, 330);
            cGraph = Check("APM 그래프 (최근 60초)", 26, 272, 330);

            // ---- 오른쪽: 알림 · 계기판 · 저장 ----
            Head("알림", RX, 14);
            cBreak = Check("휴식 알림", RX + 10, 40, 130);
            nBreak = Num(10, 180, 5, 0, RX + 144, 38);
            Lbl("분 연속 작업하면", RX + 240, 42, Dim);
            Lbl("점심 시간", RX + 10, 76, Fg);
            dLunch = Time(RX + 144, 72);
            Lbl("10분 전 · 정각 알림", RX + 240, 76, Dim);
            Lbl("퇴근 시간", RX + 10, 110, Fg);
            dOff = Time(RX + 144, 106);
            Lbl("10분 전 · 정각 알림", RX + 240, 110, Dim);

            Head("계기판", RX, 150);
            Lbl("투명도", RX + 10, 182, Fg);
            tOpacity = new TrackBar();
            tOpacity.Minimum = 30; tOpacity.Maximum = 100; tOpacity.TickFrequency = 10; tOpacity.SmallChange = 1; tOpacity.LargeChange = 5;
            tOpacity.SetBounds(RX + 134, 174, 170, 30); tOpacity.BackColor = Bg;
            tOpacity.ValueChanged += Changed;
            Controls.Add(tOpacity);
            lOpacity = Lbl("", RX + 310, 182, Green);
            Lbl("게이지 최대 APM", RX + 10, 218, Fg);
            nGauge = Num(100, 2000, 50, 0, RX + 144, 214);
            Lbl("APM 평균 시간 (초)", RX + 10, 252, Fg);
            nWindow = Num(1, 30, 0.5m, 1, RX + 144, 248);
            Lbl("시작 완충 (초)", RX + 10, 284, Fg);
            nStart = Num(0.5m, 10, 0.5m, 1, RX + 144, 280);
            Lbl("클수록 첫 몇 타에 덜 튐", RX + 240, 284, Dim);
            Lbl("평균 시간이 짧으면 빨리 반응하고 빨리 떨어지고, 길면 묵직합니다.", RX + 10, 310, Dim);
            cAuto = Check("종료할 때 설정 자동 저장", RX + 10, 330, 330);

            // ---- 아래: 버튼 · 상태 ----
            Panel sep = new Panel(); sep.BackColor = Color.FromArgb(40, 70, 44); sep.SetBounds(16, 362, 712, 1); Controls.Add(sep);
            Button bSave = Btn("설정 저장", 16, 376, 110, true);
            Button bLoad = Btn("설정 불러오기…", 132, 376, 130, false);
            Button bReset = Btn("기본값", 268, 376, 96, false);
            Button bClose = Btn("닫기", 648, 376, 80, false);
            bSave.Click += delegate
            {
                string m; bool ok = owner.SaveSettings(out m);
                SetStatus(m, ok ? Green : Warn);
            };
            bLoad.Click += delegate
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "설정 파일 불러오기";
                    dlg.Filter = "설정 파일 (*.ini)|*.ini|모든 파일 (*.*)|*.*";
                    dlg.InitialDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string m; bool ok = owner.LoadSettings(dlg.FileName, out m);
                    if (ok) RefreshFromOptions();
                    SetStatus(m, ok ? Green : Warn);
                }
            };
            bReset.Click += delegate
            {
                owner.ResetSettings(); RefreshFromOptions();
                SetStatus("기본값으로 되돌렸습니다. 다음 실행 때도 쓰려면 [설정 저장]을 누르세요.", Dim);
            };
            bClose.Click += delegate { Close(); };
            CancelButton = bClose;

            lStatus = new Label();
            lStatus.AutoSize = false; lStatus.SetBounds(16, 416, 712, 44); lStatus.ForeColor = Dim; lStatus.BackColor = Bg;
            Controls.Add(lStatus);

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            RefreshFromOptions();
            SetStatus(status, Dim);
        }

        // 계기판 왼쪽(공간이 없으면 아래)에 뜨게 함
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle wa = Screen.FromControl(owner).WorkingArea;
            int x = owner.Left - Width - 8, y = owner.Top;
            if (x < wa.Left) { x = Math.Max(wa.Left, owner.Right - Width); y = owner.Bottom + 8; }
            if (y + Height > wa.Bottom) y = Math.Max(wa.Top, wa.Bottom - Height);
            Location = new Point(x, y);
        }

        void PickImage()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "초상화 이미지 선택";
                dlg.Filter = "이미지 (*.gif;*.png;*.jpg;*.jpeg;*.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.bmp|모든 파일 (*.*)|*.*";
                if (!string.IsNullOrEmpty(opt.PortraitImage))
                {
                    try { dlg.InitialDirectory = Path.GetDirectoryName(opt.PortraitImage); } catch { }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                opt.PortraitImage = dlg.FileName;
                opt.Portrait = 2;
                owner.ApplyOptions();
                RefreshFromOptions();
                if (owner.PortraitError != null) SetStatus("이미지를 열지 못했습니다: " + owner.PortraitError, Warn);
                else SetStatus("초상화 이미지를 적용했습니다:\n" + dlg.FileName + "\n이미지는 복사하지 않고 이 경로에서 읽습니다. 파일을 옮기면 다시 선택해 주세요.", Green);
            }
        }

        void SetStatus(string text, Color c) { lStatus.Text = text ?? ""; lStatus.ForeColor = c; }

        void Head(string text, int x, int y)
        {
            Label l = Lbl(text, x, y, Green);
            l.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            Panel line = new Panel(); line.BackColor = Color.FromArgb(40, 70, 44); line.SetBounds(x + 54, y + 11, 302, 1);
            Controls.Add(line);
        }

        Label Lbl(string text, int x, int y, Color c)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); l.ForeColor = c; l.BackColor = Bg;
            Controls.Add(l); return l;
        }

        CheckBox Check(string text, int x, int y, int w)
        {
            CheckBox c = new CheckBox(); c.Text = text; c.SetBounds(x, y, w, 22); c.ForeColor = Fg; c.BackColor = Bg;
            c.CheckedChanged += Changed;
            Controls.Add(c); return c;
        }

        ComboBox Combo(int x, int y, int w)
        {
            ComboBox c = new ComboBox(); c.DropDownStyle = ComboBoxStyle.DropDownList; c.SetBounds(x, y, w, 24);
            c.BackColor = Field; c.ForeColor = Fg; c.FlatStyle = FlatStyle.Flat;
            c.SelectedIndexChanged += Changed;
            Controls.Add(c); return c;
        }

        NumericUpDown Num(decimal min, decimal max, decimal step, int decimals, int x, int y)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max; n.Increment = step; n.DecimalPlaces = decimals;
            n.SetBounds(x, y, 90, 24); n.BackColor = Field; n.ForeColor = Fg; n.TextAlign = HorizontalAlignment.Right;
            n.ValueChanged += Changed;
            Controls.Add(n); return n;
        }

        DateTimePicker Time(int x, int y)
        {
            DateTimePicker d = new DateTimePicker();
            d.Format = DateTimePickerFormat.Custom; d.CustomFormat = "HH:mm"; d.ShowUpDown = true;
            d.SetBounds(x, y, 90, 24);
            d.ValueChanged += Changed;
            Controls.Add(d); return d;
        }

        Button Btn(string text, int x, int y, int w, bool primary)
        {
            Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 32);
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = primary ? Green : Color.FromArgb(70, 90, 75);
            b.BackColor = primary ? Color.FromArgb(28, 66, 32) : Field; b.ForeColor = Fg;
            Controls.Add(b); return b;
        }

        void RefreshFromOptions()
        {
            loading = true;
            int ti = 0;
            for (int i = 0; i < Theme.All.Length; i++) if (Theme.All[i].Key == Theme.Get(opt.Theme).Key) ti = i;
            cTheme.SelectedIndex = ti;
            cPortrait.SelectedIndex = Math.Max(0, Math.Min(2, opt.Portrait));
            lImage.Text = string.IsNullOrEmpty(opt.PortraitImage) ? "(선택 안 함)" : Path.GetFileName(opt.PortraitImage);
            cRank.Checked = opt.Rank; cLed.Checked = opt.Led; cMsg.Checked = opt.Messages;
            cInfo.Checked = opt.Info; cGraph.Checked = opt.Graph; cBreak.Checked = opt.BreakReminder;
            cAuto.Checked = opt.AutoSave;
            dOff.Value = DateTime.Today.AddMinutes(opt.OffMinutes);
            dLunch.Value = DateTime.Today.AddMinutes(opt.LunchMinutes);
            nBreak.Value = Math.Max(nBreak.Minimum, Math.Min(nBreak.Maximum, opt.BreakMinutes));
            tOpacity.Value = Math.Max(tOpacity.Minimum, Math.Min(tOpacity.Maximum, opt.Opacity));
            lOpacity.Text = opt.Opacity + "%";
            nGauge.Value = Math.Max(nGauge.Minimum, Math.Min(nGauge.Maximum, opt.MaxGauge));
            nWindow.Value = Math.Max(nWindow.Minimum, Math.Min(nWindow.Maximum, (decimal)opt.WindowSec));
            nStart.Value = Math.Max(nStart.Minimum, Math.Min(nStart.Maximum, (decimal)opt.StartSec));
            loading = false;
        }

        void Changed(object sender, EventArgs e)
        {
            if (loading) return;
            opt.Theme = Theme.All[Math.Max(0, cTheme.SelectedIndex)].Key;
            opt.Portrait = Math.Max(0, cPortrait.SelectedIndex);
            opt.Rank = cRank.Checked; opt.Led = cLed.Checked; opt.Messages = cMsg.Checked;
            opt.Info = cInfo.Checked; opt.Graph = cGraph.Checked; opt.BreakReminder = cBreak.Checked;
            opt.AutoSave = cAuto.Checked;
            opt.OffMinutes = dOff.Value.Hour * 60 + dOff.Value.Minute;
            opt.LunchMinutes = dLunch.Value.Hour * 60 + dLunch.Value.Minute;
            opt.BreakMinutes = (int)nBreak.Value;
            opt.Opacity = tOpacity.Value;
            opt.MaxGauge = (int)nGauge.Value;
            opt.WindowSec = (double)nWindow.Value;
            opt.StartSec = (double)nStart.Value;
            lOpacity.Text = opt.Opacity + "%";
            owner.ApplyOptions();
            if (sender == cPortrait && opt.Portrait == 2)
            {
                if (string.IsNullOrEmpty(opt.PortraitImage)) { SetStatus("[이미지 선택…]으로 쓸 이미지를 골라 주세요. 고르기 전까지는 초상화 칸이 숨겨집니다.", Dim); }
                else if (owner.PortraitError != null) SetStatus("이미지를 열지 못했습니다: " + owner.PortraitError, Warn);
            }
        }
    }
}
