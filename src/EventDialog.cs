using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>颜色选择小控件。</summary>
    public class SwatchPicker : Control
    {
        private int _index;

        public event EventHandler IndexChanged;

        public SwatchPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.S(24);
            Cursor = Cursors.Hand;
        }

        public int Index
        {
            get { return _index; }
            set { _index = value; Invalidate(); if (IndexChanged != null) IndexChanged(this, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            int n = Theme.EventColors.Length;
            int d = Theme.S(16);
            int gap = Theme.S(10);
            for (int i = 0; i < n; i++)
            {
                Rectangle r = new Rectangle(i * (d + gap), (Height - d) / 2, d, d);
                using (SolidBrush b = new SolidBrush(Theme.EventColors[i]))
                    g.FillEllipse(b, r);
                if (i == _index)
                {
                    using (Pen p = new Pen(Theme.TextPrimary, Theme.SF(1.6f)))
                        g.DrawEllipse(p, r.X - Theme.S(3), r.Y - Theme.S(3), r.Width + Theme.S(6), r.Height + Theme.S(6));
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int d = Theme.S(16);
            int gap = Theme.S(10);
            int idx = e.X / (d + gap);
            if (idx >= 0 && idx < Theme.EventColors.Length) Index = idx;
            base.OnMouseDown(e);
        }
    }

    /// <summary>新建 / 编辑日程对话框。</summary>
    public class EventDialog : Form
    {
        private readonly EventStore _store;
        private readonly EventItem _original;
        private EventItem _result;

        private FieldBox _titleBox;
        private FlatCheck _allDay;
        private DateField _startDate;
        private DateField _endDate;
        private FlatCombo _startHour;
        private FlatCombo _startMin;
        private FlatCombo _endHour;
        private FlatCombo _endMin;
        private FlatCombo _repeat;
        private FlatCombo _reminder;
        private FlatCheck _untilCheck;
        private DateField _untilDate;
        private FieldBox _locationBox;
        private FieldBox _notesBox;
        private SwatchPicker _swatches;
        private UiButton _deleteBtn;
        private Label _warn;
        private Label _repeatHint;

        public bool Deleted;
        public bool Changed;

        public EventItem Result
        {
            get { return _result; }
        }

        public EventDialog(EventStore store, EventItem existing, DateTime defaultStart, DateTime defaultEnd, bool defaultAllDay)
        {
            _store = store;
            _original = existing;
            _result = existing != null ? existing.Clone() : new EventItem();

            Ui.StyleForm(this);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = existing == null ? "新建日程" : "编辑日程";
            ClientSize = new Size(Theme.S(500), Theme.S(612));
            DoubleBuffered = true;

            DateTime start = existing != null ? existing.Start : defaultStart;
            DateTime end = existing != null ? existing.End : defaultEnd;
            bool allDay = existing != null ? existing.AllDay : defaultAllDay;

            int x = Theme.S(22);
            int w = Theme.S(456);

            // 标题
            Label lt = Ui.Caption("标题");
            lt.Location = new Point(x, Theme.S(16));
            Controls.Add(lt);

            _titleBox = Ui.Input(existing != null ? existing.Title : "", false);
            _titleBox.SetBounds(x, Theme.S(34), w, Theme.S(38));
            Controls.Add(_titleBox);

            // 全天
            _allDay = new FlatCheck();
            _allDay.Text = "全天";
            _allDay.Checked = allDay;
            _allDay.SetBounds(x + w - Theme.S(70), Theme.S(72), Theme.S(70), Theme.S(24));
            _allDay.CheckedChanged += delegate { SyncAllDay(); };
            Controls.Add(_allDay);

            // 开始
            Label ls = Ui.Caption("开始");
            ls.Location = new Point(x, Theme.S(80));
            Controls.Add(ls);

            int timeW = Theme.S(62);
            int dateW = w - timeW * 2 - Theme.S(22);
            _startDate = new DateField();
            _startDate.Value = start.Date;
            _startDate.SetBounds(x, Theme.S(98), dateW, Theme.S(32));
            Controls.Add(_startDate);

            _startHour = MakeTimeCombo(true);
            _startHour.SetBounds(x + dateW + Theme.S(8), Theme.S(98), timeW, Theme.S(32));
            Controls.Add(_startHour);
            Label c1 = new Label();
            c1.Text = ":";
            c1.AutoSize = false;
            c1.TextAlign = ContentAlignment.MiddleCenter;
            c1.ForeColor = Theme.TextSecondary;
            c1.Tag = ThemeRole.Secondary;
            c1.BackColor = Color.Transparent;
            c1.SetBounds(x + dateW + Theme.S(8) + timeW - Theme.S(2), Theme.S(98), Theme.S(8), Theme.S(32));
            Controls.Add(c1);
            _startMin = MakeTimeCombo(false);
            _startMin.SetBounds(x + dateW + Theme.S(8) + timeW + Theme.S(6), Theme.S(98), timeW, Theme.S(32));
            Controls.Add(_startMin);

            // 结束
            Label le = Ui.Caption("结束");
            le.Location = new Point(x, Theme.S(140));
            Controls.Add(le);

            _endDate = new DateField();
            _endDate.Value = end.Date;
            _endDate.SetBounds(x, Theme.S(158), dateW, Theme.S(32));
            Controls.Add(_endDate);

            _endHour = MakeTimeCombo(true);
            _endHour.SetBounds(x + dateW + Theme.S(8), Theme.S(158), timeW, Theme.S(32));
            Controls.Add(_endHour);
            Label c2 = new Label();
            c2.Text = ":";
            c2.AutoSize = false;
            c2.TextAlign = ContentAlignment.MiddleCenter;
            c2.ForeColor = Theme.TextSecondary;
            c2.Tag = ThemeRole.Secondary;
            c2.BackColor = Color.Transparent;
            c2.SetBounds(x + dateW + Theme.S(8) + timeW - Theme.S(2), Theme.S(158), Theme.S(8), Theme.S(32));
            Controls.Add(c2);
            _endMin = MakeTimeCombo(false);
            _endMin.SetBounds(x + dateW + Theme.S(8) + timeW + Theme.S(6), Theme.S(158), timeW, Theme.S(32));
            Controls.Add(_endMin);

            // 重复 + 提醒
            Label lr = Ui.Caption("重复");
            lr.Location = new Point(x, Theme.S(202));
            Controls.Add(lr);
            _repeat = new FlatCombo();
            _repeat.Items.AddRange(new object[] { "不重复", "每天", "每周", "每月", "工作日（周一至周五）" });
            _repeat.SelectedIndex = existing != null ? (int)existing.Recurrence : 0;
            _repeat.SetBounds(x, Theme.S(220), Theme.S(210), Theme.S(30));
            _repeat.SelectedIndexChanged += delegate { SyncUntil(); };
            Controls.Add(_repeat);

            Label lm = Ui.Caption("提醒");
            lm.Location = new Point(x + Theme.S(228), Theme.S(202));
            Controls.Add(lm);
            _reminder = new FlatCombo();
            _reminder.Items.AddRange(new object[] { "不提醒", "事件开始时", "提前 5 分钟", "提前 15 分钟", "提前 30 分钟", "提前 1 小时", "提前 1 天" });
            _reminder.SetBounds(x + Theme.S(228), Theme.S(220), w - Theme.S(228), Theme.S(30));
            _reminder.SelectedIndex = ReminderToIndex(existing != null ? existing.ReminderMinutes : store.Data.Settings.DefaultReminderMinutes);
            Controls.Add(_reminder);

            // 重复截止
            _untilCheck = new FlatCheck();
            _untilCheck.Text = "重复截止";
            _untilCheck.SetBounds(x, Theme.S(262), Theme.S(90), Theme.S(24));
            _untilCheck.CheckedChanged += delegate { SyncUntil(); };
            Controls.Add(_untilCheck);

            _untilDate = new DateField();
            _untilDate.Value = (existing != null && existing.RecurrenceUntil != DateTime.MinValue) ? existing.RecurrenceUntil : start.Date.AddMonths(3);
            _untilDate.SetBounds(x + Theme.S(96), Theme.S(258), dateW - Theme.S(96), Theme.S(32));
            Controls.Add(_untilDate);

            _repeatHint = new Label();
            _repeatHint.AutoSize = false;
            _repeatHint.ForeColor = Theme.TextMuted;
            _repeatHint.Tag = ThemeRole.Muted;
            _repeatHint.BackColor = Color.Transparent;
            _repeatHint.Font = Theme.FSmall;
            _repeatHint.Text = "";
            _repeatHint.SetBounds(x + Theme.S(96) + dateW - Theme.S(96) + Theme.S(10), Theme.S(262), w - (dateW + Theme.S(10)), Theme.S(24));
            Controls.Add(_repeatHint);

            if (existing != null && existing.RecurrenceUntil != DateTime.MinValue) _untilCheck.Checked = true;

            // 地点
            Label ll = Ui.Caption("地点");
            ll.Location = new Point(x, Theme.S(300));
            Controls.Add(ll);
            _locationBox = Ui.Input(existing != null ? existing.Location : "", false);
            _locationBox.SetBounds(x, Theme.S(318), w, Theme.S(38));
            Controls.Add(_locationBox);

            // 备注
            Label ln = Ui.Caption("备注");
            ln.Location = new Point(x, Theme.S(366));
            Controls.Add(ln);
            _notesBox = Ui.Input(existing != null ? existing.Notes : "", true);
            _notesBox.SetBounds(x, Theme.S(384), w, Theme.S(110));
            Controls.Add(_notesBox);

            // 颜色
            Label lc = Ui.Caption("颜色");
            lc.Location = new Point(x, Theme.S(506));
            Controls.Add(lc);
            _swatches = new SwatchPicker();
            _swatches.Index = existing != null ? existing.ColorIndex : 0;
            _swatches.SetBounds(x + Theme.S(44), Theme.S(502), w - Theme.S(44), Theme.S(26));
            Controls.Add(_swatches);

            // 提示
            _warn = new Label();
            _warn.AutoSize = false;
            _warn.ForeColor = Theme.Danger;
            _warn.Tag = ThemeRole.Danger;
            _warn.BackColor = Color.Transparent;
            _warn.Font = Theme.FSmall;
            _warn.TextAlign = ContentAlignment.MiddleLeft;
            _warn.SetBounds(x, Theme.S(538), w - Theme.S(180), Theme.S(24));
            Controls.Add(_warn);

            // 按钮
            UiButton cancel = new UiButton();
            cancel.Text = "取消";
            cancel.Style = ButtonStyle.Secondary;
            cancel.SetBounds(x + w - Theme.S(88) - Theme.S(96), Theme.S(562), Theme.S(88), Theme.S(34));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            UiButton save = new UiButton();
            save.Text = existing == null ? "创建" : "保存";
            save.Style = ButtonStyle.Primary;
            save.SetBounds(x + w - Theme.S(88), Theme.S(562), Theme.S(88), Theme.S(34));
            save.Click += delegate { OnSave(); };
            Controls.Add(save);

            _deleteBtn = new UiButton();
            _deleteBtn.Text = "删除";
            _deleteBtn.Style = ButtonStyle.Danger;
            _deleteBtn.SetBounds(x, Theme.S(562), Theme.S(80), Theme.S(34));
            _deleteBtn.Visible = existing != null;
            _deleteBtn.Click += delegate
            {
                Deleted = true;
                Changed = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_deleteBtn);

            AcceptButton = null;
            KeyPreview = true;
            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.S) { OnSave(); e.Handled = true; }
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };

            SetTime(_startHour, _startMin, start);
            SetTime(_endHour, _endMin, end);
            SyncAllDay();
            SyncUntil();

            // 打开期间切换主题：对话框内所有控件同步换色，已输入内容不受影响
            Theme.Changed += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Ui.StyleForm(this);
            Ui.RepaintTheme(this);
            Theme.ApplyTitleBar(Handle);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= OnThemeChanged;
            base.OnFormClosed(e);
        }

        private FlatCombo MakeTimeCombo(bool hours)
        {
            FlatCombo c = new FlatCombo();
            if (hours)
            {
                for (int i = 0; i < 24; i++) c.Items.Add(i.ToString("00", CultureInfo.InvariantCulture));
            }
            else
            {
                for (int i = 0; i < 60; i += 5) c.Items.Add(i.ToString("00", CultureInfo.InvariantCulture));
            }
            return c;
        }

        private static void SetTime(FlatCombo h, FlatCombo m, DateTime t)
        {
            h.SelectedIndex = Math.Max(0, Math.Min(23, t.Hour));
            string mv = t.Minute.ToString("00", CultureInfo.InvariantCulture);
            int idx = m.Items.IndexOf(mv);
            if (idx < 0)
            {
                m.Items.Add(mv);
                List<string> all = new List<string>();
                for (int i = 0; i < m.Items.Count; i++) all.Add(m.Items[i].ToString());
                all.Sort(StringComparer.Ordinal);
                m.Items.Clear();
                m.Items.AddRange(all.ToArray());
                idx = m.Items.IndexOf(mv);
            }
            m.SelectedIndex = idx;
        }

        private static int TimeValue(FlatCombo h, FlatCombo m)
        {
            int hh = h.SelectedIndex < 0 ? 0 : h.SelectedIndex;
            int mm = 0;
            if (m.SelectedIndex >= 0) int.TryParse(m.Items[m.SelectedIndex].ToString(), out mm);
            return hh * 60 + Math.Max(0, Math.Min(59, mm));
        }

        private void SyncAllDay()
        {
            bool on = _allDay.Checked;
            _startHour.Enabled = !on;
            _startMin.Enabled = !on;
            _endHour.Enabled = !on;
            _endMin.Enabled = !on;
            _startHour.Visible = !on;
            _startMin.Visible = !on;
            _endHour.Visible = !on;
            _endMin.Visible = !on;
        }

        private void SyncUntil()
        {
            bool recurring = _repeat.SelectedIndex > 0;
            _untilCheck.Enabled = recurring;
            _untilDate.Enabled = recurring && _untilCheck.Checked;
            _untilDate.Visible = recurring && _untilCheck.Checked;
            if (!recurring) _untilCheck.Checked = false;
            if (_repeatHint != null)
            {
                _repeatHint.Text = recurring ? (_untilCheck.Checked ? "含截止日" : "长期重复") : "";
            }
        }

        private static int ReminderToIndex(int minutes)
        {
            if (minutes < 0) return 0;
            if (minutes == 0) return 1;
            if (minutes <= 5) return 2;
            if (minutes <= 15) return 3;
            if (minutes <= 30) return 4;
            if (minutes <= 60) return 5;
            return 6;
        }

        private static int IndexToReminder(int index)
        {
            switch (index)
            {
                case 1: return 0;
                case 2: return 5;
                case 3: return 15;
                case 4: return 30;
                case 5: return 60;
                case 6: return 1440;
                default: return -1;
            }
        }

        private void OnSave()
        {
            string title = _titleBox.Inner.Text.Trim();
            if (title.Length == 0)
            {
                _warn.Text = "请填写标题";
                _titleBox.Inner.Focus();
                return;
            }

            DateTime start;
            DateTime end;
            if (_allDay.Checked)
            {
                start = _startDate.Value.Date;
                DateTime endDay = _endDate.Value.Date;
                if (endDay < start) endDay = start;
                end = endDay.AddDays(1).AddSeconds(-1);
            }
            else
            {
                start = _startDate.Value.Date.AddMinutes(TimeValue(_startHour, _startMin));
                end = _endDate.Value.Date.AddMinutes(TimeValue(_endHour, _endMin));
                if (end <= start)
                {
                    _warn.Text = "结束时间必须晚于开始时间";
                    return;
                }
            }

            EventItem e = _original != null ? _original.Clone() : new EventItem();
            if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
            e.Title = title;
            e.Start = start;
            e.End = end;
            e.AllDay = _allDay.Checked;
            e.Location = _locationBox.Inner.Text.Trim();
            e.Notes = _notesBox.Inner.Text.Trim();
            e.ReminderMinutes = IndexToReminder(_reminder.SelectedIndex);
            e.Recurrence = (RecurrenceType)Math.Max(0, Math.Min(4, _repeat.SelectedIndex));
            e.ColorIndex = _swatches.Index;

            if (e.Recurrence == RecurrenceType.None) e.RecurrenceUntil = DateTime.MinValue;
            else if (_untilCheck.Checked) e.RecurrenceUntil = _untilDate.Value.Date;
            else e.RecurrenceUntil = DateTime.MinValue;

            if (_original != null && _original.Recurrence != e.Recurrence)
                e.ExcludedStarts.Clear();

            _result = e;
            Changed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(Handle);
        }
    }
}
