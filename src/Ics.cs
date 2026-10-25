using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QingLi
{
    /// <summary>极简 ICS（RFC5545）读写：支持单次日程与 每天/每周/每月/工作日 重复。</summary>
    public static class Ics
    {
        public static string UtcStamp(DateTime utc)
        {
            return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') sb.Append("\\\\");
                else if (c == ';') sb.Append("\\;");
                else if (c == ',') sb.Append("\\,");
                else if (c == '\r') { }
                else if (c == '\n') sb.Append("\\n");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Unescape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n' || n == 'N') { sb.Append('\n'); i++; }
                    else if (n == '\\') { sb.Append('\\'); i++; }
                    else if (n == ';') { sb.Append(';'); i++; }
                    else if (n == ',') { sb.Append(','); i++; }
                    else sb.Append(s[i]);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public static string Fold(string line)
        {
            if (line == null) return "";
            if (line.Length <= 72) return line;
            StringBuilder sb = new StringBuilder();
            int pos = 0;
            bool first = true;
            while (pos < line.Length)
            {
                int take = first ? 72 : 71;
                if (pos + take >= line.Length)
                {
                    if (!first) sb.Append(" ");
                    sb.Append(line.Substring(pos));
                    break;
                }
                if (!first) sb.Append(" ");
                sb.Append(line.Substring(pos, take));
                sb.Append("\r\n");
                pos += take;
                first = false;
            }
            return sb.ToString();
        }

        public static string BuildRRule(EventItem e)
        {
            string freq = null;
            switch (e.Recurrence)
            {
                case RecurrenceType.Daily: freq = "DAILY"; break;
                case RecurrenceType.Weekly: freq = "WEEKLY"; break;
                case RecurrenceType.Monthly: freq = "MONTHLY"; break;
                case RecurrenceType.Weekdays: freq = "WEEKLY"; break;
            }
            if (freq == null) return null;
            StringBuilder sb = new StringBuilder("RRULE:FREQ=" + freq);
            if (e.Recurrence == RecurrenceType.Weekdays) sb.Append(";BYDAY=MO,TU,WE,TH,FR");
            else if (e.Recurrence == RecurrenceType.Weekly) sb.Append(";BYDAY=" + DayCode(e.Start.DayOfWeek));
            if (e.RecurrenceUntil != DateTime.MinValue)
            {
                if (e.AllDay) sb.Append(";UNTIL=" + e.RecurrenceUntil.Date.AddDays(1).ToString("yyyyMMdd"));
                else sb.Append(";UNTIL=" + UtcStamp(e.RecurrenceUntil.Date.AddDays(1).AddSeconds(-1).ToUniversalTime()));
            }
            return sb.ToString();
        }

        private static string DayCode(DayOfWeek d)
        {
            string[] c = new string[] { "SU", "MO", "TU", "WE", "TH", "FR", "SA" };
            return c[(int)d];
        }

        // ---------- 导入 ----------

        public static List<EventItem> Parse(string path)
        {
            string raw = System.IO.File.ReadAllText(path, Encoding.UTF8);
            return ParseText(raw);
        }

        public static List<EventItem> ParseText(string raw)
        {
            List<EventItem> result = new List<EventItem>();
            if (raw == null) return result;
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = raw.Split('\n');

            // 展开折行
            List<string> flat = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                if (ln.Length > 0 && (ln[0] == ' ' || ln[0] == '\t') && flat.Count > 0)
                    flat[flat.Count - 1] = flat[flat.Count - 1] + ln.Substring(1);
                else
                    flat.Add(ln);
            }

            EventItem cur = null;
            bool inAlarm = false;
            int alarmMinutes = -1;
            for (int i = 0; i < flat.Count; i++)
            {
                string line = flat[i];
                if (line.Length == 0) continue;
                if (line == "BEGIN:VEVENT")
                {
                    cur = new EventItem();
                    cur.Id = Guid.NewGuid().ToString("N");
                    cur.Recurrence = RecurrenceType.None;
                    inAlarm = false;
                    alarmMinutes = -1;
                    continue;
                }
                if (cur == null) continue;
                if (line == "END:VEVENT")
                {
                    if (alarmMinutes >= 0) cur.ReminderMinutes = alarmMinutes;
                    if (string.IsNullOrEmpty(cur.Title)) cur.Title = "（无标题）";
                    if (cur.End <= cur.Start)
                        cur.End = cur.AllDay ? cur.Start.Date : cur.Start.AddHours(1);
                    result.Add(cur);
                    cur = null;
                    continue;
                }
                if (line == "BEGIN:VALARM") { inAlarm = true; continue; }
                if (line == "END:VALARM") { inAlarm = false; continue; }

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string head = line.Substring(0, colon);
                string val = line.Substring(colon + 1);
                string name = head;
                int semi = head.IndexOf(';');
                if (semi > 0) name = head.Substring(0, semi);
                name = name.ToUpperInvariant();

                if (inAlarm)
                {
                    if (name == "TRIGGER")
                    {
                        Match m = Regex.Match(val.ToUpperInvariant(), @"-?P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?)?");
                        if (m.Success)
                        {
                            int d = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
                            int h = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                            int mi = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
                            int total = d * 1440 + h * 60 + mi;
                            if (!val.TrimStart().StartsWith("-")) total = 0;
                            alarmMinutes = total;
                        }
                    }
                    continue;
                }

                switch (name)
                {
                    case "SUMMARY": cur.Title = Unescape(val).Trim(); break;
                    case "LOCATION": cur.Location = Unescape(val).Trim(); break;
                    case "DESCRIPTION": cur.Notes = Unescape(val).Trim(); break;
                    case "DTSTART":
                        {
                            bool allday = head.ToUpperInvariant().Contains("VALUE=DATE") && !head.ToUpperInvariant().Contains("VALUE=DATE-TIME");
                            DateTime dt;
                            if (TryParseDate(val, out dt))
                            {
                                cur.AllDay = allday;
                                cur.Start = allday ? dt.Date : dt;
                                cur.End = allday ? dt.Date.AddDays(1).AddSeconds(-1) : dt.AddHours(1);
                            }
                        }
                        break;
                    case "DTEND":
                        {
                            DateTime dt;
                            if (TryParseDate(val, out dt))
                                cur.End = cur.AllDay ? dt.Date.AddSeconds(-1) : dt;
                        }
                        break;
                    case "DURATION":
                        {
                            Match m = Regex.Match(val.ToUpperInvariant(), @"P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?)?");
                            if (m.Success)
                            {
                                int d = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
                                int h = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                                int mi = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
                                cur.End = cur.Start.Add(new TimeSpan(d, h, mi, 0));
                            }
                        }
                        break;
                    case "RRULE":
                        ApplyRRule(cur, val);
                        break;
                }
            }
            return result;
        }

        private static void ApplyRRule(EventItem e, string val)
        {
            string freq = "";
            Dictionary<string, string> map = new Dictionary<string, string>();
            string[] parts = val.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = parts[i].Substring(0, eq).Trim().ToUpperInvariant();
                string v = parts[i].Substring(eq + 1).Trim();
                map[k] = v;
                if (k == "FREQ") freq = v.ToUpperInvariant();
            }

            if (freq == "DAILY") e.Recurrence = RecurrenceType.Daily;
            else if (freq == "WEEKLY")
            {
                string byday;
                if (map.TryGetValue("BYDAY", out byday))
                {
                    string[] days = byday.Split(',');
                    bool hasSat = false, hasSun = false;
                    for (int i = 0; i < days.Length; i++)
                    {
                        string d = days[i].Trim().ToUpperInvariant();
                        if (d.EndsWith("SA")) hasSat = true;
                        if (d.EndsWith("SU")) hasSun = true;
                    }
                    e.Recurrence = (days.Length >= 5 && !hasSat && !hasSun) ? RecurrenceType.Weekdays : RecurrenceType.Weekly;
                }
                else e.Recurrence = RecurrenceType.Weekly;
            }
            else if (freq == "MONTHLY") e.Recurrence = RecurrenceType.Monthly;
            else e.Recurrence = RecurrenceType.None;

            string until;
            if (map.TryGetValue("UNTIL", out until))
            {
                DateTime u;
                if (TryParseDate(until, out u)) e.RecurrenceUntil = u.Date;
            }
            else
            {
                string count;
                if (map.TryGetValue("COUNT", out count))
                {
                    int n;
                    if (int.TryParse(count, out n) && n > 1 && n < 500)
                    {
                        switch (e.Recurrence)
                        {
                            case RecurrenceType.Daily: e.RecurrenceUntil = e.Start.AddDays(n - 1).Date; break;
                            case RecurrenceType.Weekly: e.RecurrenceUntil = e.Start.AddDays((n - 1) * 7).Date; break;
                            case RecurrenceType.Monthly: e.RecurrenceUntil = e.Start.AddMonths(n - 1).Date; break;
                            case RecurrenceType.Weekdays: e.RecurrenceUntil = e.Start.AddDays(n * 2).Date; break;
                        }
                    }
                }
            }
        }

        private static bool TryParseDate(string val, out DateTime result)
        {
            result = DateTime.MinValue;
            string v = val.Trim();
            bool utc = v.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            if (utc) v = v.Substring(0, v.Length - 1);
            string[] formats = new string[]
            {
                "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm", "yyyyMMdd",
                "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd"
            };
            DateTime parsed;
            if (DateTime.TryParseExact(v, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                result = utc ? parsed.ToLocalTime() : parsed;
                return true;
            }
            if (DateTime.TryParse(v, out parsed))
            {
                result = utc ? parsed.ToLocalTime() : parsed;
                return true;
            }
            return false;
        }
    }
}
