// =====================================================================
//  그림 친구 (DrawPet) - 직접 그린 캐릭터가 바탕화면을 돌아다니는 프로그램
//  빌드: build_pet.bat (Windows 내장 .NET Framework 4.x csc.exe, C# 5 문법)
//
//  [사용법]
//   1. 그리기 창에서 이름을 입력하고 64x64 칸에 캐릭터를 그립니다.
//   2. "+ 새 프레임" / "복제"로 장면을 여러 장 그리면 애니메이션이 됩니다.
//      (어니언 스킨을 켜면 이전 프레임이 흐리게 비쳐서 이어 그리기 쉽습니다)
//   3. "화면에 내보내기"를 누르면 캐릭터가 작업 표시줄 위를 걸어 다닙니다.
//      캐릭터는 오른쪽을 보도록 그리세요. 왼쪽으로 걸을 땐 자동으로 뒤집힙니다.
//
//  [캐릭터 조작]
//   - 좌클릭 드래그: 들어서 옮기기 (놓으면 떨어짐)
//   - 더블클릭: 그 캐릭터 닫기
//   - 우클릭: 메뉴 (그리기 창 열기 / 방향 바꾸기 / 닫기 / 모두 종료)
//
//  [투명성] 네트워크·레지스트리 접근 없음. 파일은 사용자가 "저장/불러오기"를
//           눌렀을 때 직접 고른 PNG 파일만 읽고 씁니다.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("DrawPet")]
[assembly: AssemblyDescription("Draw your own animated character and let it walk on your desktop.")]
[assembly: AssemblyProduct("DrawPet")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace DrawPet
{
    static class Native
    {
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();
    }

    // ------------------------------------------------------------------
    //  프로그램 시작 / 종료 관리
    //  그리기 창과 캐릭터 창이 모두 닫히면 프로그램이 끝납니다.
    // ------------------------------------------------------------------
    static class Program
    {
        public static EditorForm Editor;
        public static readonly List<PetForm> Pets = new List<PetForm>();
        public static readonly Random Rnd = new Random();

        [STAThread]
        static void Main()
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Editor = new EditorForm();
            Editor.Show();
            Application.Run();
        }

        public static void ShowEditor()
        {
            if (Editor == null || Editor.IsDisposed) return;
            Editor.Show();
            if (Editor.WindowState == FormWindowState.Minimized) Editor.WindowState = FormWindowState.Normal;
            Editor.Activate();
        }

        public static void CheckExit()
        {
            bool editorOpen = Editor != null && !Editor.IsDisposed && Editor.Visible;
            if (Pets.Count == 0 && !editorOpen) Application.ExitThread();
        }

        public static void ExitAll()
        {
            foreach (PetForm p in Pets.ToArray()) p.Close();
            if (Editor != null && !Editor.IsDisposed)
            {
                Editor.AllowClose = true;
                Editor.Close();
            }
            Application.ExitThread();
        }
    }

    // ------------------------------------------------------------------
    //  픽셀 그림 도우미
    // ------------------------------------------------------------------
    static class Px
    {
        public const int N = 64;                                          // 프레임 크기 64x64
        public static readonly Color Key = Color.FromArgb(255, 0, 255);   // 캐릭터 창의 투명 처리용 색

        public static Bitmap NewFrame()
        {
            Bitmap b = new Bitmap(N, N, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.Transparent);
            return b;
        }

        public static Bitmap Clone(Bitmap src)
        {
            return src.Clone(new Rectangle(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
        }

        // 투명 처리용 색(자홍색)과 정확히 같으면 살짝 바꿔서 구멍이 뚫리지 않게 함
        public static Color Fix(Color c)
        {
            if (c.R == 255 && c.G == 0 && c.B == 255) return Color.FromArgb(255, 254, 0, 254);
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        public static bool IsEmpty(Bitmap b)
        {
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    if (b.GetPixel(x, y).A != 0) return false;
            return true;
        }

        static int Norm(Color c) { return c.A == 0 ? 0 : c.ToArgb(); }

        public static void FloodFill(Bitmap b, Point start, Color col)
        {
            if (start.X < 0 || start.Y < 0 || start.X >= b.Width || start.Y >= b.Height) return;
            int target = Norm(b.GetPixel(start.X, start.Y));
            int repl = Norm(col);
            if (target == repl) return;
            Stack<Point> st = new Stack<Point>();
            st.Push(start);
            while (st.Count > 0)
            {
                Point p = st.Pop();
                if (p.X < 0 || p.Y < 0 || p.X >= b.Width || p.Y >= b.Height) continue;
                if (Norm(b.GetPixel(p.X, p.Y)) != target) continue;
                b.SetPixel(p.X, p.Y, col);
                st.Push(new Point(p.X + 1, p.Y));
                st.Push(new Point(p.X - 1, p.Y));
                st.Push(new Point(p.X, p.Y + 1));
                st.Push(new Point(p.X, p.Y - 1));
            }
        }

        public static void DrawChecker(Graphics g, Rectangle r, int sq)
        {
            if (sq < 1) sq = 1;
            using (SolidBrush light = new SolidBrush(Color.FromArgb(236, 236, 236)))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(206, 206, 206)))
            {
                g.FillRectangle(light, r);
                for (int y = 0; y * sq < r.Height; y++)
                    for (int x = 0; x * sq < r.Width; x++)
                        if (((x + y) & 1) == 1)
                            g.FillRectangle(dark, r.X + x * sq, r.Y + y * sq,
                                Math.Min(sq, r.Width - x * sq), Math.Min(sq, r.Height - y * sq));
            }
        }

        // 픽셀이 번지지 않게 정수 배율 + 최근접 보간으로 확대
        public static void DrawPixelArt(Graphics g, Bitmap b, Rectangle dest)
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(b, dest, new Rectangle(0, 0, b.Width, b.Height), GraphicsUnit.Pixel);
        }
    }

    enum Tool { Pen, Eraser, Fill }

    struct UndoItem
    {
        public int Frame;
        public Bitmap Image;
    }

    // ------------------------------------------------------------------
    //  그림판 (64x64 확대 편집)
    //  좌클릭 = 현재 도구, 우클릭 = 지우개
    // ------------------------------------------------------------------
    sealed class DrawCanvas : Control
    {
        readonly EditorForm ed;
        Point last;
        bool drawing;
        bool erasing;

        public DrawCanvas(EditorForm ed)
        {
            this.ed = ed;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Cross;
        }

        int Cell { get { return Math.Max(1, Math.Min(ClientSize.Width, ClientSize.Height) / Px.N); } }

        Point ToPixel(Point p)
        {
            int c = Cell;
            return new Point((int)Math.Floor((double)p.X / c), (int)Math.Floor((double)p.Y / c));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Control);
            int c = Cell;
            Rectangle r = new Rectangle(0, 0, Px.N * c, Px.N * c);
            Px.DrawChecker(g, r, c * 4);

            if (ed.OnionSkin && ed.Current > 0)
            {
                ColorMatrix cm = new ColorMatrix();
                cm.Matrix33 = 0.3f;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(ed.Frames[ed.Current - 1], r, 0, 0, Px.N, Px.N, GraphicsUnit.Pixel, ia);
                }
            }

            Px.DrawPixelArt(g, ed.Frames[ed.Current], r);

            g.PixelOffsetMode = PixelOffsetMode.Default;
            if (ed.ShowGrid && c >= 4)
            {
                using (Pen thin = new Pen(Color.FromArgb(28, 0, 0, 0)))
                using (Pen thick = new Pen(Color.FromArgb(70, 0, 0, 0)))
                {
                    for (int i = 1; i < Px.N; i++)
                    {
                        Pen p = (i % 8 == 0) ? thick : thin;
                        g.DrawLine(p, i * c, 0, i * c, r.Height - 1);
                        g.DrawLine(p, 0, i * c, r.Width - 1, i * c);
                    }
                }
            }
            g.DrawRectangle(Pens.Gray, 0, 0, r.Width - 1, r.Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
            Point p = ToPixel(e.Location);
            erasing = e.Button == MouseButtons.Right || ed.CurrentTool == Tool.Eraser;
            ed.PushUndo();

            if (ed.CurrentTool == Tool.Fill)
            {
                Px.FloodFill(ed.Frames[ed.Current], p, e.Button == MouseButtons.Right ? Color.Transparent : ed.PenColor);
                ed.FrameEdited();
                return;
            }

            drawing = true;
            Capture = true;
            last = p;
            Stamp(p);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!drawing) return;
            Point p = ToPixel(e.Location);
            if (p == last) return;
            Line(last, p);
            last = p;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!drawing) return;
            drawing = false;
            Capture = false;
            ed.FrameEdited();
        }

        void Stamp(Point p)
        {
            Bitmap b = ed.Frames[ed.Current];
            Color col = erasing ? Color.Transparent : ed.PenColor;
            int n = ed.BrushSize;
            int o = (n - 1) / 2;
            for (int dy = 0; dy < n; dy++)
                for (int dx = 0; dx < n; dx++)
                {
                    int x = p.X - o + dx, y = p.Y - o + dy;
                    if (x >= 0 && y >= 0 && x < Px.N && y < Px.N) b.SetPixel(x, y, col);
                }
        }

        // 브레젠험 직선: 마우스를 빨리 움직여도 선이 끊기지 않게
        void Line(Point a, Point b)
        {
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Stamp(new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }

    // ------------------------------------------------------------------
    //  프레임 썸네일
    // ------------------------------------------------------------------
    sealed class ThumbBox : Control
    {
        readonly EditorForm ed;
        public int Index;

        public ThumbBox(EditorForm ed, int index)
        {
            this.ed = ed;
            Index = index;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            bool sel = Index == ed.Current;
            g.Clear(sel ? Color.FromArgb(255, 140, 0) : SystemColors.ControlDark);
            if (Index >= ed.Frames.Count) return;
            int pad = 3;
            int side = Math.Min(Width, Height) - pad * 2;
            int scale = Math.Max(1, side / Px.N);
            int s = scale * Px.N;
            Rectangle r = new Rectangle((Width - s) / 2, (Height - s) / 2, s, s);
            Px.DrawChecker(g, r, 8);
            Px.DrawPixelArt(g, ed.Frames[Index], r);
            g.PixelOffsetMode = PixelOffsetMode.Default;
            TextRenderer.DrawText(g, (Index + 1).ToString(), Font, new Point(r.X + 1, r.Y + 1), Color.DimGray);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ed.SelectFrame(Index);
        }
    }

    // ------------------------------------------------------------------
    //  애니메이션 미리보기
    // ------------------------------------------------------------------
    sealed class PreviewBox : Control
    {
        readonly EditorForm ed;

        public PreviewBox(EditorForm ed)
        {
            this.ed = ed;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Control);
            if (ed.Frames.Count == 0) return;
            int i = ed.PreviewIndex % ed.Frames.Count;
            int scale = Math.Max(1, Math.Min(Width, Height) / Px.N);
            int s = scale * Px.N;
            Rectangle r = new Rectangle((Width - s) / 2, (Height - s) / 2, s, s);
            Px.DrawChecker(g, r, 8);
            Px.DrawPixelArt(g, ed.Frames[i], r);
        }
    }

    // ------------------------------------------------------------------
    //  그리기 창
    // ------------------------------------------------------------------
    sealed class EditorForm : Form
    {
        const int MaxFrames = 32;
        const int MaxUndo = 60;

        internal readonly List<Bitmap> Frames = new List<Bitmap>();
        internal int Current;
        internal int PreviewIndex;
        internal Tool CurrentTool = Tool.Pen;
        internal Color PenColor = Color.Black;
        internal bool AllowClose;

        readonly List<UndoItem> undo = new List<UndoItem>();
        readonly float dpi;
        bool hideNoticeShown;

        TextBox txtName;
        NumericUpDown numFps, numSpeed, numBrush;
        ComboBox cmbScale;
        CheckBox chkOnion, chkGrid, chkFlip;
        Panel colorNow;
        DrawCanvas canvas;
        FlowLayoutPanel strip;
        PreviewBox preview;
        Label lblFrame;
        readonly Timer previewTimer = new Timer();

        internal int BrushSize { get { return (int)numBrush.Value; } }
        internal bool OnionSkin { get { return chkOnion.Checked; } }
        internal bool ShowGrid { get { return chkGrid.Checked; } }

        static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(0, 0, 0),       Color.FromArgb(90, 90, 90),    Color.FromArgb(170, 170, 170), Color.FromArgb(255, 255, 255),
            Color.FromArgb(230, 40, 40),   Color.FromArgb(255, 140, 0),   Color.FromArgb(255, 220, 0),   Color.FromArgb(120, 220, 40),
            Color.FromArgb(20, 140, 60),   Color.FromArgb(0, 200, 220),   Color.FromArgb(40, 90, 230),   Color.FromArgb(20, 30, 110),
            Color.FromArgb(150, 60, 200),  Color.FromArgb(255, 130, 180), Color.FromArgb(130, 80, 40),   Color.FromArgb(255, 210, 170)
        };

        public EditorForm()
        {
            using (Graphics sg = Graphics.FromHwnd(IntPtr.Zero)) dpi = sg.DpiX / 96f;
            Frames.Add(Px.NewFrame());

            SuspendLayout();
            Font = new Font("Malgun Gothic", 9f);
            Text = "그림 친구 - 캐릭터 그리기";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(870, 700);
            KeyPreview = true;

            // --- 윗줄: 이름 / 설정 ---
            AddLabel(this, "이름", 12, 15);
            txtName = new TextBox();
            txtName.SetBounds(50, 12, 150, 23);
            txtName.MaxLength = 20;
            Controls.Add(txtName);

            AddLabel(this, "애니 속도(FPS)", 215, 15);
            numFps = AddNum(this, 315, 12, 1, 30, 6);
            numFps.ValueChanged += delegate { previewTimer.Interval = 1000 / (int)numFps.Value; };

            AddLabel(this, "이동 속도", 375, 15);
            numSpeed = AddNum(this, 440, 12, 0, 10, 3);

            AddLabel(this, "크기", 500, 15);
            cmbScale = new ComboBox();
            cmbScale.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScale.Items.AddRange(new object[] { "1배", "2배", "3배", "4배" });
            cmbScale.SelectedIndex = 1;
            cmbScale.SetBounds(535, 12, 60, 23);
            Controls.Add(cmbScale);

            // --- 그림판 ---
            canvas = new DrawCanvas(this);
            canvas.SetBounds(12, 45, 512, 512);
            Controls.Add(canvas);

            // --- 프레임 목록 ---
            strip = new FlowLayoutPanel();
            strip.SetBounds(12, 565, 512, 92);
            strip.WrapContents = false;
            strip.AutoScroll = true;
            strip.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(strip);

            AddButton(this, "+ 새 프레임", 12, 662, 90, 28, delegate { AddFrame(false); });
            AddButton(this, "복제", 106, 662, 60, 28, delegate { AddFrame(true); });
            AddButton(this, "삭제", 170, 662, 60, 28, delegate { DeleteFrame(); });
            AddButton(this, "◀ 앞으로", 234, 662, 80, 28, delegate { MoveFrame(-1); });
            AddButton(this, "뒤로 ▶", 318, 662, 80, 28, delegate { MoveFrame(1); });
            lblFrame = AddLabel(this, "", 410, 668);

            // --- 오른쪽: 도구 ---
            int rx = 540, rw = 318;
            GroupBox gTool = AddGroup("도구", rx, 45, rw, 95);
            RadioButton rbPen = AddToolButton(gTool, "펜", 10, 20, Tool.Pen);
            AddToolButton(gTool, "지우개", 82, 20, Tool.Eraser);
            AddToolButton(gTool, "채우기", 154, 20, Tool.Fill);
            rbPen.Checked = true;
            AddLabel(gTool, "굵기", 10, 63);
            numBrush = AddNum(gTool, 45, 60, 1, 8, 2);
            AddLabel(gTool, "※ 우클릭하면 지우개", 110, 63);

            // --- 색 ---
            GroupBox gColor = AddGroup("색", rx, 145, rw, 130);
            for (int i = 0; i < Palette.Length; i++)
            {
                Panel sw = new Panel();
                sw.SetBounds(10 + (i % 8) * 34, 20 + (i / 8) * 32, 28, 28);
                sw.BackColor = Palette[i];
                sw.BorderStyle = BorderStyle.FixedSingle;
                sw.Cursor = Cursors.Hand;
                Color c = Palette[i];
                sw.Click += delegate { SetPenColor(c); };
                gColor.Controls.Add(sw);
            }
            AddLabel(gColor, "현재 색", 10, 96);
            colorNow = new Panel();
            colorNow.SetBounds(65, 90, 50, 28);
            colorNow.BorderStyle = BorderStyle.FixedSingle;
            colorNow.BackColor = PenColor;
            gColor.Controls.Add(colorNow);
            AddButton(gColor, "다른 색...", 125, 90, 90, 28, delegate { PickColor(); });

            // --- 보기 ---
            GroupBox gView = AddGroup("보기 / 움직임", rx, 280, rw, 80);
            chkOnion = AddCheck(gView, "어니언 스킨 (이전 프레임 흐리게 보기)", 10, 20, 290, true);
            chkGrid = AddCheck(gView, "격자", 10, 47, 70, true);
            chkFlip = AddCheck(gView, "왼쪽으로 갈 때 좌우 반전", 85, 47, 220, true);
            chkOnion.CheckedChanged += delegate { canvas.Invalidate(); };
            chkGrid.CheckedChanged += delegate { canvas.Invalidate(); };

            AddButton(this, "실행 취소 (Ctrl+Z)", rx, 368, 155, 30, delegate { Undo(); });
            AddButton(this, "이 프레임 지우기", rx + 163, 368, 155, 30, delegate { ClearFrame(); });

            // --- 미리보기 ---
            GroupBox gPrev = AddGroup("미리보기", rx, 405, rw, 160);
            preview = new PreviewBox(this);
            preview.SetBounds(10, 22, 128, 128);
            gPrev.Controls.Add(preview);
            Label help = AddLabel(gPrev, "캐릭터는 오른쪽을 보도록\n그려 주세요.\n\n왼쪽으로 걸을 때는\n자동으로 뒤집힙니다.", 150, 25);
            help.AutoSize = false;
            help.SetBounds(150, 25, 160, 120);

            // --- 저장 / 내보내기 ---
            AddButton(this, "불러오기", rx, 575, 155, 34, delegate { LoadCharacter(); });
            AddButton(this, "저장", rx + 163, 575, 155, 34, delegate { SaveCharacter(); });
            Button go = AddButton(this, "▶  화면에 내보내기", rx, 617, rw, 50, delegate { Launch(); });
            go.Font = new Font("Malgun Gothic", 12f, FontStyle.Bold);

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            previewTimer.Interval = 1000 / (int)numFps.Value;
            previewTimer.Tick += delegate
            {
                if (Frames.Count == 0) return;
                PreviewIndex = (PreviewIndex + 1) % Frames.Count;
                preview.Invalidate();
            };
            previewTimer.Start();

            RebuildStrip();
        }

        // ---------------- 컨트롤 생성 도우미 ----------------
        static Label AddLabel(Control parent, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        static NumericUpDown AddNum(Control parent, int x, int y, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            n.SetBounds(x, y, 50, 23);
            parent.Controls.Add(n);
            return n;
        }

        static Button AddButton(Control parent, string text, int x, int y, int w, int h, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.Click += click;
            parent.Controls.Add(b);
            return b;
        }

        static CheckBox AddCheck(Control parent, string text, int x, int y, int w, bool on)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = on;
            c.SetBounds(x, y, w, 24);
            parent.Controls.Add(c);
            return c;
        }

        GroupBox AddGroup(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.SetBounds(x, y, w, h);
            Controls.Add(g);
            return g;
        }

        RadioButton AddToolButton(Control parent, string text, int x, int y, Tool tool)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.Appearance = Appearance.Button;
            r.TextAlign = ContentAlignment.MiddleCenter;
            r.SetBounds(x, y, 68, 30);
            r.CheckedChanged += delegate { if (r.Checked) CurrentTool = tool; };
            parent.Controls.Add(r);
            return r;
        }

        // ---------------- 색 ----------------
        void SetPenColor(Color c)
        {
            PenColor = Px.Fix(c);
            colorNow.BackColor = PenColor;
        }

        void PickColor()
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.Color = PenColor;
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK) SetPenColor(dlg.Color);
            }
        }

        // ---------------- 프레임 관리 ----------------
        void RebuildStrip()
        {
            strip.SuspendLayout();
            while (strip.Controls.Count > 0)
            {
                Control c = strip.Controls[0];
                strip.Controls.RemoveAt(0);
                c.Dispose();
            }
            int t = (int)Math.Round(68 * dpi);
            for (int i = 0; i < Frames.Count; i++)
            {
                ThumbBox tb = new ThumbBox(this, i);
                tb.Size = new Size(t, t);
                tb.Margin = new Padding(3);
                strip.Controls.Add(tb);
            }
            strip.ResumeLayout();
            SelectFrame(Math.Min(Current, Frames.Count - 1));
        }

        internal void SelectFrame(int i)
        {
            if (i < 0 || i >= Frames.Count) return;
            Current = i;
            canvas.Invalidate();
            foreach (Control c in strip.Controls) c.Invalidate();
            if (i < strip.Controls.Count) strip.ScrollControlIntoView(strip.Controls[i]);
            lblFrame.Text = "프레임 " + (Current + 1) + " / " + Frames.Count;
        }

        internal void FrameEdited()
        {
            canvas.Invalidate();
            if (Current < strip.Controls.Count) strip.Controls[Current].Invalidate();
            preview.Invalidate();
        }

        void AddFrame(bool duplicate)
        {
            if (Frames.Count >= MaxFrames)
            {
                MessageBox.Show(this, "프레임은 최대 " + MaxFrames + "장까지 만들 수 있습니다.", Text);
                return;
            }
            Bitmap b = duplicate ? Px.Clone(Frames[Current]) : Px.NewFrame();
            Frames.Insert(Current + 1, b);
            Current++;
            ClearUndo();
            RebuildStrip();
        }

        void DeleteFrame()
        {
            if (Frames.Count == 1)
            {
                ClearFrame();
                return;
            }
            Frames[Current].Dispose();
            Frames.RemoveAt(Current);
            if (Current >= Frames.Count) Current = Frames.Count - 1;
            ClearUndo();
            RebuildStrip();
        }

        void MoveFrame(int delta)
        {
            int j = Current + delta;
            if (j < 0 || j >= Frames.Count) return;
            Bitmap tmp = Frames[Current];
            Frames[Current] = Frames[j];
            Frames[j] = tmp;
            Current = j;
            ClearUndo();
            RebuildStrip();
        }

        void ClearFrame()
        {
            PushUndo();
            using (Graphics g = Graphics.FromImage(Frames[Current])) g.Clear(Color.Transparent);
            FrameEdited();
        }

        // ---------------- 실행 취소 ----------------
        internal void PushUndo()
        {
            UndoItem u = new UndoItem();
            u.Frame = Current;
            u.Image = Px.Clone(Frames[Current]);
            undo.Add(u);
            if (undo.Count > MaxUndo)
            {
                undo[0].Image.Dispose();
                undo.RemoveAt(0);
            }
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            UndoItem u = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            if (u.Frame >= Frames.Count) { u.Image.Dispose(); return; }
            Frames[u.Frame].Dispose();
            Frames[u.Frame] = u.Image;
            SelectFrame(u.Frame);
            FrameEdited();
        }

        void ClearUndo()
        {
            foreach (UndoItem u in undo) u.Image.Dispose();
            undo.Clear();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !(ActiveControl is TextBoxBase))
            {
                Undo();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- 저장 / 불러오기 ----------------
        // 저장 형식: 프레임을 가로로 이어 붙인 PNG 한 장 (파일 이름 = 캐릭터 이름)
        static string SafeFileName(string s)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s;
        }

        void SaveCharacter()
        {
            string name = txtName.Text.Trim();
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG 그림 (*.png)|*.png";
                dlg.FileName = SafeFileName(name.Length > 0 ? name : "내 캐릭터") + ".png";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Bitmap sheet = new Bitmap(Px.N * Frames.Count, Px.N, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(sheet))
                        {
                            g.Clear(Color.Transparent);
                            g.CompositingMode = CompositingMode.SourceCopy;
                            for (int i = 0; i < Frames.Count; i++)
                                Px.DrawPixelArt(g, Frames[i], new Rectangle(i * Px.N, 0, Px.N, Px.N));
                        }
                        sheet.Save(dlg.FileName, ImageFormat.Png);
                    }
                    if (name.Length == 0) txtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "저장하지 못했습니다.\n" + ex.Message, Text);
                }
            }
        }

        void LoadCharacter()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "PNG 그림 (*.png)|*.png|그림 파일|*.png;*.bmp;*.gif;*.jpg";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Bitmap src;
                    using (FileStream fs = File.OpenRead(dlg.FileName))
                    using (Image tmp = Image.FromStream(fs))
                        src = new Bitmap(tmp);

                    List<Bitmap> loaded = new List<Bitmap>();
                    using (src)
                    {
                        int side = Math.Min(src.Width, src.Height);
                        int n = Math.Max(1, Math.Min(MaxFrames, src.Width / side));
                        for (int i = 0; i < n; i++)
                        {
                            Bitmap f = Px.NewFrame();
                            using (Graphics g = Graphics.FromImage(f))
                            {
                                g.CompositingMode = CompositingMode.SourceCopy;
                                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                                g.PixelOffsetMode = PixelOffsetMode.Half;
                                g.DrawImage(src, new Rectangle(0, 0, Px.N, Px.N), new Rectangle(i * side, 0, side, side), GraphicsUnit.Pixel);
                            }
                            // 반투명 픽셀은 완전 투명/불투명으로 정리 (캐릭터 테두리가 지저분해지지 않게)
                            for (int y = 0; y < Px.N; y++)
                                for (int x = 0; x < Px.N; x++)
                                {
                                    Color c = f.GetPixel(x, y);
                                    f.SetPixel(x, y, c.A < 128 ? Color.Transparent : Px.Fix(c));
                                }
                            loaded.Add(f);
                        }
                    }

                    foreach (Bitmap b in Frames) b.Dispose();
                    Frames.Clear();
                    Frames.AddRange(loaded);
                    Current = 0;
                    PreviewIndex = 0;
                    ClearUndo();
                    RebuildStrip();
                    txtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "불러오지 못했습니다.\n" + ex.Message, Text);
                }
            }
        }

        // ---------------- 화면에 내보내기 ----------------
        void Launch()
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "캐릭터 이름을 먼저 입력해 주세요.", Text);
                txtName.Focus();
                return;
            }

            List<Bitmap> copies = new List<Bitmap>();
            foreach (Bitmap f in Frames)
                if (!Px.IsEmpty(f)) copies.Add(Px.Clone(f));   // 빈 프레임은 건너뜀
            if (copies.Count == 0)
            {
                MessageBox.Show(this, "먼저 캐릭터를 그려 주세요.", Text);
                return;
            }

            PetForm pet = new PetForm(name, copies.ToArray(), (int)numFps.Value, (int)numSpeed.Value,
                                      cmbScale.SelectedIndex + 1, chkFlip.Checked, Bounds);
            Program.Pets.Add(pet);
            pet.Show();
        }

        // 캐릭터가 돌아다니는 중에 창을 닫으면 숨기기만 함 (캐릭터 우클릭 → 그리기 창 열기)
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing && Program.Pets.Count > 0)
            {
                e.Cancel = true;
                if (!hideNoticeShown)
                {
                    hideNoticeShown = true;
                    MessageBox.Show(this,
                        "그리기 창을 숨깁니다. 캐릭터는 계속 돌아다녀요.\n\n" +
                        "· 캐릭터 우클릭 → 그리기 창 열기\n" +
                        "· 캐릭터 더블클릭 → 그 캐릭터 닫기\n" +
                        "· 캐릭터 우클릭 → 모두 종료", Text);
                }
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            previewTimer.Stop();
            ClearUndo();
            base.OnFormClosed(e);
            if (Program.Editor == this) Program.Editor = null;   // 닫히는 중에도 Visible이 true일 수 있으므로 명시적으로 표시
            Program.CheckExit();
        }
    }

    // ------------------------------------------------------------------
    //  바탕화면 캐릭터 창
    //  투명 배경 + 항상 위. 작업 표시줄 위를 걷다가 가끔 쉬거나 방향을 바꿈.
    // ------------------------------------------------------------------
    sealed class PetForm : Form
    {
        enum State { Fall, Walk, Idle, Drag }

        readonly Bitmap[] frames;
        readonly Bitmap[] flipped;
        readonly string petName;
        readonly int fps;
        readonly double speedPx;    // 초당 이동 픽셀
        readonly double gravity;
        readonly bool flip;
        readonly int spriteSize;
        readonly int nameH;
        readonly Font nameFont;
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ContextMenuStrip menu;

        State state = State.Fall;
        double x, y, vy, stateLeft, animAcc;
        int dir = 1;
        int frame;
        long lastTicks;
        Point dragOffset;

        public PetForm(string name, Bitmap[] frames, int fps, int speed, int scale, bool flip, Rectangle spawnNear)
        {
            petName = name;
            this.frames = frames;
            this.fps = Math.Max(1, fps);
            this.flip = flip;
            flipped = new Bitmap[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                flipped[i] = Px.Clone(frames[i]);
                flipped[i].RotateFlip(RotateFlipType.RotateNoneFlipX);
            }

            float dpi;
            using (Graphics sg = Graphics.FromHwnd(IntPtr.Zero)) dpi = sg.DpiX / 96f;
            int pix = Math.Max(1, (int)Math.Round(scale * dpi));
            spriteSize = Px.N * pix;
            speedPx = speed * 20.0 * dpi;
            gravity = 1800.0 * dpi;

            nameFont = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Size ts = TextRenderer.MeasureText(name, nameFont);
            nameH = ts.Height + 2;
            int w = Math.Max(spriteSize, ts.Width + 12);

            Text = name;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Px.Key;
            TransparencyKey = Px.Key;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            ClientSize = new Size(w, nameH + spriteSize);

            // 그리기 창이 있는 화면 위쪽에서 떨어지며 등장
            Rectangle wa = Screen.FromRectangle(spawnNear).WorkingArea;
            x = wa.Left + Program.Rnd.Next(Math.Max(1, wa.Width - Width));
            y = wa.Top;
            dir = Program.Rnd.Next(2) == 0 ? -1 : 1;
            Location = new Point((int)x, (int)y);

            menu = new ContextMenuStrip();
            menu.Items.Add("그리기 창 열기", null, delegate { Program.ShowEditor(); });
            menu.Items.Add("방향 바꾸기", null, delegate { dir = -dir; Invalidate(); });
            menu.Items.Add(new ToolStripSeparator());
            // 메뉴 클릭 처리가 끝난 뒤에 닫도록 BeginInvoke 사용
            menu.Items.Add("이 캐릭터 닫기", null, delegate { BeginInvoke(new MethodInvoker(Close)); });
            menu.Items.Add("모두 종료", null, delegate { BeginInvoke(new MethodInvoker(Program.ExitAll)); });
            ContextMenuStrip = menu;

            timer.Interval = 30;
            timer.Tick += OnTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            lastTicks = clock.ElapsedTicks;
            timer.Start();
        }

        void ChangeState(State s)
        {
            state = s;
            if (s == State.Walk) stateLeft = 2.0 + Program.Rnd.NextDouble() * 4.0;
            else if (s == State.Idle) { stateLeft = 1.0 + Program.Rnd.NextDouble() * 2.0; frame = 0; }
        }

        void Animate(double dt)
        {
            if (frames.Length <= 1) return;
            animAcc += dt * fps;
            while (animAcc >= 1.0)
            {
                animAcc -= 1.0;
                frame = (frame + 1) % frames.Length;
            }
        }

        void OnTick(object sender, EventArgs e)
        {
            long now = clock.ElapsedTicks;
            double dt = (now - lastTicks) / (double)Stopwatch.Frequency;
            lastTicks = now;
            if (dt > 0.1) dt = 0.1;

            int prevFrame = frame, prevDir = dir;

            if (state == State.Drag)
            {
                Animate(dt);
            }
            else
            {
                Rectangle wa = Screen.FromPoint(new Point((int)x + Width / 2, (int)y + Height / 2)).WorkingArea;
                double ground = wa.Bottom - Height;
                double left = wa.Left;
                double right = Math.Max(left, wa.Right - Width);

                switch (state)
                {
                    case State.Fall:
                        vy += gravity * dt;
                        y += vy * dt;
                        if (y >= ground) { y = ground; vy = 0; ChangeState(State.Walk); }
                        break;

                    case State.Walk:
                        x += dir * speedPx * dt;
                        if (x <= left) { x = left; dir = 1; }
                        else if (x >= right) { x = right; dir = -1; }
                        stateLeft -= dt;
                        if (stateLeft <= 0)
                        {
                            int r = Program.Rnd.Next(100);
                            if (r < 25) ChangeState(State.Idle);           // 잠깐 쉬기
                            else
                            {
                                if (r < 45) dir = -dir;                     // 뒤돌기
                                ChangeState(State.Walk);
                            }
                        }
                        break;

                    case State.Idle:
                        stateLeft -= dt;
                        if (stateLeft <= 0)
                        {
                            if (Program.Rnd.Next(2) == 0) dir = -dir;
                            ChangeState(State.Walk);
                        }
                        break;
                }

                // 작업 표시줄 위치가 바뀌거나 다른 모니터로 옮겨졌을 때 바닥에 맞춤
                if (state != State.Fall)
                {
                    if (y < ground - 1) { state = State.Fall; vy = 0; }
                    else if (y > ground) y = ground;
                }
                if (x < left) x = left;
                if (x > right) x = right;

                if (state != State.Idle) Animate(dt);

                Point p = new Point((int)Math.Round(x), (int)Math.Round(y));
                if (p != Location) Location = p;
            }

            if (frame != prevFrame || dir != prevDir) Invalidate();
        }

        // ---------------- 마우스 ----------------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            state = State.Drag;
            Point c = Cursor.Position;
            dragOffset = new Point(c.X - Left, c.Y - Top);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (state != State.Drag) return;
            Point c = Cursor.Position;
            x = c.X - dragOffset.X;
            y = c.Y - dragOffset.Y;
            Location = new Point((int)x, (int)y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (state != State.Drag || e.Button != MouseButtons.Left) return;
            Capture = false;
            vy = 0;
            state = State.Fall;   // 놓으면 바닥으로 떨어짐
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) Close();
        }

        // ---------------- 그리기 ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Px.Key);

            // 이름표: 배경이 무엇이든 보이도록 검은 테두리 + 흰 글씨 (번짐 없는 글꼴 렌더링)
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            SizeF sz = g.MeasureString(petName, nameFont);
            float tx = (ClientSize.Width - sz.Width) / 2f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) g.DrawString(petName, nameFont, Brushes.Black, tx + dx, 1 + dy);
            g.DrawString(petName, nameFont, Brushes.White, tx, 1);

            Bitmap b = (flip && dir < 0) ? flipped[frame] : frames[frame];
            int sx = (ClientSize.Width - spriteSize) / 2;
            Px.DrawPixelArt(g, b, new Rectangle(sx, nameH, spriteSize, spriteSize));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            Program.Pets.Remove(this);
            foreach (Bitmap b in frames) b.Dispose();
            foreach (Bitmap b in flipped) b.Dispose();
            nameFont.Dispose();
            base.OnFormClosed(e);
            menu.Dispose();
            Program.CheckExit();
        }
    }
}
