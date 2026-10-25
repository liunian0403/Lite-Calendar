using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace QingLi
{
    /// <summary>一天的节假日信息。</summary>
    public class HolidayInfo
    {
        /// <summary>节日名称，如“国庆节”。</summary>
        public string Name = "";

        /// <summary>true = 法定放假休息；false = 调休上班（周末补班）。</summary>
        public bool IsOffDay = true;

        /// <summary>所在日期（仅日期部分有效）。</summary>
        public DateTime Date;
    }

    /// <summary>
    /// 中国法定节假日与调休数据服务。
    /// 数据源：holiday-cn 公开数据（依据国务院办公厅节假日安排公告整理），按年从公开 CDN 拉取：
    ///   1. cdn.jsdelivr.net（国内可访问）
    ///   2. fastly.jsdelivr.net（jsDelivr 备用节点）
    ///   3. raw.githubusercontent.com（源站直连）
    /// 拉取成功后缓存到 %APPDATA%\QingLi\holidays-{year}.json；网络不可用时依次回退：
    /// 本地缓存 → 编译期内置快照（src\HolidayData.cs）。任何一层都保证节假日可展示。
    /// </summary>
    public class HolidayService
    {
        private static readonly string[] SourceTemplates = new string[]
        {
            "https://cdn.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
            "https://fastly.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
            "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{0}.json"
        };

        /// <summary>缓存与数据目录（默认 %APPDATA%\QingLi）。</summary>
        public readonly string DataDir;

        private readonly Dictionary<DateTime, HolidayInfo> _days = new Dictionary<DateTime, HolidayInfo>();
        private readonly object _lock = new object();
        private string _status = "内置数据";
        private int _refreshing;

        /// <summary>后台刷新完成后触发（可能在工作线程上，订阅方负责切换到 UI 线程）。</summary>
        public event Action Updated;

        public HolidayService()
            : this(null)
        {
        }

        public HolidayService(string dataDir)
        {
            DataDir = string.IsNullOrEmpty(dataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QingLi")
                : dataDir;
            // 老版本 .NET Framework 默认协议可能不含 TLS 1.2，显式补上（失败则维持系统默认）。
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
            catch { }
        }

        /// <summary>数据来源描述，用于状态栏展示，如“已联网更新 10-25 14:30”“本地缓存”“内置数据”。</summary>
        public string StatusText
        {
            get { lock (_lock) { return _status; } }
        }

        /// <summary>载入数据：先内置快照，再用本地缓存逐年覆盖。可在无网络环境安全调用。</summary>
        public void Load()
        {
            lock (_lock)
            {
                _days.Clear();
                _status = "内置数据";
                MergeJsonNoLock(HolidayData.DefaultJson);

                try
                {
                    if (Directory.Exists(DataDir))
                    {
                        string[] files = Directory.GetFiles(DataDir, "holidays-*.json");
                        Array.Sort(files, StringComparer.Ordinal);
                        for (int i = 0; i < files.Length; i++)
                        {
                            try
                            {
                                if (MergeJsonNoLock(File.ReadAllText(files[i], Encoding.UTF8)) > 0)
                                    _status = "本地缓存";
                            }
                            catch
                            {
                                // 缓存损坏时忽略该文件，继续用内置数据。
                            }
                        }
                    }
                }
                catch
                {
                    // 目录不可读等情况直接忽略。
                }
            }
        }

        /// <summary>后台拉取当年与次年数据并合并、写缓存。重复调用会被合并（刷新中再调用直接返回）。</summary>
        public void RefreshAsync()
        {
            if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0) return;
            Thread t = new Thread(delegate()
            {
                bool fetched = false;
                try
                {
                    int year0 = DateTime.Today.Year;
                    for (int year = year0; year <= year0 + 1; year++)
                    {
                        string json = FetchYear(year);
                        if (json == null) continue;
                        try
                        {
                            lock (_lock)
                            {
                                if (MergeJsonNoLock(json) > 0) fetched = true;
                            }
                            try
                            {
                                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                                File.WriteAllText(CacheFile(year), json, Encoding.UTF8);
                            }
                            catch { /* 缓存写失败不影响本次会话使用 */ }
                        }
                        catch
                        {
                            // 该年份数据格式异常，跳过。
                        }
                    }
                    if (fetched)
                    {
                        lock (_lock)
                        {
                            _status = "已联网更新 " + DateTime.Now.ToString("M-d HH:mm", CultureInfo.InvariantCulture);
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshing, 0);
                    if (Updated != null) Updated();
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>查询某天的节假日信息；非节假日返回 null。</summary>
        public HolidayInfo GetDay(DateTime day)
        {
            lock (_lock)
            {
                HolidayInfo hi;
                return _days.TryGetValue(day.Date, out hi) ? hi : null;
            }
        }

        /// <summary>搜索节假日：命中节日名（中文或内置英文别名）的下一个日期。
        /// 同名节日只返回放假日（无放假记录时才回退调休日）。query 为 holiday/vacation/节假 时列出全部。</summary>
        public List<HolidayInfo> SearchHolidays(string query, int maxCount)
        {
            return SearchHolidays(query, DateTime.Today, maxCount);
        }

        /// <summary>搜索节假日（指定起始日，便于测试与复用）。
        /// 优先返回未来两年内的下一个日期；若无未来命中（如次年安排尚未公布），
        /// 回退返回最近一年内已发生的记录，避免搜索结果为空的困惑。</summary>
        public List<HolidayInfo> SearchHolidays(string query, DateTime from, int maxCount)
        {
            List<HolidayInfo> all;
            lock (_lock)
            {
                all = new List<HolidayInfo>(_days.Values);
            }
            all.Sort(delegate (HolidayInfo a, HolidayInfo b) { return a.Date.CompareTo(b.Date); });

            DateTime start = from.Date;
            List<HolidayInfo> result = SearchRange(all, query, start, start.AddYears(2), maxCount);
            if (result.Count == 0)
                result = SearchRange(all, query, start.AddDays(-366), start, maxCount);
            return result;
        }

        /// <summary>在 [start, end] 闭区间内搜索：同名节日优先保留最早的放假记录（无放假记录时保留最早调休记录）。</summary>
        private static List<HolidayInfo> SearchRange(List<HolidayInfo> all, string query, DateTime start, DateTime end, int maxCount)
        {
            bool wildcard = IsHolidayWildcard(query);
            Dictionary<string, HolidayInfo> best = new Dictionary<string, HolidayInfo>(StringComparer.CurrentCulture);
            for (int i = 0; i < all.Count; i++)
            {
                HolidayInfo hi = all[i];
                if (hi.Date < start || hi.Date > end) continue;
                if (!wildcard && !NameMatches(hi, query)) continue;

                HolidayInfo cur;
                if (!best.TryGetValue(hi.Name, out cur))
                {
                    best[hi.Name] = hi;
                }
                else if (hi.IsOffDay && !cur.IsOffDay)
                {
                    best[hi.Name] = hi;
                }
            }

            List<HolidayInfo> result = new List<HolidayInfo>(best.Values);
            result.Sort(delegate (HolidayInfo a, HolidayInfo b) { return a.Date.CompareTo(b.Date); });
            if (maxCount > 0 && result.Count > maxCount) result.RemoveRange(maxCount, result.Count - maxCount);
            return result;
        }

        /// <summary>“calendar”及常见拼写变体（calander / cal）视为同一关键词。</summary>
        public static bool IsCalendarKeyword(string q)
        {
            if (q == null) return false;
            q = q.Trim().ToLowerInvariant();
            return q == "calendar" || q == "calander" || q == "cal";
        }

        /// <summary>列出全部即将到来的节假日（搜索词为 holiday/vacation/节假 时的通配语义）。</summary>
        public static bool IsHolidayWildcard(string q)
        {
            if (q == null) return false;
            q = q.Trim().ToLowerInvariant();
            return q == "holiday" || q == "vacation" || q == "节假" || q == "节假日" || q == "假期";
        }

        /// <summary>节日名匹配：支持中文包含匹配与内置英文别名（如 national day → 国庆节）。</summary>
        public static bool NameMatches(HolidayInfo info, string query)
        {
            if (info == null || string.IsNullOrEmpty(query)) return false;
            string q = query.Trim();
            if (q.Length == 0) return false;
            if (info.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;

            string[] aliases = EnglishAliases(info.Name);
            for (int i = 0; i < aliases.Length; i++)
            {
                string a = aliases[i];
                if (a.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (q.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string[] EnglishAliases(string chineseName)
        {
            switch (chineseName)
            {
                case "元旦": return new string[] { "new year" };
                case "春节": return new string[] { "spring festival", "chinese new year", "cny" };
                case "清明节": return new string[] { "qingming", "tomb sweeping" };
                case "劳动节": return new string[] { "labour day", "labor day", "may day" };
                case "端午节": return new string[] { "dragon boat" };
                case "中秋节": return new string[] { "mid autumn", "moon festival" };
                case "国庆节": return new string[] { "national day" };
                default: return new string[0];
            }
        }

        // ---------- 内部实现 ----------

        private string CacheFile(int year)
        {
            return Path.Combine(DataDir, "holidays-" + year + ".json");
        }

        /// <summary>依次尝试各数据源拉取某年数据，全部失败返回 null。</summary>
        private string FetchYear(int year)
        {
            for (int i = 0; i < SourceTemplates.Length; i++)
            {
                string url = string.Format(CultureInfo.InvariantCulture, SourceTemplates[i], year);
                try
                {
                    using (TimedWebClient wc = new TimedWebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        string json = wc.DownloadString(url);
                        // 简单校验确实是该年的数据，避免把错误页当数据合并。
                        if (json != null && json.IndexOf("\"year\": " + year, StringComparison.Ordinal) >= 0)
                            return json;
                        if (json != null && json.IndexOf("\"year\":" + year, StringComparison.Ordinal) >= 0)
                            return json;
                    }
                }
                catch
                {
                    // 换下一个源。
                }
            }
            return null;
        }

        /// <summary>解析并合并 JSON 文本（支持内置快照的 {year:[...]} 形态与数据源的 {year,days:[...]} 形态）。返回合并条数。</summary>
        public int MergeJson(string json)
        {
            lock (_lock)
            {
                return MergeJsonNoLock(json);
            }
        }

        private int MergeJsonNoLock(string json)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            object root = JsonLite.Parse(json);
            Dictionary<string, object> map = root as Dictionary<string, object>;
            if (map == null) return 0;

            int n = 0;
            List<object> days = JsonLite.GetArray(root, "days");
            if (days != null) n += MergeDays(days);

            foreach (KeyValuePair<string, object> kv in map)
            {
                if (kv.Key == "days" || kv.Key == "year") continue;
                List<object> arr = kv.Value as List<object>;
                if (arr != null) n += MergeDays(arr);
            }
            return n;
        }

        private int MergeDays(List<object> days)
        {
            int n = 0;
            for (int i = 0; i < days.Count; i++)
            {
                object o = days[i];
                string name = JsonLite.GetString(o, "name", "");
                string dateText = JsonLite.GetString(o, "date", "");
                DateTime d;
                if (name.Length == 0) continue;
                if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;

                HolidayInfo hi = new HolidayInfo();
                hi.Name = name;
                hi.Date = d.Date;
                hi.IsOffDay = JsonLite.GetBool(o, "isOffDay", true);
                _days[hi.Date] = hi;
                n++;
            }
            return n;
        }

        /// <summary>带超时的 WebClient（默认超时太长，联网失败时不能卡住后台线程太久）。</summary>
        private sealed class TimedWebClient : WebClient
        {
            public int TimeoutMs = 8000;

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest r = base.GetWebRequest(address);
                if (r != null)
                {
                    r.Timeout = TimeoutMs;
                    HttpWebRequest http = r as HttpWebRequest;
                    if (http != null) http.ReadWriteTimeout = TimeoutMs;
                }
                return r;
            }
        }
    }
}
