using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace QingLi
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    /// <summary>扁平圆角按钮。</summary>
    public class UiButton : Control
    {
        private ButtonStyle _style = ButtonStyle.Secondary;
        private bool _hover;
        private bool _down;

        public ButtonStyle Style
        {
            get { return _style; }
            set { _style = value; Invalidate(); }
        }

        public UiButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FBody;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _down)
            {
                _down = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg, fg, border;
            switch (_style)
            {
                case ButtonStyle.Primary:
                    bg = _down ? Theme.AccentFill : (_hover ? Theme.AccentFillHover : Theme.AccentFill);
                    fg = Color.White;
                    border = Color.Transparent;
                    break;
                case ButtonStyle.Ghost:
                    bg = _down ? Theme.SurfaceHover : (_hover ? Theme.SurfaceHi : Color.Transparent);
                    fg = _hover ? Theme.TextPrimary : Theme.TextSecondary;
                    border = Color.Transparent;
                    break;
                case ButtonStyle.Danger:
                    bg = _down ? Theme.Mix(Theme.Surface, Theme.Danger, 0.35f) : (_hover ? Theme.Mix(Theme.Surface, Theme.Danger, 0.25f) : Theme.Surface);
                    fg = Theme.Danger;
                    border = Theme.Mix(Theme.Surface, Theme.Danger, 0.4f);
                    break;
                default:
                    bg = _down ? Theme.SurfaceHover : (_hover ? Theme.SurfaceHi : Theme.Surface);
                    fg = Theme.TextPrimary;
                    border = Theme.Border;
                    break;
            }

            int radius = Theme.S(6);
            if (bg != Color.Transparent) Theme.FillRound(g, r, radius, bg);
            if (border != Color.Transparent) Theme.DrawRound(g, r, radius, border, 1f);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.S(6), 0, Width - Theme.S(12), Height), fg, flags);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }

    /// <summary>分段控件（月 / 周 / 日）。</summary>
    public class Segmented : Control
    {
        private string[] _items = new string[0];
        private int _selected;

        public event EventHandler SelectedIndexChanged;

        public Segmented()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.FBody;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public string[] Items
        {
            get { return _items; }
            set { _items = value ?? new string[0]; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0) value = 0;
                if (_items.Length > 0 && value >= _items.Length) value = _items.Length - 1;
                if (_selected == value) return;
                _selected = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private int ItemWidth
        {
            get { return _items.Length == 0 ? Width : Width / _items.Length; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, Theme.S(7), Theme.Surface);
            Theme.DrawRound(g, r, Theme.S(7), Theme.Border, 1f);

            int iw = ItemWidth;
            for (int i = 0; i < _items.Length; i++)
            {
                bool sel = i == _selected;
                Rectangle ir = new Rectangle(Theme.S(3) + i * iw, Theme.S(3), iw - Theme.S(2), Height - Theme.S(7));
                if (sel)
                {
                    Theme.FillRound(g, ir, Theme.S(5), Theme.AccentSoft);
                    Theme.DrawRound(g, ir, Theme.S(5), Theme.Mix(Theme.AccentSoft, Theme.Accent, 0.45f), 1f);
                }
                Theme.TextC(g, _items[i], sel ? Theme.FBodyBold : Theme.FBody, sel ? Theme.Accent : Theme.TextSecondary, ir);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int iw = ItemWidth;
            if (iw > 0)
            {
                int idx = e.X / iw;
                if (idx >= 0 && idx < _items.Length) SelectedIndex = idx;
            }
            base.OnMouseDown(e);
        }
    }

    /// <summary>迷你月历：点击选日、左右切换月份、带事件小圆点。</summary>
    public class MiniMonth : Control
    {
        private DateTime _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _selected = DateTime.Today;
        private DateTime _hover = DateTime.MinValue;

        public HashSet<DateTime> Marked = new HashSet<DateTime>();
        public bool ShowHeader = true;
        public int HeaderHeight = 30;
        public int WeekdayHeight = 20;
        public int Rows = 6;

        public event Action<DateTime> DateClicked;
        public event Action<DateTime> MonthChanged;

        public MiniMonth()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.FSmall;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public DateTime Month
        {
            get { return _month; }
            set
            {
                _month = new DateTime(value.Year, value.Month, 1);
                Invalidate();
                if (MonthChanged != null) MonthChanged(_month);
            }
        }

        public DateTime SelectedDate
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        private DateTime FirstCell
        {
            get
            {
                int lead = ((int)_month.DayOfWeek + 6) % 7; // 周一为第一列
                return _month.AddDays(-lead);
            }
        }

        private void CellMetrics(out int cellW, out int cellH)
        {
            int top = ShowHeader ? Theme.S(HeaderHeight) + Theme.S(WeekdayHeight) : Theme.S(WeekdayHeight);
            cellW = Math.Max(1, Width / 7);
            cellH = Math.Max(1, (Height - top) / Rows);
        }

        public DateTime DateAt(Point p)
        {
            int cellW, cellH;
            CellMetrics(out cellW, out cellH);
            int top = ShowHeader ? Theme.S(HeaderHeight) + Theme.S(WeekdayHeight) : Theme.S(WeekdayHeight);
            if (p.Y < top) return DateTime.MinValue;
            int col = Math.Min(6, Math.Max(0, p.X / cellW));
            int row = Math.Min(Rows - 1, Math.Max(0, (p.Y - top) / cellH));
            return FirstCell.AddDays(row * 7 + col);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            int cellW, cellH;
            CellMetrics(out cellW, out cellH);
            int y = 0;
            if (ShowHeader)
            {
                Rectangle hr = new Rectangle(0, 0, Width, Theme.S(HeaderHeight));
                Rectangle left = new Rectangle(Theme.S(2), Theme.S(4), Theme.S(24), Theme.S(22));
                Rectangle right = new Rectangle(Width - Theme.S(26), Theme.S(4), Theme.S(24), Theme.S(22));
                Theme.TextC(g, "\u2039", Theme.FBodyBold, Theme.TextSecondary, left);
                Theme.TextC(g, "\u203A", Theme.FBodyBold, Theme.TextSecondary, right);
                Theme.TextC(g, string.Format("{0}年{1}月", _month.Year, _month.Month), Theme.FBodyBold, Theme.TextPrimary,
                    new Rectangle(Theme.S(28), 0, Width - Theme.S(56), Theme.S(HeaderHeight)));
                y = Theme.S(HeaderHeight);
            }

            string[] wd = new string[] { "一", "二", "三", "四", "五", "六", "日" };
            for (int i = 0; i < 7; i++)
            {
                Rectangle dr = new Rectangle(i * cellW, y, cellW, Theme.S(WeekdayHeight));
                Color c = (i >= 5) ? Theme.Weekend : Theme.TextMuted;
                Theme.TextC(g, wd[i], Theme.FSmall, c, dr);
            }
            y += Theme.S(WeekdayHeight);

            DateTime first = FirstCell;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    DateTime d = first.AddDays(r * 7 + c);
                    Rectangle cell = new Rectangle(c * cellW, y + r * cellH, cellW, cellH);
                    bool inMonth = d.Month == _month.Month;
                    bool isToday = d.Date == DateTime.Today;
                    bool isSel = d.Date == _selected.Date;

                    if (d.Date == _hover.Date && inMonth)
                        Theme.FillRound(g, new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2), Theme.S(4), Theme.SurfaceHi);

                    int dia = Math.Min(cellW, cellH) - Theme.S(6);
                    if (dia > Theme.S(24)) dia = Theme.S(24);
                    Rectangle circle = new Rectangle(cell.X + (cellW - dia) / 2, cell.Y + (cellH - dia) / 2, dia, dia);
                    if (isSel && !isToday) Theme.DrawRound(g, circle, dia / 2, Theme.Mix(Theme.Surface, Theme.Accent, 0.55f), 1f);
                    if (isToday) Theme.FillRound(g, circle, dia / 2, Theme.AccentFill);

                    Color tc;
                    if (isToday) tc = Color.White;
                    else if (!inMonth) tc = Theme.TextMuted;
                    else if (c >= 5) tc = Theme.Weekend;
                    else tc = Theme.TextPrimary;

                    Rectangle numBox = new Rectangle(circle.X + dia / 2 - Theme.S(17), circle.Y, Theme.S(34), dia);
                    Theme.TextC(g, d.Day.ToString(CultureInfo.InvariantCulture), Theme.FSmall, tc, numBox);

                    if (Marked.Contains(d.Date))
                    {
                        int dot = Theme.S(4);
                        Rectangle dr2 = new Rectangle(cell.X + cellW / 2 - dot / 2, cell.Bottom - Theme.S(5), dot, dot);
                        using (SolidBrush b = new SolidBrush(isToday ? Theme.Accent : Theme.Mix(Theme.TextSecondary, Theme.Surface, 0.15f)))
                            g.FillEllipse(b, dr2);
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            DateTime d = DateAt(e.Location);
            if (d != _hover) { _hover = d; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = DateTime.MinValue;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (ShowHeader)
            {
                if (e.Y < Theme.S(HeaderHeight))
                {
                    if (e.X < Theme.S(28)) Month = _month.AddMonths(-1);
                    else if (e.X > Width - Theme.S(30)) Month = _month.AddMonths(1);
                    return;
                }
            }
            DateTime d = DateAt(e.Location);
            if (d == DateTime.MinValue) return;
            SelectedDate = d;
            if (DateClicked != null) DateClicked(d);
            base.OnMouseDown(e);
        }
    }

    /// <summary>无边框弹出容器（点外部自动关闭）。</summary>
    public class PopupForm : Form
    {
        public PopupForm(Control content)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            Padding = new Padding(1);
            content.Dock = DockStyle.Fill;
            Controls.Add(content);
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.DrawRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Theme.S(6), Theme.BorderStrong, 1f);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }
    }

    /// <summary>点击弹出月历的日期输入框。</summary>
    public class DateField : Control
    {
        private DateTime _value = DateTime.Today;

        public event EventHandler ValueChanged;

        public DateField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.FBody;
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = Theme.S(32);
        }

        public DateTime Value
        {
            get { return _value; }
            set
            {
                if (value == _value) return;
                _value = value;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, Theme.S(6), Theme.Surface);
            Theme.DrawRound(g, r, Theme.S(6), Theme.Border, 1f);
            Theme.TextLeft(g, FormatDate(_value), Theme.FBody, Theme.TextPrimary,
                new Rectangle(Theme.S(10), 0, Width - Theme.S(30), Height));
            Theme.TextRight(g, "\u25BE", Theme.FSmall, Theme.TextMuted, new Rectangle(0, 0, Width - Theme.S(9), Height));
        }

        public static string WeekName(DateTime d)
        {
            string[] n = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return n[(int)d.DayOfWeek];
        }

        public static string FormatDate(DateTime d)
        {
            return string.Format("{0}年{1}月{2}日 {3}", d.Year, d.Month, d.Day, WeekName(d));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ShowPicker();
        }

        public void ShowPicker()
        {
            MiniMonth mm = new MiniMonth();
            mm.Month = _value;
            mm.SelectedDate = _value;
            mm.ShowHeader = true;
            mm.HeaderHeight = 32;
            mm.Dock = DockStyle.Fill;
            mm.DateClicked += delegate (DateTime d)
            {
                Value = d;
                Control p = Parent;
                Form f = FindForm();
                foreach (Form open in Application.OpenForms)
                {
                    if (open is PopupForm) { open.Close(); break; }
                }
            };
            PopupForm pop = new PopupForm(mm);
            pop.ClientSize = new Size(Theme.S(260), Theme.S(240));
            Point sp = PointToScreen(new Point(0, Height + Theme.S(4)));
            Rectangle scr = Screen.FromControl(this).WorkingArea;
            if (sp.Y + pop.Height > scr.Bottom) sp.Y = PointToScreen(new Point(0, 0)).Y - pop.Height - Theme.S(4);
            pop.Location = sp;
            pop.Show(FindForm());
            pop.BringToFront();
        }
    }

    /// <summary>深色下拉框（自绘）。</summary>
    public class FlatCombo : ComboBox
    {
        public FlatCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Surface;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FBody;
            ItemHeight = Theme.S(22);
            IntegralHeight = false;
            DropDownHeight = Theme.S(190);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool active = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            Color bg = sel ? Theme.AccentFill : (active ? Theme.Surface : Theme.SurfaceHi);
            using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);
            if (e.Index >= 0)
            {
                TextRenderer.DrawText(g, Items[e.Index].ToString(), Font,
                    new Rectangle(e.Bounds.X + Theme.S(8), e.Bounds.Y, e.Bounds.Width - Theme.S(10), e.Bounds.Height),
                    sel ? Color.White : Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>深色复选框（完全自绘）。</summary>
    public class FlatCheck : CheckBox
    {
        private bool _hover;

        public FlatCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FBody;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Window))
                e.Graphics.FillRectangle(b, 0, 0, Width, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            using (SolidBrush b = new SolidBrush(Theme.Window))
                g.FillRectangle(b, 0, 0, Width, Height);
            int box = Theme.S(16);
            Rectangle r = new Rectangle(0, (Height - box) / 2, box, box);
            Color bg = Checked ? Theme.AccentFill : (_hover ? Theme.SurfaceHover : Theme.Surface);
            Theme.FillRound(g, r, Theme.S(4), bg);
            Theme.DrawRound(g, r, Theme.S(4), Checked ? Theme.AccentFill : Theme.BorderStrong, 1f);
            if (Checked)
            {
                using (Pen p = new Pen(Color.White, Theme.SF(1.9f)))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new Point[]
                    {
                        new Point(r.X + Theme.S(3), r.Y + box / 2),
                        new Point(r.X + box / 2 - Theme.S(1), r.Bottom - Theme.S(4)),
                        new Point(r.Right - Theme.S(3), r.Y + Theme.S(4))
                    });
                }
            }
            Theme.TextLeft(g, Text, Font, ForeColor, new Rectangle(box + Theme.S(8), 0, Width - box - Theme.S(8), Height));
        }
    }

    /// <summary>圆角输入框外壳（包裹 TextBox，获得焦点时描边变亮）。</summary>
    public class FieldBox : Panel
    {
        public readonly Control Inner;

        public FieldBox(Control inner)
        {
            DoubleBuffered = true;
            BackColor = Theme.Window;
            Inner = inner;
            Controls.Add(inner);
            inner.GotFocus += delegate { Invalidate(); };
            inner.LostFocus += delegate { Invalidate(); };
            inner.TextChanged += delegate { Invalidate(); };
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int padX = Theme.S(10);
            int padY = Theme.S(6);
            Inner.SetBounds(padX, padY, Math.Max(10, Width - padX * 2), Math.Max(10, Height - padY * 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, Theme.S(6), Theme.Surface);
            Theme.DrawRound(g, r, Theme.S(6), Inner.Focused ? Theme.Accent : Theme.Border, 1f);
        }
    }

    /// <summary>深色单行/多行文本框。</summary>
    public class FlatTextBox : TextBox
    {
        private const int WM_PAINT = 0x000F;
        public string Placeholder = "";

        public FlatTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FBody;
            ShortcutsEnabled = true;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && Placeholder.Length > 0 && Text.Length == 0 && !Focused)
            {
                using (Graphics g = CreateGraphics())
                {
                    TextRenderer.DrawText(g, Placeholder, Font,
                        new Rectangle(2, 0, Math.Max(10, Width - 4), Height), Theme.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
            }
        }
    }

    internal static class Ui
    {
        public static Label Caption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Theme.TextMuted;
            l.BackColor = Color.Transparent;
            l.Font = Theme.FSmall;
            return l;
        }

        public static FieldBox Input(string text, bool multiline)
        {
            FlatTextBox tb = new FlatTextBox();
            tb.Text = text ?? "";
            if (multiline)
            {
                tb.Multiline = true;
                tb.ScrollBars = ScrollBars.Vertical;
                tb.AcceptsReturn = true;
            }
            FieldBox box = new FieldBox(tb);
            return box;
        }

        public static void StyleForm(Form f)
        {
            f.BackColor = Theme.Window;
            f.ForeColor = Theme.TextPrimary;
            f.Font = Theme.FBody;
            f.AutoScaleMode = AutoScaleMode.None;
        }
    }
}
