using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Media;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>右下角提醒气泡（自绘、不抢焦点、可稍后提醒）。</summary>
    public class ToastForm : Form
    {
        private readonly Occurrence _occ;
        private readonly Timer _life;
        private Rectangle _okRect;
        private Rectangle _snoozeRect;
        private bool _hoverOk;
        private bool _hoverSnooze;

        private static readonly List<ToastForm> Open = new List<ToastForm>();

        public event Action<Occurrence> OpenRequested;
        public event Action<Occurrence> SnoozeRequested;

        public ToastForm(Occurrence occ)
        {
            _occ = occ;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.S(380), Theme.S(112));
            AutoScaleMode = AutoScaleMode.None;

            _life = new Timer();
            _life.Interval = 25000;
            _life.Tick += delegate { Close(); };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Open.Add(this);
            Reposition();
            _life.Start();
            try { SystemSounds.Exclamation.Play(); }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Open.Remove(this);
            Reposition();
            base.OnFormClosed(e);
        }

        private static void Reposition()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int bottom = wa.Bottom - Theme.S(16);
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                ToastForm t = Open[i];
                t.Location = new Point(wa.Right - t.Width - Theme.S(16), bottom - t.Height);
                bottom -= t.Height + Theme.S(10);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, Theme.S(10), Theme.SurfaceHi);
            Theme.DrawRound(g, r, Theme.S(10), Theme.BorderStrong, 1f);

            Color color = Theme.EventColor(_occ.ColorIndex);
            Theme.FillRound(g, new Rectangle(Theme.S(1), Theme.S(10), Theme.S(4), Height - Theme.S(20)), Theme.S(2), color);

            Theme.TextLeft(g, "日程提醒", Theme.FSmall, Theme.Accent, new Rectangle(Theme.S(16), Theme.S(8), Width - Theme.S(30), Theme.S(18)));
            Theme.TextLeft(g, _occ.Title, Theme.FTitle, Theme.TextPrimary, new Rectangle(Theme.S(16), Theme.S(26), Width - Theme.S(30), Theme.S(24)));

            string when;
            if (_occ.AllDay) when = _occ.Start.ToString("M月d日", CultureInfo.InvariantCulture) + " · 全天";
            else when = _occ.Start.ToString("M月d日 HH:mm", CultureInfo.InvariantCulture) + " - " + _occ.End.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(_occ.Source.Location)) when += " · " + _occ.Source.Location;
            Theme.TextLeft(g, when, Theme.FSmall, Theme.TextSecondary, new Rectangle(Theme.S(16), Theme.S(50), Width - Theme.S(30), Theme.S(18)));

            int by = Height - Theme.S(36);
            _snoozeRect = new Rectangle(Theme.S(16), by, Theme.S(104), Theme.S(26));
            _okRect = new Rectangle(Theme.S(128), by, Theme.S(80), Theme.S(26));
            Theme.FillRound(g, _snoozeRect, Theme.S(5), _hoverSnooze ? Theme.SurfaceHover : Theme.Surface);
            Theme.DrawRound(g, _snoozeRect, Theme.S(5), Theme.Border, 1f);
            Theme.TextC(g, "5 分钟后提醒", Theme.FSmall, Theme.TextPrimary, _snoozeRect);
            Theme.FillRound(g, _okRect, Theme.S(5), _hoverOk ? Theme.AccentFillHover : Theme.AccentFill);
            Theme.TextC(g, "知道了", Theme.FSmall, Color.White, _okRect);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool a = _snoozeRect.Contains(e.Location);
            bool b = _okRect.Contains(e.Location);
            if (a != _hoverSnooze || b != _hoverOk)
            {
                _hoverSnooze = a;
                _hoverOk = b;
                Cursor = (a || b) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_okRect.Contains(e.Location))
            {
                Close();
                return;
            }
            if (_snoozeRect.Contains(e.Location))
            {
                if (SnoozeRequested != null) SnoozeRequested(_occ);
                Close();
                return;
            }
            if (OpenRequested != null) OpenRequested(_occ);
            Close();
        }
    }

    /// <summary>提醒调度：每 20 秒扫描一次即将开始的日程。</summary>
    public class ReminderService : IDisposable
    {
        private class SnoozeEntry
        {
            public DateTime At;
            public Occurrence Occ;
        }

        private readonly EventStore _store;
        private readonly Timer _timer;
        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly List<SnoozeEntry> _snoozes = new List<SnoozeEntry>();
        private DateTime _startedAt;
        private bool _enabled = true;

        public event Action<Occurrence> ReminderDue;

        public ReminderService(EventStore store)
        {
            _store = store;
            _startedAt = DateTime.Now;
            _timer = new Timer();
            _timer.Interval = 20000;
            _timer.Tick += delegate { Check(); };
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public void Start()
        {
            _timer.Start();
            Check();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Snooze(Occurrence occ, int minutes)
        {
            SnoozeEntry s = new SnoozeEntry();
            s.At = DateTime.Now.AddMinutes(minutes);
            s.Occ = occ;
            _snoozes.Add(s);
        }

        public void Check()
        {
            if (!_enabled) return;
            DateTime now = DateTime.Now;

            for (int i = _snoozes.Count - 1; i >= 0; i--)
            {
                if (_snoozes[i].At <= now)
                {
                    Occurrence o = _snoozes[i].Occ;
                    _snoozes.RemoveAt(i);
                    Fire(o);
                }
            }

            List<Occurrence> occs;
            try
            {
                occs = _store.ExpandRange(now.AddHours(-3), now.AddHours(27));
            }
            catch
            {
                return;
            }

            for (int i = 0; i < occs.Count; i++)
            {
                Occurrence o = occs[i];
                int rm = o.Source.ReminderMinutes;
                if (rm < 0) continue;
                DateTime trigger = o.Start.AddMinutes(-rm);
                if (trigger > now) continue;
                if (trigger < now.AddMinutes(-15)) continue;
                string key = o.Source.Id + "|" + o.Start.Ticks;
                if (_fired.Contains(key)) continue;
                _fired.Add(key);
                if (trigger < _startedAt.AddSeconds(-20)) continue;
                Fire(o);
            }
        }

        private void Fire(Occurrence o)
        {
            if (ReminderDue != null) ReminderDue(o);
        }

        public void Dispose()
        {
            if (_timer != null) _timer.Dispose();
        }
    }
}
