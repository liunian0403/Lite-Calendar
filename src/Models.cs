using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace QingLi
{
    public enum RecurrenceType
    {
        None = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Weekdays = 4
    }

    public enum CalendarView
    {
        Month = 0,
        Week = 1,
        Day = 2
    }

    /// <summary>一条日程（重复日程只存一条主记录）。</summary>
    public class EventItem
    {
        [XmlAttribute]
        public string Id = "";

        public string Title = "";
        public DateTime Start = DateTime.Now;
        public DateTime End = DateTime.Now.AddHours(1);
        public bool AllDay = false;
        public string Location = "";
        public string Notes = "";

        /// <summary>-1 = 不提醒；0 = 开始时；其余为提前分钟数。</summary>
        public int ReminderMinutes = -1;

        public RecurrenceType Recurrence = RecurrenceType.None;
        public DateTime RecurrenceUntil = DateTime.MinValue;

        /// <summary>被单独删除（或改成独立日程）的重复实例，按原始发生时间记录。</summary>
        public List<DateTime> ExcludedStarts = new List<DateTime>();

        /// <summary>配色索引，用于事件色块。</summary>
        public int ColorIndex = 0;

        public EventItem Clone()
        {
            EventItem c = new EventItem();
            c.Id = Id;
            c.Title = Title;
            c.Start = Start;
            c.End = End;
            c.AllDay = AllDay;
            c.Location = Location;
            c.Notes = Notes;
            c.ReminderMinutes = ReminderMinutes;
            c.Recurrence = Recurrence;
            c.RecurrenceUntil = RecurrenceUntil;
            c.ColorIndex = ColorIndex;
            c.ExcludedStarts = new List<DateTime>(ExcludedStarts);
            return c;
        }

        public TimeSpan Duration
        {
            get
            {
                TimeSpan d = End - Start;
                if (d <= TimeSpan.Zero) d = AllDay ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(30);
                return d;
            }
        }

        public int DayCount
        {
            get
            {
                int n = (int)(End.Date - Start.Date).TotalDays + 1;
                return n < 1 ? 1 : n;
            }
        }

        public string RecurrenceText
        {
            get
            {
                switch (Recurrence)
                {
                    case RecurrenceType.Daily: return "每天";
                    case RecurrenceType.Weekly: return "每周";
                    case RecurrenceType.Monthly: return "每月";
                    case RecurrenceType.Weekdays: return "工作日";
                    default: return "不重复";
                }
            }
        }
    }

    /// <summary>展开后的一次具体发生。</summary>
    public class Occurrence
    {
        public EventItem Source;
        public DateTime Start;
        public DateTime End;
        public bool AllDay;
        public bool IsRecurringInstance;

        public string Title { get { return Source.Title; } }
        public int ColorIndex { get { return Source.ColorIndex; } }
        public TimeSpan Duration { get { return Source.Duration; } }

        public DateTime EffectiveEnd
        {
            get
            {
                if (AllDay)
                {
                    DateTime e = End.Date;
                    if (e <= Start.Date) e = Start.Date.AddDays(1);
                    return e;
                }
                return End;
            }
        }
    }

    public class AppSettings
    {
        public int View = 0;
        public int DefaultReminderMinutes = 10;
        public bool MinimizeToTray = true;
        public bool CloseToTray = true;
        public string LastExportPath = "";

        /// <summary>主题档位：system / light / dark。空串表示未设置（区分全新安装与升级，见 Theme.ResolveInitialMode）。</summary>
        public string Theme = "";
    }

    [XmlRoot("QingLiData")]
    public class AppData
    {
        public int Version = 1;
        public AppSettings Settings = new AppSettings();
        public List<EventItem> Events = new List<EventItem>();
    }

    /// <summary>本地持久化：%APPDATA%\QingLi\data.xml</summary>
    public class EventStore
    {
        public AppData Data;
        public readonly string DataDir;
        public readonly string DataFile;

        /// <summary>加载前 data.xml 是否已存在（全新安装判定）。</summary>
        public bool FileExistedAtLoad;

        /// <summary>加载的文件里是否出现过 &lt;Theme&gt; 元素（1.1.0 升级判定）。</summary>
        public bool FileHasThemeElement;

        /// <summary>加载失败（XML 非法）后按全新安装处理，避免把损坏文件当成升级。</summary>
        public bool LoadFailed;

        public event EventHandler Changed;

        public EventStore() : this(null)
        {
        }

        public EventStore(string dataDir)
        {
            DataDir = string.IsNullOrEmpty(dataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QingLi")
                : dataDir;
            DataFile = Path.Combine(DataDir, "data.xml");
            Data = new AppData();
        }

        public void Load()
        {
            FileExistedAtLoad = false;
            FileHasThemeElement = false;
            LoadFailed = false;
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                if (File.Exists(DataFile))
                {
                    FileExistedAtLoad = true;
                    try
                    {
                        string raw = File.ReadAllText(DataFile);
                        FileHasThemeElement = raw.IndexOf("<Theme", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch
                    {
                    }
                    using (FileStream fs = new FileStream(DataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        XmlSerializer ser = new XmlSerializer(typeof(AppData));
                        AppData d = (AppData)ser.Deserialize(fs);
                        if (d != null) Data = d;
                    }
                }
            }
            catch (Exception)
            {
                LoadFailed = true;
                FileExistedAtLoad = false;
                FileHasThemeElement = false;
                try
                {
                    if (File.Exists(DataFile))
                        File.Copy(DataFile, DataFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                }
                catch
                {
                }
                Data = new AppData();
            }

            if (Data.Events == null) Data.Events = new List<EventItem>();
            if (Data.Settings == null) Data.Settings = new AppSettings();
            for (int i = 0; i < Data.Events.Count; i++)
            {
                EventItem e = Data.Events[i];
                if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
                if (e.ExcludedStarts == null) e.ExcludedStarts = new List<DateTime>();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                string tmp = DataFile + ".tmp";
                XmlSerializer ser = new XmlSerializer(typeof(AppData));
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    ser.Serialize(fs, Data);
                }
                if (File.Exists(DataFile))
                {
                    try
                    {
                        File.Replace(tmp, DataFile, DataFile + ".bak", true);
                    }
                    catch
                    {
                        File.Copy(tmp, DataFile, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, DataFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存失败: " + ex.Message);
            }

            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void Add(EventItem e)
        {
            if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
            Data.Events.Add(e);
            Save();
        }

        public void Update(EventItem e)
        {
            for (int i = 0; i < Data.Events.Count; i++)
            {
                if (Data.Events[i].Id == e.Id)
                {
                    Data.Events[i] = e;
                    Save();
                    return;
                }
            }
            Add(e);
        }

        public void Remove(EventItem e)
        {
            Data.Events.RemoveAll(delegate (EventItem x) { return x.Id == e.Id; });
            Save();
        }

        public EventItem Find(string id)
        {
            for (int i = 0; i < Data.Events.Count; i++)
                if (Data.Events[i].Id == id) return Data.Events[i];
            return null;
        }

        /// <summary>删除某一次发生：重复日程只排除该次，单次日程直接删除。</summary>
        public void RemoveOccurrence(Occurrence occ)
        {
            if (occ == null || occ.Source == null) return;
            if (occ.Source.Recurrence == RecurrenceType.None || !occ.IsRecurringInstance)
            {
                Remove(occ.Source);
                return;
            }
            EventItem e = Find(occ.Source.Id);
            if (e == null) return;
            DateTime key = AlignKey(e, occ.Start);
            bool exists = false;
            for (int i = 0; i < e.ExcludedStarts.Count; i++)
                if (e.ExcludedStarts[i] == key) exists = true;
            if (!exists) e.ExcludedStarts.Add(key);
            Save();
        }

        public static DateTime AlignKey(EventItem e, DateTime start)
        {
            if (e.AllDay) return start.Date;
            return start;
        }

        public List<Occurrence> ExpandRange(DateTime from, DateTime to)
        {
            List<Occurrence> list = new List<Occurrence>();
            for (int i = 0; i < Data.Events.Count; i++)
            {
                EventItem e = Data.Events[i];
                ExpandOne(e, from, to, list);
            }
            list.Sort(delegate (Occurrence a, Occurrence b)
            {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) return c;
                if (a.AllDay != b.AllDay) return a.AllDay ? -1 : 1;
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
            });
            return list;
        }

        private static void ExpandOne(EventItem e, DateTime from, DateTime to, List<Occurrence> output)
        {
            TimeSpan duration = e.Duration;

            if (e.Recurrence == RecurrenceType.None)
            {
                if (Overlaps(e.Start, duration, from, to, e.AllDay)) output.Add(Make(e, e.Start, duration, false));
                return;
            }

            DateTime baseStart = e.Start;
            DateTime until = e.RecurrenceUntil;

            if (e.Recurrence == RecurrenceType.Daily || e.Recurrence == RecurrenceType.Weekly || e.Recurrence == RecurrenceType.Weekdays)
            {
                int step = e.Recurrence == RecurrenceType.Weekly ? 7 : 1;
                DateTime cur = e.Start;
                int guard = 0;
                while (cur < to && guard < 20000)
                {
                    guard++;
                    if (until != DateTime.MinValue && cur.Date > until.Date) break;
                    if (e.Recurrence == RecurrenceType.Weekdays && (cur.DayOfWeek == DayOfWeek.Saturday || cur.DayOfWeek == DayOfWeek.Sunday))
                    {
                        cur = cur.AddDays(1);
                        continue;
                    }
                    if (Overlaps(cur, duration, from, to, e.AllDay) && !IsExcluded(e, cur))
                        output.Add(Make(e, cur, duration, true));
                    cur = cur.AddDays(step);
                }
                return;
            }

            // 每月：按"日"对齐，月末自动收敛（31 号 → 该月最后一天），避免逐月漂移
            for (int n = 0; n < 1200; n++)
            {
                int total = (baseStart.Year * 12 + baseStart.Month - 1) + n;
                int year = total / 12;
                int month = total % 12 + 1;
                if (year > 2100) break;
                int day = Math.Min(baseStart.Day, DateTime.DaysInMonth(year, month));
                DateTime cur = new DateTime(year, month, day, baseStart.Hour, baseStart.Minute, baseStart.Second);
                if (until != DateTime.MinValue && cur.Date > until.Date) break;
                if (cur >= to) break;
                if (Overlaps(cur, duration, from, to, e.AllDay) && !IsExcluded(e, cur))
                    output.Add(Make(e, cur, duration, true));
            }
        }

        private static bool IsExcluded(EventItem e, DateTime start)
        {
            DateTime key = AlignKey(e, start);
            for (int i = 0; i < e.ExcludedStarts.Count; i++)
                if (e.ExcludedStarts[i] == key) return true;
            return false;
        }

        private static bool Overlaps(DateTime start, TimeSpan duration, DateTime from, DateTime to, bool allDay)
        {
            DateTime end = allDay ? start.Date.AddDays(Math.Max(1, (int)Math.Ceiling(duration.TotalDays))) : start + duration;
            if (allDay && duration.TotalDays < 1) end = start.Date.AddDays(1);
            return start < to && end > from;
        }

        private static Occurrence Make(EventItem e, DateTime start, TimeSpan duration, bool recurring)
        {
            Occurrence o = new Occurrence();
            o.Source = e;
            o.Start = start;
            o.AllDay = e.AllDay;
            o.IsRecurringInstance = recurring;
            if (e.AllDay)
            {
                int days = Math.Max(1, e.DayCount);
                o.End = start.Date.AddDays(days).AddSeconds(-1);
            }
            else
            {
                o.End = start + duration;
            }
            return o;
        }

        // ---------- 导入导出 ----------
        public string ExportIcs(System.Collections.Generic.List<EventItem> items, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//QingLi//Calendar 1.0//CN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine("X-WR-CALNAME:轻历");
            for (int i = 0; i < items.Count; i++)
            {
                EventItem e = items[i];
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine("UID:" + e.Id + "@qingli");
                sb.AppendLine("DTSTAMP:" + Ics.UtcStamp(DateTime.UtcNow));
                if (e.AllDay)
                {
                    sb.AppendLine("DTSTART;VALUE=DATE:" + e.Start.ToString("yyyyMMdd"));
                    sb.AppendLine("DTEND;VALUE=DATE:" + e.End.Date.AddDays(1).ToString("yyyyMMdd"));
                }
                else
                {
                    sb.AppendLine("DTSTART:" + Ics.UtcStamp(e.Start.ToUniversalTime()));
                    sb.AppendLine("DTEND:" + Ics.UtcStamp(e.End.ToUniversalTime()));
                }
                sb.AppendLine("SUMMARY:" + Ics.Escape(e.Title));
                if (!string.IsNullOrEmpty(e.Location)) sb.AppendLine("LOCATION:" + Ics.Escape(e.Location));
                if (!string.IsNullOrEmpty(e.Notes)) sb.AppendLine("DESCRIPTION:" + Ics.Escape(e.Notes));
                if (e.ReminderMinutes >= 0)
                {
                    sb.AppendLine("BEGIN:VALARM");
                    sb.AppendLine("ACTION:DISPLAY");
                    sb.AppendLine("DESCRIPTION:" + Ics.Escape(e.Title));
                    sb.AppendLine("TRIGGER:-PT" + e.ReminderMinutes + "M");
                    sb.AppendLine("END:VALARM");
                }
                string rrule = Ics.BuildRRule(e);
                if (rrule != null) sb.AppendLine(rrule);
                sb.AppendLine("END:VEVENT");
            }
            sb.AppendLine("END:VCALENDAR");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
