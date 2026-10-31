using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>左侧"当日日程"列表（自绘、可点击、可滚动）。</summary>
    public class AgendaList : Control
    {
        private readonly List<Occurrence> _items = new List<Occurrence>();
        private int _hover = -1;
        private int _scroll;
        private DateTime _day = DateTime.Today;

        public event Action<Occurrence> ItemClicked;

        public AgendaList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.FBody;
            TabStop = false;
        }

        public void SetItems(DateTime day, List<Occurrence> items)
        {
            _day = day;
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _scroll = 0;
            Invalidate();
        }

        private int RowH { get { return Theme.S(50); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Theme.Window);

            if (_items.Count == 0)
            {
                Theme.TextC(g, "这一天没有安排", Theme.FSmall, Theme.TextMuted, new Rectangle(0, Theme.S(24), Width, Theme.S(24)));
                Theme.TextC(g, "双击日历空白处即可新建", Theme.FSmall, Theme.TextMuted, new Rectangle(0, Theme.S(46), Width, Theme.S(24)));
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                int y = i * RowH - _scroll;
                if (y + RowH < 0 || y > Height) continue;
                Rectangle row = new Rectangle(0, y, Width - 1, RowH - Theme.S(4));
                if (i == _hover) Theme.FillRound(g, row, Theme.S(6), Theme.SurfaceHi);

                Occurrence o = _items[i];
                Color color = Theme.EventColor(o.ColorIndex);
                Rectangle bar = new Rectangle(row.X, row.Y + Theme.S(6), Theme.S(3), row.Height - Theme.S(12));
                Theme.FillRound(g, bar, Theme.S(2), color);

                string timeText = o.AllDay ? "全天" : o.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
                Theme.TextLeft(g, timeText, Theme.FTime, Theme.TextSecondary, new Rectangle(row.X + Theme.S(10), row.Y + Theme.S(2), Theme.S(46), Theme.S(18)));
                Theme.TextLeft(g, o.Title, Theme.FBody, Theme.TextPrimary, new Rectangle(row.X + Theme.S(58), row.Y + Theme.S(2), row.Width - Theme.S(62), Theme.S(18)));
                if (!string.IsNullOrEmpty(o.Source.Location))
                    Theme.TextLeft(g, o.Source.Location, Theme.FSmall, Theme.TextMuted, new Rectangle(row.X + Theme.S(58), row.Y + Theme.S(20), row.Width - Theme.S(62), Theme.S(16)));
                else if (!o.AllDay)
                    Theme.TextLeft(g, o.End.ToString("HH:mm", CultureInfo.InvariantCulture) + " 结束", Theme.FSmall, Theme.TextMuted, new Rectangle(row.X + Theme.S(58), row.Y + Theme.S(20), row.Width - Theme.S(62), Theme.S(16)));
            }
        }

        private int IndexAt(Point p)
        {
            int idx = (p.Y + _scroll) / RowH;
            if (idx < 0 || idx >= _items.Count) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx >= 0 && e.Button == MouseButtons.Left && ItemClicked != null)
                ItemClicked(_items[idx]);
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int total = _items.Count * RowH;
            int max = Math.Max(0, total - Height);
            _scroll -= (e.Delta / 120) * Theme.S(50);
            if (_scroll < 0) _scroll = 0;
            if (_scroll > max) _scroll = max;
            Invalidate();
        }
    }

    /// <summary>搜索结果条目：日程 / 节假日 / calendar 命令三类。</summary>
    public class SearchHit
    {
        public string Title = "";
        public string Subtitle = "";
        public int ColorIndex = 0;

        /// <summary>日程命中（其余两类为 null）。</summary>
        public EventItem Event;

        /// <summary>节假日命中时的跳转日期。</summary>
        public DateTime JumpDate;

        public bool IsHoliday;
        public bool IsCalendarCommand;
    }

    /// <summary>搜索下拉结果列表。</summary>
    public class SearchList : Control
    {
        private readonly List<SearchHit> _items = new List<SearchHit>();
        private int _selected;
        private int _hover = -1;

        public event Action<SearchHit> Activated;

        public SearchList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = Theme.FBody;
            TabStop = false;
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (_items.Count == 0) { _selected = 0; return; }
                if (value < 0) value = 0;
                if (value >= _items.Count) value = _items.Count - 1;
                _selected = value;
                Invalidate();
            }
        }

        public void SetItems(List<SearchHit> items)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _selected = 0;
            Invalidate();
        }

        public SearchHit Current
        {
            get { return _items.Count == 0 ? null : _items[_selected]; }
        }

        private int RowH { get { return Theme.S(46); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Theme.Surface);
            if (_items.Count == 0)
            {
                Theme.TextC(g, "没有匹配的日程或节假日", Theme.FSmall, Theme.TextMuted, new Rectangle(0, 0, Width, Height));
                return;
            }
            for (int i = 0; i < _items.Count; i++)
            {
                Rectangle row = new Rectangle(Theme.S(4), Theme.S(4) + i * RowH, Width - Theme.S(8), RowH - Theme.S(4));
                if (i == _selected) Theme.FillRound(g, row, Theme.S(5), Theme.AccentSoft);
                else if (i == _hover) Theme.FillRound(g, row, Theme.S(5), Theme.SurfaceHi);

                SearchHit it = _items[i];
                Color color = it.IsHoliday ? Theme.Holiday
                    : it.IsCalendarCommand ? Theme.Accent
                    : Theme.EventColor(it.ColorIndex);
                Theme.FillRound(g, new Rectangle(row.X + Theme.S(6), row.Y + Theme.S(8), Theme.S(3), row.Height - Theme.S(16)), Theme.S(2), color);
                Theme.TextLeft(g, it.Title, Theme.FBody, Theme.TextPrimary, new Rectangle(row.X + Theme.S(16), row.Y + Theme.S(3), row.Width - Theme.S(22), Theme.S(20)));
                Theme.TextLeft(g, it.Subtitle, Theme.FSmall, Theme.TextMuted, new Rectangle(row.X + Theme.S(16), row.Y + Theme.S(21), row.Width - Theme.S(22), Theme.S(18)));
            }
        }

        private int IndexAt(Point p)
        {
            int idx = (p.Y - Theme.S(4)) / RowH;
            if (idx < 0 || idx >= _items.Count) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx != _hover)
            {
                _hover = idx;
                if (idx >= 0) { _selected = idx; }
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx >= 0 && Activated != null) Activated(_items[idx]);
            base.OnMouseDown(e);
        }
    }

    /// <summary>三视图共用主窗口。</summary>
    public class MainForm : Form
    {
        private readonly EventStore _store;
        private readonly ReminderService _reminders;
        private readonly HolidayService _holidays;

        private CalendarView _view = CalendarView.Month;
        private DateTime _anchor = DateTime.Today;
        private DateTime _selected = DateTime.Today;

        private Panel _top;
        private UiButton _prev;
        private UiButton _next;
        private UiButton _today;
        private UiButton _newBtn;
        private UiButton _more;
        private Label _title;
        private FieldBox _searchBox;
        private FlatTextBox _search;
        private Segmented _seg;
        private Panel _content;
        private MonthView _monthView;
        private TimeGridView _timeView;
        private Panel _sidebar;
        private MiniMonth _mini;
        private Label _sideTitle;
        private AgendaList _agenda;
        private Panel _status;
        private Label _statusLeft;
        private Label _statusRight;
        private Panel _searchPanel;
        private SearchList _searchList;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayThemeItem;
        private Timer _themeWatch;
        private Timer _hintTimer;
        private bool _reallyExit;
        private bool _balloonShown;

        public MainForm(EventStore store, ReminderService reminders, HolidayService holidays)
        {
            _store = store;
            _reminders = reminders;
            _holidays = holidays;

            Ui.StyleForm(this);
            Text = "轻历 - 日程与提醒";
            MinimumSize = new Size(Theme.S(980), Theme.S(660));
            ClientSize = new Size(Theme.S(1240), Theme.S(790));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Program.AppIcon; } catch { }

            _view = (CalendarView)Math.Max(0, Math.Min(2, store.Data.Settings.View));

            BuildTopBar();
            BuildSidebar();
            BuildContent();
            BuildStatus();
            BuildSearchPanel();
            BuildTray();

            _reminders.ReminderDue += OnReminderDue;
            _store.Changed += delegate { RefreshAll(); };

            Theme.Changed += OnThemeChanged;

            // 系统主题广播去抖：500ms 内多次 WM_SETTINGCHANGE 只处理一次
            _themeWatch = new Timer();
            _themeWatch.Interval = 500;
            _themeWatch.Tick += delegate
            {
                _themeWatch.Stop();
                Theme.RefreshFromSystem();
            };

            // 状态栏临时提示的恢复定时器
            _hintTimer = new Timer();
            _hintTimer.Interval = 2000;
            _hintTimer.Tick += delegate
            {
                _hintTimer.Stop();
                UpdateStatus();
            };

            KeyDown += OnFormKeyDown;
            Resize += delegate { LayoutAll(); };

            LayoutAll();
            RefreshAll();
            if (_view != CalendarView.Month) _timeView.ScrollToTime(DateTime.Today.AddHours(8));

            // 节假日数据：视图挂载 + 后台自动拉取（完成后回到 UI 线程刷新）
            _monthView.Holidays = _holidays;
            _timeView.Holidays = _holidays;
            _mini.Holidays = _holidays;
            _holidays.Updated += OnHolidaysUpdated;
            _holidays.RefreshAsync();
        }

        // ---------------- 界面构建 ----------------

        private void BuildTopBar()
        {
            _top = new Panel();
            _top.BackColor = Theme.Window;
            _top.Tag = ThemeRole.Window;
            _top.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.8f)))
                    e.Graphics.DrawLine(p, 0, _top.Height - 1, _top.Width, _top.Height - 1);
            };
            Controls.Add(_top);

            _prev = new UiButton();
            _prev.Text = "\u2039";
            _prev.Style = ButtonStyle.Secondary;
            _prev.Font = Theme.FBodyBold;
            _prev.Size = new Size(Theme.S(32), Theme.S(32));
            _prev.Click += delegate { Navigate(-1); };
            _top.Controls.Add(_prev);

            _next = new UiButton();
            _next.Text = "\u203A";
            _next.Style = ButtonStyle.Secondary;
            _next.Font = Theme.FBodyBold;
            _next.Size = new Size(Theme.S(32), Theme.S(32));
            _next.Click += delegate { Navigate(1); };
            _top.Controls.Add(_next);

            _today = new UiButton();
            _today.Text = "今天";
            _today.Style = ButtonStyle.Secondary;
            _today.Size = new Size(Theme.S(60), Theme.S(32));
            _today.Click += delegate { GoToday(); };
            _top.Controls.Add(_today);

            _title = new Label();
            _title.AutoSize = false;
            _title.ForeColor = Theme.TextPrimary;
            _title.Tag = ThemeRole.Primary;
            _title.BackColor = Color.Transparent;
            _title.Font = Theme.FBig;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _top.Controls.Add(_title);

            _newBtn = new UiButton();
            _newBtn.Text = "＋ 新建日程";
            _newBtn.Style = ButtonStyle.Primary;
            _newBtn.Font = Theme.FBody;
            _newBtn.Size = new Size(Theme.S(112), Theme.S(34));
            _newBtn.Click += delegate { NewEventAt(_selected.Date.AddHours(9)); };
            _top.Controls.Add(_newBtn);

            _more = new UiButton();
            _more.Text = "\u22EF";
            _more.Style = ButtonStyle.Secondary;
            _more.Font = Theme.FBodyBold;
            _more.Size = new Size(Theme.S(36), Theme.S(34));
            _more.Click += delegate { ShowMoreMenu(); };
            _top.Controls.Add(_more);

            _seg = new Segmented();
            _seg.Items = new string[] { "月", "周", "日" };
            _seg.SelectedIndex = (int)_view;
            _seg.Size = new Size(Theme.S(144), Theme.S(34));
            _seg.SelectedIndexChanged += delegate
            {
                _view = (CalendarView)_seg.SelectedIndex;
                _store.Data.Settings.View = (int)_view;
                _store.Save();
                LayoutAll();
                RefreshAll();
                if (_view != CalendarView.Month) _timeView.ScrollToTime(DateTime.Today.AddHours(8));
            };
            _top.Controls.Add(_seg);

            _search = new FlatTextBox();
            _search.BorderStyle = BorderStyle.None;
            _search.BackColor = Theme.Surface;
            _search.ForeColor = Theme.TextPrimary;
            _search.Font = Theme.FBody;
            _search.Placeholder = "搜索标题 / 地点 / 节假日（支持 calendar）";
            _search.TextChanged += delegate { UpdateSearch(); };
            _search.KeyDown += OnSearchKeyDown;
            _search.GotFocus += delegate { UpdateSearch(); };
            _searchBox = new FieldBox(_search);
            _searchBox.Size = new Size(Theme.S(220), Theme.S(34));
            _top.Controls.Add(_searchBox);
        }

        private void BuildSidebar()
        {
            _sidebar = new Panel();
            _sidebar.BackColor = Theme.Window;
            _sidebar.Tag = ThemeRole.Window;
            _sidebar.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Mix(Theme.Window, Theme.Border, 0.8f)))
                    e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            };
            Controls.Add(_sidebar);

            _mini = new MiniMonth();
            _mini.ShowHeader = true;
            _mini.DateClicked += delegate (DateTime d)
            {
                _selected = d;
                if (_view == CalendarView.Month) _anchor = d;
                else if (_view == CalendarView.Week) _anchor = d;
                else _anchor = d;
                RefreshAll();
            };
            _mini.MonthChanged += delegate { RefreshMiniMarks(); };
            _sidebar.Controls.Add(_mini);

            _sideTitle = new Label();
            _sideTitle.AutoSize = false;
            _sideTitle.ForeColor = Theme.TextSecondary;
            _sideTitle.Tag = ThemeRole.Secondary;
            _sideTitle.BackColor = Color.Transparent;
            _sideTitle.Font = Theme.FBodyBold;
            _sideTitle.TextAlign = ContentAlignment.MiddleLeft;
            _sidebar.Controls.Add(_sideTitle);

            _agenda = new AgendaList();
            _agenda.ItemClicked += delegate (Occurrence o) { EditOccurrence(o); };
            _sidebar.Controls.Add(_agenda);
        }

        private void BuildContent()
        {
            _content = new Panel();
            _content.BackColor = Theme.Window;
            _content.Tag = ThemeRole.Window;
            Controls.Add(_content);

            _monthView = new MonthView();
            _monthView.Store = _store;
            _monthView.EventClicked += delegate (Occurrence o) { EditOccurrence(o); };
            _monthView.DayDoubleClicked += delegate (DateTime d) { NewEventAt(d.AddHours(9)); };
            _monthView.DayRightClicked += ShowDayMenu;
            _monthView.SelectionChanged += delegate (DateTime d)
            {
                _selected = d;
                UpdateSidebar();
            };
            _content.Controls.Add(_monthView);

            _timeView = new TimeGridView();
            _timeView.Store = _store;
            _timeView.EventClicked += delegate (Occurrence o) { EditOccurrence(o); };
            _timeView.SlotDoubleClicked += delegate (DateTime t) { NewEventAt(t); };
            _timeView.EventRightClicked += ShowSlotMenu;
            _timeView.DateHeaderClicked += delegate (DateTime d)
            {
                _selected = d;
                if (_view == CalendarView.Day) { _anchor = d; RefreshAll(); }
                else UpdateSidebar();
            };
            _content.Controls.Add(_timeView);
        }

        private void BuildStatus()
        {
            _status = new Panel();
            _status.BackColor = Theme.Mix(Theme.Window, Theme.Surface, 0.6f);
            _status.Tag = ThemeRole.StatusBar;
            Controls.Add(_status);

            _statusLeft = new Label();
            _statusLeft.AutoSize = false;
            _statusLeft.ForeColor = Theme.TextMuted;
            _statusLeft.Tag = ThemeRole.Muted;
            _statusLeft.BackColor = Color.Transparent;
            _statusLeft.Font = Theme.FSmall;
            _statusLeft.TextAlign = ContentAlignment.MiddleLeft;
            _status.Controls.Add(_statusLeft);

            _statusRight = new Label();
            _statusRight.AutoSize = false;
            _statusRight.ForeColor = Theme.TextMuted;
            _statusRight.Tag = ThemeRole.Muted;
            _statusRight.BackColor = Color.Transparent;
            _statusRight.Font = Theme.FSmall;
            _statusRight.TextAlign = ContentAlignment.MiddleRight;
            _status.Controls.Add(_statusRight);
        }

        private void BuildSearchPanel()
        {
            _searchPanel = new Panel();
            _searchPanel.BackColor = Theme.Surface;
            _searchPanel.Tag = ThemeRole.Surface;
            _searchPanel.Visible = false;
            _searchPanel.Paint += delegate (object s, PaintEventArgs e)
            {
                Theme.DrawRound(e.Graphics, new Rectangle(0, 0, _searchPanel.Width - 1, _searchPanel.Height - 1), Theme.S(6), Theme.BorderStrong, 1f);
            };
            Controls.Add(_searchPanel);

            _searchList = new SearchList();
            _searchList.Dock = DockStyle.Fill;
            _searchList.Activated += delegate (SearchHit hit) { OpenFromSearch(hit); };
            _searchPanel.Controls.Add(_searchList);
        }

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Program.AppIcon; } catch { _tray.Icon = SystemIcons.Application; }
            _tray.Text = "轻历 - 日程与提醒";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { RestoreFromTray(); };

            _trayMenu = NewThemeMenu();
            _trayMenu.Items.Add("打开轻历", null, delegate { RestoreFromTray(); });
            _trayMenu.Items.Add("新建日程", null, delegate { RestoreFromTray(); NewEventAt(DateTime.Now); });
            _trayMenu.Items.Add("今天", null, delegate { RestoreFromTray(); GoToday(); });
            _trayThemeItem = BuildThemeMenu();
            _trayMenu.Items.Add(_trayThemeItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出", null, delegate { _reallyExit = true; Close(); });

            // 托盘菜单的 BackColor/ForeColor 只在构建期写一次会残留旧配色：每次展开前重新着色并同步勾选
            _trayMenu.Opening += delegate
            {
                _trayMenu.BackColor = Theme.Surface;
                _trayMenu.ForeColor = Theme.TextPrimary;
                _trayMenu.Renderer = new ThemeMenuRenderer();
                _trayThemeItem.DropDown.BackColor = Theme.Surface;
                _trayThemeItem.DropDown.ForeColor = Theme.TextPrimary;
                SyncThemeChecks(_trayThemeItem);
            };
            _tray.ContextMenuStrip = _trayMenu;
        }

        private void LayoutAll()
        {
            int topH = Theme.S(58);
            int sideW = Theme.S(252);
            int statusH = Theme.S(26);

            _top.SetBounds(0, 0, ClientSize.Width, topH);
            int y = (topH - Theme.S(32)) / 2;
            _prev.Location = new Point(Theme.S(14), y);
            _next.Location = new Point(Theme.S(50), y);
            _today.Location = new Point(Theme.S(90), y);
            _title.SetBounds(Theme.S(162), 0, Math.Max(Theme.S(120), ClientSize.Width - Theme.S(700)), topH);

            int right = ClientSize.Width - Theme.S(14);
            _more.Location = new Point(right - Theme.S(36), (topH - Theme.S(34)) / 2);
            right -= Theme.S(42);
            _newBtn.Location = new Point(right - Theme.S(112), (topH - Theme.S(34)) / 2);
            right -= Theme.S(120);
            _seg.Location = new Point(right - Theme.S(144), (topH - Theme.S(34)) / 2);
            right -= Theme.S(152);
            _searchBox.Location = new Point(right - Theme.S(220), (topH - Theme.S(34)) / 2);

            _sidebar.SetBounds(0, topH, sideW, ClientSize.Height - topH - statusH);
            _content.SetBounds(sideW, topH, ClientSize.Width - sideW, ClientSize.Height - topH - statusH);
            _status.SetBounds(0, ClientSize.Height - statusH, ClientSize.Width, statusH);
            _statusLeft.SetBounds(Theme.S(12), 0, ClientSize.Width / 2, statusH);
            _statusRight.SetBounds(ClientSize.Width / 2, 0, ClientSize.Width / 2 - Theme.S(12), statusH);

            _mini.SetBounds(Theme.S(12), Theme.S(12), sideW - Theme.S(24), Theme.S(226));
            _sideTitle.SetBounds(Theme.S(14), Theme.S(248), sideW - Theme.S(28), Theme.S(22));
            _agenda.SetBounds(Theme.S(12), Theme.S(274), sideW - Theme.S(24), Math.Max(Theme.S(60), _sidebar.Height - Theme.S(286)));

            _monthView.Visible = _view == CalendarView.Month;
            _timeView.Visible = _view != CalendarView.Month;
            _monthView.SetBounds(Theme.S(8), Theme.S(8), _content.Width - Theme.S(16), _content.Height - Theme.S(16));
            _timeView.SetBounds(Theme.S(4), Theme.S(4), _content.Width - Theme.S(8), _content.Height - Theme.S(8));

            if (_searchPanel.Visible) PositionSearchPanel();
        }

        private void PositionSearchPanel()
        {
            Point p = _searchBox.PointToScreen(new Point(0, _searchBox.Height + Theme.S(4)));
            Point cp = PointToClient(p);
            _searchPanel.SetBounds(cp.X, cp.Y, Math.Max(Theme.S(300), _searchBox.Width + Theme.S(160)), Theme.S(300));
            _searchPanel.BringToFront();
        }

        // ---------------- 数据刷新 ----------------

        private void RefreshAll()
        {
            _monthView.AnchorDate = _anchor;
            _monthView.SelectedDate = _selected;
            _monthView.Reload();

            _timeView.AnchorDate = _anchor;
            _timeView.SelectedDate = _selected;
            _timeView.DayCount = _view == CalendarView.Week ? 7 : 1;
            _timeView.Reload();

            _title.Text = TitleText();
            UpdateSidebar();
            RefreshMiniMarks();
            UpdateStatus();
        }

        /// <summary>状态栏：日程总数与节假日数据来源。</summary>
        private void UpdateStatus()
        {
            int total = _store.Data.Events.Count;
            _statusLeft.Text = "数据文件：" + _store.DataFile;
            _statusRight.Text = string.Format("共 {0} 条日程 · 节假日：{1}", total, _holidays.StatusText);
        }

        private void RefreshMiniMarks()
        {
            HashSet<DateTime> set = new HashSet<DateTime>();
            DateTime first = _mini.Month;
            DateTime from = first.AddDays(-7);
            DateTime to = first.AddDays(42);
            List<Occurrence> list = _store.ExpandRange(from, to);
            for (int i = 0; i < list.Count; i++)
            {
                DateTime d = list[i].Start.Date;
                DateTime last = list[i].EffectiveEnd.Date;
                if (list[i].AllDay && list[i].EffectiveEnd.TimeOfDay == TimeSpan.Zero) last = last.AddDays(-1);
                if (last < d) last = d;
                for (DateTime cur = d; cur <= last; cur = cur.AddDays(1)) set.Add(cur);
            }
            _mini.Marked = set;
            _mini.SelectedDate = _selected;
            _mini.Invalidate();
        }

        private void UpdateSidebar()
        {
            string title = "日程 · " + _selected.ToString("M月d日", CultureInfo.InvariantCulture) + " " + DateField.WeekName(_selected);
            HolidayInfo hi = _holidays.GetDay(_selected.Date);
            if (hi != null) title += hi.IsOffDay ? " · " + hi.Name : " · 调休上班";
            _sideTitle.Text = title;
            List<Occurrence> list = _store.ExpandRange(_selected.Date, _selected.Date.AddDays(1));
            _agenda.SetItems(_selected, list);
        }

        /// <summary>节假日数据后台刷新完成后回到 UI 线程刷新界面。</summary>
        private void OnHolidaysUpdated()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    _monthView.Reload();
                    _timeView.Reload();
                    _mini.Invalidate();
                    UpdateSidebar();
                    UpdateStatus();
                }));
            }
            catch
            {
                // 窗口已关闭等情况直接忽略
            }
        }

        private string TitleText()
        {
            if (_view == CalendarView.Month)
                return string.Format("{0}年{1}月", _anchor.Year, _anchor.Month);
            if (_view == CalendarView.Week)
            {
                int lead = ((int)_anchor.DayOfWeek + 6) % 7;
                DateTime s = _anchor.Date.AddDays(-lead);
                DateTime e = s.AddDays(6);
                if (s.Month == e.Month) return string.Format("{0}年{1}月{2}日 - {3}日", s.Year, s.Month, s.Day, e.Day);
                return string.Format("{0}年{1}月{2}日 - {3}月{4}日", s.Year, s.Month, s.Day, e.Month, e.Day);
            }
            return _anchor.ToString("yyyy年M月d日", CultureInfo.InvariantCulture) + " " + DateField.WeekName(_anchor);
        }

        // ---------------- 导航 ----------------

        private void Navigate(int dir)
        {
            if (_view == CalendarView.Month) _anchor = _anchor.AddMonths(dir);
            else if (_view == CalendarView.Week) _anchor = _anchor.AddDays(7 * dir);
            else _anchor = _anchor.AddDays(dir);

            if (_view == CalendarView.Day) _selected = _anchor;
            else if (_selected.Month != _anchor.Month || _selected.Year != _anchor.Year)
                _selected = _anchor;

            RefreshAll();
            if (_view != CalendarView.Month) _timeView.ScrollToTime(DateTime.Today.AddHours(8));
        }

        private void GoToday()
        {
            _anchor = DateTime.Today;
            _selected = DateTime.Today;
            RefreshAll();
            if (_view != CalendarView.Month) _timeView.ScrollToTime(DateTime.Now);
        }

        private void GoToDate(DateTime d)
        {
            _anchor = d;
            _selected = d;
            RefreshAll();
        }

        // ---------------- 增删改 ----------------

        private void NewEventAt(DateTime start)
        {
            NewEventAt(start, false);
        }

        private void NewEventAt(DateTime start, bool allDay)
        {
            DateTime s = allDay ? start.Date : start;
            DateTime e = allDay ? start.Date.AddDays(1).AddSeconds(-1) : start.AddHours(1);
            EventDialog dlg = new EventDialog(_store, null, s, e, allDay);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Changed && dlg.Result != null)
            {
                _store.Add(dlg.Result);
                GoToDate(dlg.Result.Start.Date);
            }
        }

        private void EditOccurrence(Occurrence o)
        {
            if (o == null) return;
            EventItem src = _store.Find(o.Source.Id);
            if (src == null) src = o.Source;

            bool recurring = src.Recurrence != RecurrenceType.None && o.IsRecurringInstance;
            EventItem editing = src.Clone();
            bool singleInstance = false;

            if (recurring)
            {
                int scope = ScopeDialog.Ask(this, "修改重复日程", "这个日程属于重复系列，你希望修改哪一部分？");
                if (scope == 0) return;
                if (scope == 1)
                {
                    singleInstance = true;
                    TimeSpan dur = src.Duration;
                    editing.Id = "";
                    editing.Recurrence = RecurrenceType.None;
                    editing.RecurrenceUntil = DateTime.MinValue;
                    editing.ExcludedStarts = new List<DateTime>();
                    editing.Start = o.Start;
                    editing.End = src.AllDay ? o.Start.Date.AddDays(src.DayCount).AddSeconds(-1) : o.Start + dur;
                }
            }

            EventDialog dlg = new EventDialog(_store, editing, editing.Start, editing.End, editing.AllDay);
            DialogResult dr = dlg.ShowDialog(this);
            if (dr != DialogResult.OK) return;

            if (dlg.Deleted)
            {
                DeleteOccurrence(o);
                return;
            }
            if (!dlg.Changed || dlg.Result == null) return;

            if (singleInstance)
            {
                EventItem ne = dlg.Result;
                ne.Id = Guid.NewGuid().ToString("N");
                _store.Add(ne);
                _store.RemoveOccurrence(o);
            }
            else
            {
                _store.Update(dlg.Result);
            }
            RefreshAll();
        }

        private void DeleteOccurrence(Occurrence o)
        {
            if (o == null) return;
            EventItem src = _store.Find(o.Source.Id);
            if (src == null) src = o.Source;

            if (src.Recurrence != RecurrenceType.None && o.IsRecurringInstance)
            {
                int scope = ScopeDialog.Ask(this, "删除重复日程", "「" + o.Title + "」是重复日程，要删除哪一部分？");
                if (scope == 0) return;
                if (scope == 2) _store.Remove(src);
                else _store.RemoveOccurrence(o);
                RefreshAll();
                return;
            }

            if (MessageBox.Show(this, "确定删除「" + o.Title + "」吗？", "删除日程",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                _store.Remove(src);
                RefreshAll();
            }
        }

        private void ShowDayMenu(DateTime day, Occurrence occ, Point at)
        {
            ContextMenuStrip m = NewThemeMenu();
            m.Items.Add("新建日程", null, delegate { NewEventAt(day.AddHours(9)); });
            m.Items.Add("新建全天日程", null, delegate { NewEventAt(day, true); });
            if (occ != null)
            {
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("编辑「" + occ.Title + "」", null, delegate { EditOccurrence(occ); });
                m.Items.Add("删除「" + occ.Title + "」", null, delegate { DeleteOccurrence(occ); });
            }
            m.Show(_monthView, at);
        }

        private void ShowSlotMenu(DateTime day, Occurrence occ, Point at)
        {
            ContextMenuStrip m = NewThemeMenu();
            m.Items.Add("新建日程", null, delegate { NewEventAt(day.Date.AddHours(9)); });
            if (occ != null)
            {
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("编辑「" + occ.Title + "」", null, delegate { EditOccurrence(occ); });
                m.Items.Add("删除「" + occ.Title + "」", null, delegate { DeleteOccurrence(occ); });
            }
            m.Show(_timeView, at);
        }

        private void ShowMoreMenu()
        {
            ContextMenuStrip m = NewThemeMenu();
            m.Items.Add("导入 ICS 日历…", null, delegate { ImportIcs(); });
            m.Items.Add("导出全部日程为 ICS…", null, delegate { ExportIcs(); });
            m.Items.Add(BuildThemeMenu());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("打开数据文件夹", null, delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", _store.DataDir); } catch { }
            });
            m.Items.Add("关于轻历", null, delegate
            {
                MessageBox.Show(this,
                    "轻历 1.2.0\n\n一个本地优先的 Windows 日程应用：月 / 周 / 日视图、重复日程、到点提醒、ICS 导入导出、中国法定节假日与调休展示。\n节假日数据自动从公开数据源拉取并本地缓存，断网时使用内置数据；搜索支持 calendar / calander 英文关键词。\n主题支持跟随系统 / 始终浅色 / 始终深色（菜单即时切换，Ctrl+Shift+L 循环）。\n数据目录：" + _store.DataDir,
                    "关于轻历", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            m.Show(_more, new Point(0, _more.Height + Theme.S(2)));
        }

        /// <summary>按当前主题创建一个统一着色的右键菜单（主窗口 / 月视图 / 周视图 / 托盘共用）。</summary>
        private static ContextMenuStrip NewThemeMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ThemeMenuRenderer();
            m.BackColor = Theme.Surface;
            m.ForeColor = Theme.TextPrimary;
            m.ShowImageMargin = false;
            return m;
        }

        /// <summary>「主题」子菜单：主窗口「更多」与托盘右键共用同一份构建逻辑，顺序固定。</summary>
        private ToolStripMenuItem BuildThemeMenu()
        {
            ToolStripMenuItem parent = new ToolStripMenuItem("主题");
            parent.DropDown.BackColor = Theme.Surface;
            parent.DropDown.ForeColor = Theme.TextPrimary;
            parent.DropDownItems.Add(MakeThemeItem("跟随系统", ThemeMode.System));
            parent.DropDownItems.Add(MakeThemeItem("始终浅色", ThemeMode.Light));
            parent.DropDownItems.Add(MakeThemeItem("始终深色", ThemeMode.Dark));
            return parent;
        }

        private ToolStripMenuItem MakeThemeItem(string text, ThemeMode mode)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.CheckOnClick = false;
            item.Checked = Theme.Mode == mode;
            item.Tag = mode;
            item.Click += delegate { ApplyThemeMode((ThemeMode)item.Tag); };
            return item;
        }

        /// <summary>切换档位：立即应用 + 立即写盘 + 同步菜单勾选 + 状态栏提示，无需重启。</summary>
        private void ApplyThemeMode(ThemeMode mode)
        {
            Theme.Apply(mode);

            _store.Data.Settings.Theme = Theme.ModeToString(mode);
            _store.Save();

            SyncThemeChecks(_trayThemeItem);
            Theme.ApplyTitleBar(Handle);
            ShowThemeHint("已切换到「" + ThemeModeText(mode) + "」");
        }

        private static string ThemeModeText(ThemeMode mode)
        {
            if (mode == ThemeMode.Light) return "始终浅色";
            if (mode == ThemeMode.Dark) return "始终深色";
            return "跟随系统";
        }

        /// <summary>同步「主题」子菜单的勾选状态（菜单展开前与切换后都要调用）。</summary>
        private void SyncThemeChecks(ToolStripMenuItem parent)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.DropDownItems.Count; i++)
            {
                ToolStripMenuItem item = parent.DropDownItems[i] as ToolStripMenuItem;
                if (item == null || !(item.Tag is ThemeMode)) continue;
                item.Checked = Theme.Mode == (ThemeMode)item.Tag;
            }
        }

        /// <summary>状态栏左侧临时提示 2 秒，结束后恢复「数据文件：...」。</summary>
        private void ShowThemeHint(string text)
        {
            if (_statusLeft == null) return;
            _statusLeft.Text = text;
            _hintTimer.Stop();
            _hintTimer.Start();
        }

        // ---------------- 搜索 ----------------

        private void UpdateSearch()
        {
            string q = _search.Text.Trim();
            if (q.Length == 0)
            {
                _searchPanel.Visible = false;
                return;
            }

            List<SearchHit> hits = new List<SearchHit>();

            // 1) calendar / calander 英文关键词：作为命令置顶（两种拼写等价）
            if (HolidayService.IsCalendarKeyword(q))
            {
                SearchHit cmd = new SearchHit();
                cmd.Title = "回到今天（Calendar）";
                cmd.Subtitle = "回车或点击跳转到今天 · 支持 calendar / calander 拼写";
                cmd.IsCalendarCommand = true;
                hits.Add(cmd);
            }

            // 2) 节假日 / 调休：中文名与英文别名均可命中（如 national day → 国庆节）
            List<HolidayInfo> hols = _holidays.SearchHolidays(q, 3);
            for (int i = 0; i < hols.Count; i++)
            {
                HolidayInfo hi = hols[i];
                SearchHit h = new SearchHit();
                h.Title = hi.Name + (hi.IsOffDay ? " · 法定放假" : " · 调休上班");
                h.Subtitle = hi.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " +
                             DateField.WeekName(hi.Date) + " · 点击跳转到这一天";
                h.IsHoliday = true;
                h.JumpDate = hi.Date;
                hits.Add(h);
            }

            // 3) 日程：按标题 / 地点 / 备注过滤，按接近现在排序
            List<SearchHit> eventHits = new List<SearchHit>();
            DateTime now = DateTime.Now;
            for (int i = 0; i < _store.Data.Events.Count; i++)
            {
                EventItem e = _store.Data.Events[i];
                if (!Contains(e.Title, q) && !Contains(e.Location, q) && !Contains(e.Notes, q)) continue;
                SearchHit h = new SearchHit();
                h.Title = e.Title;
                string sub = e.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (e.Recurrence != RecurrenceType.None) sub += " · " + e.RecurrenceText;
                if (!string.IsNullOrEmpty(e.Location)) sub += " · " + e.Location;
                h.Subtitle = sub;
                h.ColorIndex = e.ColorIndex;
                h.Event = e;
                eventHits.Add(h);
            }
            eventHits.Sort(delegate (SearchHit a, SearchHit b)
            {
                long da = Math.Abs((a.Event.Start - now).Ticks);
                long db = Math.Abs((b.Event.Start - now).Ticks);
                return da.CompareTo(db);
            });
            hits.AddRange(eventHits);

            if (hits.Count > 8) hits = hits.GetRange(0, 8);

            _searchList.SetItems(hits);
            _searchPanel.Visible = true;
            PositionSearchPanel();
        }

        private static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _search.Text = "";
                _searchPanel.Visible = false;
                _content.Focus();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                SearchHit hit = _searchList.Current;
                if (hit != null) OpenFromSearch(hit);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                _searchList.SelectedIndex = _searchList.SelectedIndex + 1;
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Up)
            {
                _searchList.SelectedIndex = _searchList.SelectedIndex - 1;
                e.Handled = true;
            }
        }

        private void OpenFromSearch(SearchHit hit)
        {
            if (hit == null) return;
            _search.Text = "";
            _searchPanel.Visible = false;

            // calendar / calander 命令：回到今天
            if (hit.IsCalendarCommand)
            {
                GoToday();
                return;
            }

            // 节假日：跳转到对应日期
            if (hit.IsHoliday)
            {
                GoToDate(hit.JumpDate);
                return;
            }

            // 日程：跳转并打开编辑
            GoToDate(hit.Event.Start.Date);
            EventItem fresh = _store.Find(hit.Event.Id);
            if (fresh == null) return;
            Occurrence occ = new Occurrence();
            occ.Source = fresh;
            occ.Start = fresh.Start;
            occ.End = fresh.End;
            occ.AllDay = fresh.AllDay;
            occ.IsRecurringInstance = false;
            EditOccurrence(occ);
        }

        // ---------------- 导入导出 ----------------

        private void ExportIcs()
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Title = "导出为 ICS";
            d.Filter = "iCalendar 文件 (*.ics)|*.ics";
            d.FileName = "轻历-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".ics";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _store.ExportIcs(_store.Data.Events, d.FileName);
                _store.Data.Settings.LastExportPath = d.FileName;
                _store.Save();
                MessageBox.Show(this, "已导出 " + _store.Data.Events.Count + " 条日程到：\n" + d.FileName,
                    "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportIcs()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Title = "导入 ICS 日历";
            d.Filter = "iCalendar 文件 (*.ics)|*.ics|所有文件 (*.*)|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                List<EventItem> items = Ics.Parse(d.FileName);
                int added = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    EventItem e = items[i];
                    bool dup = false;
                    for (int j = 0; j < _store.Data.Events.Count; j++)
                    {
                        EventItem x = _store.Data.Events[j];
                        if (x.Title == e.Title && x.Start == e.Start) { dup = true; break; }
                    }
                    if (dup) continue;
                    e.Id = Guid.NewGuid().ToString("N");
                    _store.Data.Events.Add(e);
                    added++;
                }
                _store.Save();
                RefreshAll();
                MessageBox.Show(this, string.Format("读取 {0} 个事件，新增 {1} 条（重复已跳过）。", items.Count, added),
                    "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：" + ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------- 提醒 / 托盘 ----------------

        private void OnReminderDue(Occurrence o)
        {
            ToastForm t = new ToastForm(o);
            t.OpenRequested += delegate (Occurrence occ) { RestoreFromTray(); GoToDate(occ.Start.Date); };
            t.SnoozeRequested += delegate (Occurrence occ) { _reminders.Snooze(occ, 5); };
            t.Show();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && _store.Data.Settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!_balloonShown)
                {
                    _balloonShown = true;
                    try
                    {
                        _tray.ShowBalloonTip(2500, "轻历仍在后台运行", "提醒会继续生效，双击托盘图标可重新打开。", ToolTipIcon.Info);
                    }
                    catch { }
                }
                return;
            }
            _store.Save();
            base.OnFormClosing(e);
        }

        /// <summary>主题变化后的统一处理：重新着色主窗口与原生控件、同步标题栏与菜单勾选、通知已打开的其他窗口。</summary>
        private void OnThemeChanged(object sender, EventArgs e)
        {
            Ui.StyleForm(this);

            _top.BackColor = Theme.Window;
            _sidebar.BackColor = Theme.Window;
            _content.BackColor = Theme.Window;
            _status.BackColor = Theme.Mix(Theme.Window, Theme.Surface, 0.6f);
            _searchPanel.BackColor = Theme.Surface;
            _search.BackColor = Theme.Surface;
            _search.ForeColor = Theme.TextPrimary;
            _searchBox.BackColor = Theme.Window;

            // 托盘菜单：BackColor/ForeColor 必须重设（只在 BuildTray 写一次会残留旧配色）
            if (_trayMenu != null)
            {
                _trayMenu.BackColor = Theme.Surface;
                _trayMenu.ForeColor = Theme.TextPrimary;
                _trayMenu.Renderer = new ThemeMenuRenderer();
                if (_trayThemeItem != null)
                {
                    _trayThemeItem.DropDown.BackColor = Theme.Surface;
                    _trayThemeItem.DropDown.ForeColor = Theme.TextPrimary;
                    SyncThemeChecks(_trayThemeItem);
                }
            }

            Theme.ApplyTitleBar(Handle);
            Ui.RepaintTheme(this);

            for (int i = 0; i < Application.OpenForms.Count; i++)
            {
                Form f = Application.OpenForms[i];
                if (f != null && f != this) f.Invalidate(true);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= OnThemeChanged;
            if (_themeWatch != null) _themeWatch.Dispose();
            if (_hintTimer != null) _hintTimer.Dispose();
            base.OnFormClosed(e);
        }

        // 系统深浅色切换广播：WM_SETTINGCHANGE + lParam = "ImmersiveColorSet"
        private const int WM_SETTINGCHANGE = 0x001A;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_SETTINGCHANGE) return;
            string area = "";
            try
            {
                if (m.LParam != IntPtr.Zero) area = Marshal.PtrToStringAuto(m.LParam);
            }
            catch
            {
                return;
            }
            if (!string.Equals(area, "ImmersiveColorSet", StringComparison.OrdinalIgnoreCase)) return;
            if (_themeWatch == null) return;
            _themeWatch.Stop();
            _themeWatch.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(Handle);
        }

        // ---------------- 快捷键 ----------------

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            bool typing = _search.Focused || _titleBoxFocused();
            if (e.Control && e.KeyCode == Keys.N)
            {
                NewEventAt(_selected.Date.AddHours(9));
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.F)
            {
                _search.Focus();
                _search.SelectAll();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.D1) { _seg.SelectedIndex = 0; e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.D2) { _seg.SelectedIndex = 1; e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.D3) { _seg.SelectedIndex = 2; e.Handled = true; return; }
            if (e.Control && e.Shift && e.KeyCode == Keys.L)
            {
                ThemeMode next = Theme.Mode == ThemeMode.System ? ThemeMode.Light
                    : Theme.Mode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.System;
                ApplyThemeMode(next);
                e.Handled = true;
                return;
            }
            if (typing) return;

            if (e.KeyCode == Keys.T)
            {
                GoToday();
                e.Handled = true;
                return;
            }
            if (_view == CalendarView.Month)
            {
                if (e.KeyCode == Keys.Left) { _monthView.MoveSelection(-1); _selected = _monthView.SelectedDate; _anchor = _monthView.AnchorDate; RefreshAll(); e.Handled = true; }
                else if (e.KeyCode == Keys.Right) { _monthView.MoveSelection(1); _selected = _monthView.SelectedDate; _anchor = _monthView.AnchorDate; RefreshAll(); e.Handled = true; }
                else if (e.KeyCode == Keys.Up) { _monthView.MoveSelection(-7); _selected = _monthView.SelectedDate; _anchor = _monthView.AnchorDate; RefreshAll(); e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { _monthView.MoveSelection(7); _selected = _monthView.SelectedDate; _anchor = _monthView.AnchorDate; RefreshAll(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { NewEventAt(_selected.Date.AddHours(9)); e.Handled = true; }
            }
        }

        private bool _titleBoxFocused()
        {
            Control c = ActiveControl;
            return c is TextBox;
        }
    }

    /// <summary>重复日程作用范围选择：0 取消 / 1 仅此一次 / 2 整个系列。</summary>
    public class ScopeDialog : Form
    {
        private int _result;

        public static int Ask(IWin32Window owner, string title, string message)
        {
            using (ScopeDialog d = new ScopeDialog(title, message))
            {
                d.ShowDialog(owner);
                return d._result;
            }
        }

        private ScopeDialog(string title, string message)
        {
            Ui.StyleForm(this);
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(420), Theme.S(170));
            DoubleBuffered = true;

            Label l = new Label();
            l.AutoSize = false;
            l.Text = message;
            l.ForeColor = Theme.TextPrimary;
            l.Tag = ThemeRole.Primary;
            l.BackColor = Color.Transparent;
            l.Font = Theme.FBody;
            l.SetBounds(Theme.S(20), Theme.S(20), Theme.S(380), Theme.S(52));
            Controls.Add(l);

            UiButton once = new UiButton();
            once.Text = "仅此一次";
            once.Style = ButtonStyle.Secondary;
            once.SetBounds(Theme.S(20), Theme.S(96), Theme.S(110), Theme.S(34));
            once.Click += delegate { _result = 1; Close(); };
            Controls.Add(once);

            UiButton all = new UiButton();
            all.Text = "整个系列";
            all.Style = ButtonStyle.Primary;
            all.SetBounds(Theme.S(140), Theme.S(96), Theme.S(110), Theme.S(34));
            all.Click += delegate { _result = 2; Close(); };
            Controls.Add(all);

            UiButton cancel = new UiButton();
            cancel.Text = "取消";
            cancel.Style = ButtonStyle.Ghost;
            cancel.SetBounds(Theme.S(290), Theme.S(96), Theme.S(110), Theme.S(34));
            cancel.Click += delegate { _result = 0; Close(); };
            Controls.Add(cancel);

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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(Handle);
        }
    }

    /// <summary>主题化菜单渲染：所有色值取自令牌，切换主题后重新展开即为新配色。</summary>
    public class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemeMenuRenderer() : base(new ThemeColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.OnAccent : Theme.TextPrimary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextPrimary;
            base.OnRenderArrow(e);
        }
    }

    public class ThemeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.AccentFill; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.AccentFill; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.AccentFill; } }
        public override Color MenuItemBorder { get { return Theme.AccentFill; } }
        public override Color MenuBorder { get { return Theme.BorderStrong; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
    }
}
