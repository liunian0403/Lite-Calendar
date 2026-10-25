using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>周 / 日视图共用的时间网格：可滚动、双击空白新建、现在时刻指示线。</summary>
    public class TimeGridView : Control
    {
        public EventStore Store;
        public DateTime AnchorDate = DateTime.Today;
        public int DayCount = 7;
        public DateTime SelectedDate = DateTime.Today;

        public event Action<Occurrence> EventClicked;
        public event Action<DateTime> SlotDoubleClicked;
        public event Action<DateTime, Occurrence, Point> EventRightClicked;
        public event Action<DateTime> DateHeaderClicked;

        private readonly Dictionary<DateTime, List<Occurrence>> _byDay = new Dictionary<DateTime, List<Occurrence>>();
        private readonly List<KeyValuePair<Rectangle, Occurrence>> _blocks = new List<KeyValuePair<Rectangle, Occurrence>>();
        private readonly List<KeyValuePair<Rectangle, Occurrence>> _allDayChips = new List<KeyValuePair<Rectangle, Occurrence>>();
        private int _scrollY;
        private bool _dragging;
        private int _dragOffset;
        private bool _dirty = true;
        private DateTime _hoverSlot = DateTime.MinValue;

        public TimeGridView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.FBody;
            TabStop = true;
        }

        public DateTime FirstDay
        {
            get
            {
                if (DayCount == 1) return AnchorDate.Date;
                int lead = ((int)AnchorDate.DayOfWeek + 6) % 7;
                return AnchorDate.Date.AddDays(-lead);
            }
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
            if (Store == null) return;
            DateTime from = FirstDay;
            DateTime to = from.AddDays(DayCount);
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

        private int HeaderH { get { return Theme.S(52); } }
        private int HourH { get { return Theme.S(48); } }
        private int GutterW { get { return Theme.S(52); } }
        private int ScrollBarW { get { return Theme.S(8); } }
        private int ContentH { get { return 24 * HourH; } }

        private int AllDayH
        {
            get
            {
                EnsureData();
                int max = 0;
                DateTime d0 = FirstDay;
                for (int i = 0; i < DayCount; i++)
                {
                    List<Occurrence> items;
                    if (_byDay.TryGetValue(d0.AddDays(i), out items))
                    {
                        int n = 0;
                        for (int j = 0; j < items.Count; j++) if (items[j].AllDay) n++;
                        if (n > max) max = n;
                    }
                }
                if (max > 2) max = 2;
                return max == 0 ? 0 : max * Theme.S(21) + Theme.S(4);
            }
        }

        private int GridTop { get { return HeaderH + AllDayH; } }

        private int ViewportH
        {
            get { return Math.Max(1, Height - GridTop); }
        }

        private int MaxScroll
        {
            get { return Math.Max(0, ContentH - ViewportH); }
        }

        private int ColW
        {
            get { return Math.Max(1, (Width - GutterW - ScrollBarW) / DayCount); }
        }

        private void ClampScroll()
        {
            if (_scrollY > MaxScroll) _scrollY = MaxScroll;
            if (_scrollY < 0) _scrollY = 0;
        }

        public void ScrollToTime(DateTime time)
        {
            int y = (int)(time.TimeOfDay.TotalMinutes * (HourH / 60f));
            _scrollY = Math.Max(0, y - ViewportH / 3);
            ClampScroll();
            Invalidate();
        }

        public void ScrollBy(int dy)
        {
            _scrollY += dy;
            ClampScroll();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureData();
            _blocks.Clear();
            _allDayChips.Clear();
            ClampScroll();

            Graphics g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Theme.Window);

            int colW = ColW;
            DateTime first = FirstDay;
            DateTime today = DateTime.Today;

            // ---- 表头 ----
            for (int i = 0; i < DayCount; i++)
            {
                DateTime d = first.AddDays(i);
                Rectangle hr = new Rectangle(GutterW + i * colW, 0, colW, HeaderH);
                bool isToday = d == today;
                bool isSel = d == SelectedDate.Date;
                if (isSel) Theme.FillRound(g, new Rectangle(hr.X + Theme.S(2), Theme.S(4), hr.Width - Theme.S(4), hr.Height - Theme.S(8)), Theme.S(6), Theme.Surface);

                string[] wd = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
                Color wc = isToday ? Theme.Accent : (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday ? Theme.Weekend : Theme.TextSecondary);
                Theme.TextC(g, wd[(int)d.DayOfWeek], Theme.FSmall, wc, new Rectangle(hr.X, Theme.S(4), hr.Width, Theme.S(18)));

                int dia = Theme.S(26);
                Rectangle circle = new Rectangle(hr.X + (colW - dia) / 2, Theme.S(22), dia, dia);
                if (isToday) Theme.FillRound(g, circle, dia / 2, Theme.AccentFill);
                Theme.TextC(g, d.Day.ToString(CultureInfo.InvariantCulture), isToday ? Theme.FBodyBold : Theme.FBody,
                    isToday ? Color.White : Theme.TextPrimary, circle);
            }

            // ---- 全天事件条 ----
            int adh = AllDayH;
            if (adh > 0)
            {
                for (int i = 0; i < DayCount; i++)
                {
                    DateTime d = first.AddDays(i);
                    List<Occurrence> items;
                    if (!_byDay.TryGetValue(d, out items)) continue;
                    int slot = 0;
                    for (int j = 0; j < items.Count && slot < 2; j++)
                    {
                        if (!items[j].AllDay) continue;
                        Rectangle chip = new Rectangle(GutterW + i * colW + Theme.S(2), HeaderH + Theme.S(2) + slot * Theme.S(21), colW - Theme.S(4), Theme.S(18));
                        Color color = Theme.EventColor(items[j].ColorIndex);
                        Theme.FillRound(g, chip, Theme.S(4), Theme.Mix(Theme.Surface, color, 0.22f));
                        Theme.FillRound(g, new Rectangle(chip.X, chip.Y + Theme.S(3), Theme.S(3), chip.Height - Theme.S(6)), Theme.S(2), color);
                        Theme.TextLeft(g, items[j].Title, Theme.FSmall, Theme.Mix(Theme.TextPrimary, color, 0.12f),
                            new Rectangle(chip.X + Theme.S(8), chip.Y, chip.Width - Theme.S(10), chip.Height));
                        _allDayChips.Add(new KeyValuePair<Rectangle, Occurrence>(chip, items[j]));
                        slot++;
                    }
                }
                using (Pen p = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.6f)))
                    g.DrawLine(p, 0, GridTop - 1, Width, GridTop - 1);
            }

            // ---- 时间网格 ----
            int gridTop = GridTop;
            Region oldClip = g.Clip;
            g.SetClip(new Rectangle(0, gridTop, Width, Height - gridTop), CombineMode.Intersect);

            float pxPerMin = HourH / 60f;

            // 小时线 + 刻度
            for (int h = 0; h <= 24; h++)
            {
                int y = gridTop + (int)(h * HourH) - _scrollY;
                if (y < gridTop - HourH || y > Height + HourH) continue;
                if (h < 24)
                {
                    using (Pen p = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.75f)))
                        g.DrawLine(p, GutterW, y, Width - ScrollBarW, y);
                    int half = y + HourH / 2;
                    using (Pen p2 = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.35f)))
                    {
                        p2.DashStyle = DashStyle.Dot;
                        g.DrawLine(p2, GutterW, half, Width - ScrollBarW, half);
                    }
                }
                if (h < 24)
                {
                    Theme.TextRight(g, string.Format("{0:00}:00", h), Theme.FTimeSmall, Theme.TextMuted,
                        new Rectangle(0, y - Theme.S(8), GutterW - Theme.S(8), Theme.S(16)));
                }
            }

            // 列分隔 + 今天/选中列底色
            for (int i = 0; i < DayCount; i++)
            {
                DateTime d = first.AddDays(i);
                Rectangle col = new Rectangle(GutterW + i * colW, gridTop, colW, Height - gridTop);
                if (d == today) Theme.FillRound(g, new Rectangle(col.X, gridTop, col.Width, col.Height), 0, Theme.Alpha(Theme.TodayFill, 90));
                if (i > 0)
                {
                    using (Pen p = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.6f)))
                        g.DrawLine(p, col.X, gridTop, col.X, Height);
                }
            }

            // 事件块
            for (int i = 0; i < DayCount; i++)
            {
                DateTime d = first.AddDays(i);
                List<Occurrence> items;
                if (!_byDay.TryGetValue(d, out items)) continue;

                List<Occurrence> timed = new List<Occurrence>();
                for (int j = 0; j < items.Count; j++) if (!items[j].AllDay) timed.Add(items[j]);
                if (timed.Count == 0) continue;
                timed.Sort(delegate (Occurrence a, Occurrence b) { return a.Start.CompareTo(b.Start); });

                List<List<Occurrence>> columns = new List<List<Occurrence>>();
                List<DateTime> colEnd = new List<DateTime>();
                for (int j = 0; j < timed.Count; j++)
                {
                    Occurrence o = timed[j];
                    int target = -1;
                    for (int c = 0; c < columns.Count; c++)
                    {
                        if (colEnd[c] <= o.Start) { target = c; break; }
                    }
                    if (target < 0)
                    {
                        columns.Add(new List<Occurrence>());
                        colEnd.Add(o.End);
                        target = columns.Count - 1;
                    }
                    columns[target].Add(o);
                    if (o.End > colEnd[target]) colEnd[target] = o.End;
                }

                int total = columns.Count;
                int dayX = GutterW + i * colW;
                for (int c = 0; c < columns.Count; c++)
                {
                    int bw = Math.Max(Theme.S(16), (colW - Theme.S(6)) / total);
                    for (int k = 0; k < columns[c].Count; k++)
                    {
                        Occurrence o = columns[c][k];
                        int top = gridTop + (int)(o.Start.TimeOfDay.TotalMinutes * pxPerMin) - _scrollY;
                        int height = Math.Max(Theme.S(18), (int)(o.Duration.TotalMinutes * pxPerMin) - Theme.S(1));
                        Rectangle block = new Rectangle(dayX + Theme.S(3) + c * bw, top + 1, bw - Theme.S(2), height);
                        if (block.Bottom < gridTop || block.Y > Height) continue;
                        DrawBlock(g, block, o);
                        _blocks.Add(new KeyValuePair<Rectangle, Occurrence>(block, o));
                    }
                }
            }

            // 现在时刻指示线
            if (today >= first && today < first.AddDays(DayCount))
            {
                int idx = (int)(today - first).TotalDays;
                int y = gridTop + (int)(DateTime.Now.TimeOfDay.TotalMinutes * pxPerMin) - _scrollY;
                if (y > gridTop - 2 && y < Height)
                {
                    int x1 = GutterW + idx * colW;
                    using (Pen p = new Pen(Theme.NowLine, 1.4f))
                        g.DrawLine(p, x1, y, x1 + colW, y);
                    using (SolidBrush b = new SolidBrush(Theme.NowLine))
                        g.FillEllipse(b, x1 - Theme.S(3), y - Theme.S(3), Theme.S(6), Theme.S(6));
                    Theme.TextRight(g, DateTime.Now.ToString("HH:mm"), Theme.FTimeSmall, Theme.NowLine,
                        new Rectangle(0, y - Theme.S(8), GutterW - Theme.S(8), Theme.S(16)));
                }
            }

            g.Clip = oldClip;

            // 滚动条
            if (MaxScroll > 0)
            {
                int trackH = ViewportH;
                int thumbH = Math.Max(Theme.S(30), (int)((float)trackH * ViewportH / ContentH));
                int thumbY = gridTop + (int)((float)(trackH - thumbH) * _scrollY / MaxScroll);
                Rectangle track = new Rectangle(Width - ScrollBarW + Theme.S(1), gridTop + Theme.S(2), Theme.S(4), trackH - Theme.S(4));
                Theme.FillRound(g, track, Theme.S(2), Theme.Mix(Theme.Window, Theme.Border, 0.5f));
                Rectangle thumb = new Rectangle(track.X, thumbY, track.Width, thumbH);
                Theme.FillRound(g, thumb, Theme.S(2), _dragging ? Theme.TextSecondary : Theme.BorderStrong);
            }
        }

        private void DrawBlock(Graphics g, Rectangle block, Occurrence o)
        {
            Color color = Theme.EventColor(o.ColorIndex);
            Color bg = Theme.Mix(Theme.Window, color, 0.24f);
            Theme.FillRound(g, block, Theme.S(5), bg);
            Theme.DrawRound(g, block, Theme.S(5), Theme.Mix(Theme.Window, color, 0.5f), 1f);
            Theme.FillRound(g, new Rectangle(block.X, block.Y + Theme.S(2), Theme.S(3), Math.Max(Theme.S(4), block.Height - Theme.S(4))), Theme.S(2), color);

            int textX = block.X + Theme.S(7);
            int textW = block.Width - Theme.S(10);
            Color fg = Theme.Mix(Theme.TextPrimary, color, 0.10f);
            if (block.Height >= Theme.S(34))
            {
                Theme.TextLeft(g, o.Title, Theme.FSmall, fg, new Rectangle(textX, block.Y + Theme.S(2), textW, Theme.S(16)));
                Theme.TextLeft(g, o.Start.ToString("HH:mm", CultureInfo.InvariantCulture) + " - " + o.End.ToString("HH:mm", CultureInfo.InvariantCulture),
                    Theme.FTimeSmall, Theme.TextSecondary, new Rectangle(textX, block.Y + Theme.S(16), textW, Theme.S(14)));
            }
            else
            {
                Theme.TextLeft(g, o.Start.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + o.Title, Theme.FSmall, fg,
                    new Rectangle(textX, block.Y, textW, block.Height));
            }
        }

        private DateTime TimeAt(Point p)
        {
            if (p.Y < GridTop) return DateTime.MinValue;
            float pxPerMin = HourH / 60f;
            double minutes = (p.Y - GridTop + _scrollY) / pxPerMin;
            if (minutes < 0) minutes = 0;
            if (minutes > 24 * 60) minutes = 24 * 60;
            int snapped = (int)Math.Round(minutes / 30.0) * 30;
            if (snapped >= 24 * 60) snapped = 23 * 60 + 30;
            return DateTime.Today.AddMinutes(snapped);
        }

        private Occurrence BlockAt(Point p)
        {
            for (int i = _blocks.Count - 1; i >= 0; i--)
                if (_blocks[i].Key.Contains(p)) return _blocks[i].Value;
            for (int i = _allDayChips.Count - 1; i >= 0; i--)
                if (_allDayChips[i].Key.Contains(p)) return _allDayChips[i].Value;
            return null;
        }

        private int DayIndexAt(int x)
        {
            if (x < GutterW) return -1;
            int idx = (x - GutterW) / ColW;
            if (idx < 0 || idx >= DayCount) return -1;
            return idx;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            // 滚动条拖动
            if (MaxScroll > 0 && e.X > Width - ScrollBarW - Theme.S(2))
            {
                _dragging = true;
                _dragOffset = e.Y;
                Invalidate();
                return;
            }

            if (e.Y < HeaderH)
            {
                int idx = DayIndexAt(e.X);
                if (idx >= 0 && e.Button == MouseButtons.Left)
                {
                    DateTime d = FirstDay.AddDays(idx);
                    SelectedDate = d;
                    Invalidate();
                    if (DateHeaderClicked != null) DateHeaderClicked(d);
                }
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                Occurrence o = BlockAt(e.Location);
                DateTime d = DateTime.MinValue;
                int idx = DayIndexAt(e.X);
                if (o != null) d = o.Start.Date;
                else if (idx >= 0) d = FirstDay.AddDays(idx);
                if (d != DateTime.MinValue && EventRightClicked != null) EventRightClicked(d, o, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left) return;
            Occurrence hit = BlockAt(e.Location);
            if (hit != null)
            {
                SelectedDate = hit.Start.Date;
                Invalidate();
                if (EventClicked != null) EventClicked(hit);
                return;
            }
            int di = DayIndexAt(e.X);
            if (di >= 0)
            {
                SelectedDate = FirstDay.AddDays(di);
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                int trackH = ViewportH;
                int thumbH = Math.Max(Theme.S(30), (int)((float)trackH * ViewportH / ContentH));
                int usable = Math.Max(1, trackH - thumbH);
                int delta = e.Y - _dragOffset;
                _scrollY += (int)((float)delta * MaxScroll / usable);
                _dragOffset = e.Y;
                ClampScroll();
                Invalidate();
                return;
            }
            DateTime slot = TimeAt(e.Location);
            if (slot != _hoverSlot) { _hoverSlot = slot; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (BlockAt(e.Location) != null) return;
            int idx = DayIndexAt(e.X);
            if (idx < 0) return;
            DateTime day = FirstDay.AddDays(idx);
            DateTime t = TimeAt(e.Location);
            if (t == DateTime.MinValue) return;
            DateTime start = day.Date.Add(t.TimeOfDay);
            SelectedDate = day;
            if (SlotDoubleClicked != null) SlotDoubleClicked(start);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = (e.Delta / 120) * Theme.S(64);
            ScrollBy(-step);
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { ScrollBy(Theme.S(64)); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { ScrollBy(-Theme.S(64)); e.Handled = true; }
            else if (e.KeyCode == Keys.PageDown) { ScrollBy(ViewportH); e.Handled = true; }
            else if (e.KeyCode == Keys.PageUp) { ScrollBy(-ViewportH); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }
}
