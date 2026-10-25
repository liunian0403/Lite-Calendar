using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>月视图：6 行 7 列，事件色块、今天高亮、双击新建。</summary>
    public class MonthView : Control
    {
        public EventStore Store;
        public DateTime AnchorDate = DateTime.Today;
        public DateTime SelectedDate = DateTime.Today;

        /// <summary>节假日数据源（可为 null，此时不显示节假日信息）。</summary>
        public HolidayService Holidays;

        public event Action<Occurrence> EventClicked;
        public event Action<DateTime> DayDoubleClicked;
        public event Action<DateTime, Occurrence, Point> DayRightClicked;
        public event Action<DateTime> SelectionChanged;

        private readonly Dictionary<DateTime, List<Occurrence>> _byDay = new Dictionary<DateTime, List<Occurrence>>();
        private readonly List<KeyValuePair<Rectangle, Occurrence>> _chips = new List<KeyValuePair<Rectangle, Occurrence>>();
        private DateTime _hoverDate = DateTime.MinValue;
        private DateTime _firstCell;
        private bool _dirty = true;

        public MonthView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.FBody;
            TabStop = true;
        }

        public DateTime MonthStart
        {
            get { return new DateTime(AnchorDate.Year, AnchorDate.Month, 1); }
        }

        public void Reload()
        {
            _dirty = true;
            Invalidate();
        }

        private void EnsureData()
        {
            if (!_dirty) return;
            _dirty = false;
            _byDay.Clear();
            _chips.Clear();
            if (Store == null) return;

            int lead = ((int)MonthStart.DayOfWeek + 6) % 7;
            _firstCell = MonthStart.AddDays(-lead);
            DateTime from = _firstCell;
            DateTime to = _firstCell.AddDays(42);
            List<Occurrence> list = Store.ExpandRange(from, to);
            for (int i = 0; i < list.Count; i++)
            {
                Occurrence o = list[i];
                DateTime d = o.Start.Date;
                DateTime last = o.EffectiveEnd.Date;
                if (o.AllDay && o.EffectiveEnd.TimeOfDay == TimeSpan.Zero) last = last.AddDays(-1);
                if (last < d) last = d;
                for (DateTime cur = d; cur <= last; cur = cur.AddDays(1))
                {
                    List<Occurrence> bucket;
                    if (!_byDay.TryGetValue(cur, out bucket))
                    {
                        bucket = new List<Occurrence>();
                        _byDay[cur] = bucket;
                    }
                    bucket.Add(o);
                }
            }
        }

        private int HeaderH { get { return Theme.S(30); } }
        private int CellW { get { return Math.Max(1, Width / 7); } }
        private int CellH { get { return Math.Max(1, (Height - HeaderH) / 6); } }

        public DateTime DateAt(Point p)
        {
            if (p.Y < HeaderH) return DateTime.MinValue;
            int col = Math.Min(6, Math.Max(0, p.X / CellW));
            int row = Math.Min(5, Math.Max(0, (p.Y - HeaderH) / CellH));
            EnsureData();
            return _firstCell.AddDays(row * 7 + col);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureData();
            _chips.Clear();
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Theme.Window);

            // 星期表头
            string[] wd = new string[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            for (int i = 0; i < 7; i++)
            {
                Rectangle hr = new Rectangle(i * CellW, 0, CellW, HeaderH);
                Color c = i >= 5 ? Theme.Weekend : Theme.TextSecondary;
                Theme.TextC(g, wd[i], Theme.FSmall, c, hr);
            }

            int cw = CellW;
            int ch = CellH;
            DateTime today = DateTime.Today;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    DateTime d = _firstCell.AddDays(r * 7 + c);
                    Rectangle cell = new Rectangle(c * cw + Theme.S(2), HeaderH + r * ch + Theme.S(2), cw - Theme.S(4), ch - Theme.S(4));
                    bool inMonth = d.Month == MonthStart.Month;
                    bool isToday = d.Date == today;
                    bool isSel = d.Date == SelectedDate.Date;

                    Color bg = inMonth ? Theme.Surface : Theme.Mix(Theme.Window, Theme.Surface, 0.35f);
                    if (d.Date == _hoverDate && inMonth) bg = Theme.SurfaceHi;
                    if (isSel) bg = Theme.Mix(bg, Theme.Accent, 0.10f);
                    Theme.FillRound(g, cell, Theme.S(6), bg);
                    if (isSel) Theme.DrawRound(g, cell, Theme.S(6), Theme.Mix(Theme.Accent, Theme.Surface, 0.35f), 1.4f);
                    else if (inMonth) Theme.DrawRound(g, cell, Theme.S(6), Theme.Mix(Theme.Surface, Theme.Border, 0.5f), 1f);

                    // 日期数字（文字框比圆圈宽，避免两位数被省略号截断）
                    int dia = Theme.S(24);
                    Rectangle circle = new Rectangle(cell.X + Theme.S(5), cell.Y + Theme.S(4), dia, dia);
                    HolidayInfo hi = (Holidays != null && inMonth) ? Holidays.GetDay(d) : null;
                    bool isHoliday = hi != null && hi.IsOffDay;
                    bool isMakeup = hi != null && !hi.IsOffDay;
                    if (isToday) Theme.FillRound(g, circle, dia / 2, Theme.AccentFill);
                    Color numColor = isToday ? Color.White : (inMonth ? Theme.TextPrimary : Theme.TextMuted);
                    if (!isToday && inMonth)
                    {
                        // 调休上班的周末按工作日着色；普通周末用休息色；法定假日用节日色
                        if (isMakeup) numColor = Theme.TextPrimary;
                        else if (isHoliday) numColor = Theme.Holiday;
                        else if (c >= 5) numColor = Theme.Weekend;
                    }
                    Rectangle numBox = new Rectangle(circle.X + dia / 2 - Theme.S(22), circle.Y, Theme.S(44), dia);
                    Theme.TextC(g, d.Day.ToString(CultureInfo.InvariantCulture), isToday ? Theme.FBodyBold : Theme.FBody, numColor, numBox);

                    // 节假日 / 调休徽标（休：节日色底；班：中性底），画在日期圆圈右侧
                    if (inMonth && (isHoliday || isMakeup))
                    {
                        string tag = isHoliday ? "休" : "班";
                        Color tagBg = isHoliday ? Theme.Mix(Theme.Surface, Theme.Holiday, 0.22f) : Theme.WorkdayBadge;
                        Color tagFg = isHoliday ? Theme.Holiday : Theme.TextSecondary;
                        Rectangle badge = new Rectangle(circle.Right + Theme.S(3), circle.Y + Theme.S(4), Theme.S(16), Theme.S(16));
                        Theme.FillRound(g, badge, Theme.S(4), tagBg);
                        Theme.TextC(g, tag, Theme.FSmall, tagFg, badge);

                        // 节日名称（空间足够时显示）
                        if (isHoliday && cell.Width > Theme.S(96))
                        {
                            Rectangle nameBox = new Rectangle(badge.Right + Theme.S(2), circle.Y, cell.Right - badge.Right - Theme.S(6), dia);
                            Theme.TextLeft(g, hi.Name, Theme.FSmall, Theme.Mix(Theme.TextSecondary, Theme.Holiday, 0.5f), nameBox);
                        }
                    }

                    // 事件色块
                    List<Occurrence> items;
                    if (_byDay.TryGetValue(d.Date, out items) && items.Count > 0)
                    {
                        int chipH = Theme.S(17);
                        int gap = Theme.S(3);
                        int top = circle.Bottom + Theme.S(3);
                        int bottom = cell.Bottom - Theme.S(3);
                        int capacity = Math.Max(0, (bottom - top + gap) / (chipH + gap));
                        int show = Math.Min(capacity, items.Count);
                        bool needMore = items.Count > show;
                        if (needMore) show = Math.Max(0, show - 1);

                        for (int i = 0; i < show; i++)
                        {
                            Rectangle chip = new Rectangle(cell.X + Theme.S(4), top + i * (chipH + gap), cell.Width - Theme.S(8), chipH);
                            DrawChip(g, chip, items[i], inMonth);
                            _chips.Add(new KeyValuePair<Rectangle, Occurrence>(chip, items[i]));
                        }
                        if (needMore)
                        {
                            Rectangle more = new Rectangle(cell.X + Theme.S(7), top + show * (chipH + gap), cell.Width - Theme.S(10), chipH);
                            Theme.TextLeft(g, string.Format("还有 {0} 项", items.Count - show), Theme.FSmall, Theme.TextMuted, more);
                        }
                    }
                }
            }
        }

        private void DrawChip(Graphics g, Rectangle chip, Occurrence o, bool inMonth)
        {
            Color color = Theme.EventColor(o.ColorIndex);
            Color bg = Theme.Mix(Theme.Surface, color, inMonth ? 0.20f : 0.10f);
            Theme.FillRound(g, chip, Theme.S(4), bg);

            Rectangle bar = new Rectangle(chip.X, chip.Y + Theme.S(3), Theme.S(3), chip.Height - Theme.S(6));
            Theme.FillRound(g, bar, Theme.S(2), inMonth ? color : Theme.Mix(color, Theme.Surface, 0.5f));

            string text;
            if (o.AllDay) text = o.Title;
            else text = o.Start.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + o.Title;

            Color fg = inMonth ? Theme.Mix(Theme.TextPrimary, color, 0.15f) : Theme.TextMuted;
            Theme.TextLeft(g, text, Theme.FSmall, fg, new Rectangle(chip.X + Theme.S(8), chip.Y, chip.Width - Theme.S(10), chip.Height));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            DateTime d = DateAt(e.Location);
            if (d != _hoverDate)
            {
                _hoverDate = d;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverDate = DateTime.MinValue;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private Occurrence ChipAt(Point p)
        {
            for (int i = 0; i < _chips.Count; i++)
                if (_chips[i].Key.Contains(p)) return _chips[i].Value;
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Right)
            {
                DateTime rd = DateAt(e.Location);
                if (rd == DateTime.MinValue) return;
                Occurrence ro = ChipAt(e.Location);
                if (ro != null) SelectedDate = rd;
                else SelectedDate = rd;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(SelectedDate);
                if (DayRightClicked != null) DayRightClicked(rd, ro, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left) return;
            Occurrence o = ChipAt(e.Location);
            if (o != null)
            {
                SelectedDate = o.Start.Date;
                if (SelectionChanged != null) SelectionChanged(SelectedDate);
                if (EventClicked != null) EventClicked(o);
                return;
            }
            DateTime d = DateAt(e.Location);
            if (d == DateTime.MinValue) return;
            SelectedDate = d;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(SelectedDate);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            Occurrence o = ChipAt(e.Location);
            if (o != null)
            {
                if (EventClicked != null) EventClicked(o);
                return;
            }
            DateTime d = DateAt(e.Location);
            if (d == DateTime.MinValue) return;
            SelectedDate = d;
            if (DayDoubleClicked != null) DayDoubleClicked(d);
        }

        public void MoveSelection(int days)
        {
            SelectedDate = SelectedDate.AddDays(days);
            if (SelectedDate.Month != AnchorDate.Month || SelectedDate.Year != AnchorDate.Year)
                AnchorDate = SelectedDate;
            _dirty = true;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(SelectedDate);
        }
    }
}
