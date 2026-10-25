using System;
using System.Collections.Generic;
using System.IO;
using QingLi;

/// <summary>核心逻辑回归测试：持久化、重复展开、排除实例、ICS 往返。</summary>
class TestCore
{
    static int failed = 0;
    static int passed = 0;

    static void Check(bool cond, string name)
    {
        if (cond) { passed++; Console.WriteLine("  PASS  " + name); }
        else { failed++; Console.WriteLine("  FAIL  " + name); }
    }

    static EventItem Ev(string title, DateTime start, DateTime end, RecurrenceType rec, DateTime until)
    {
        EventItem e = new EventItem();
        e.Id = Guid.NewGuid().ToString("N");
        e.Title = title;
        e.Start = start;
        e.End = end;
        e.Recurrence = rec;
        e.RecurrenceUntil = until;
        return e;
    }

    static int Count(EventStore s, DateTime from, DateTime to)
    {
        return s.ExpandRange(from, to).Count;
    }

    static int CountTitle(EventStore s, string title, DateTime from, DateTime to)
    {
        List<Occurrence> list = s.ExpandRange(from, to);
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Title == title) n++;
        return n;
    }

    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qingli-test-" + Guid.NewGuid().ToString("N"));
        Console.WriteLine("== 1. 持久化（新增 / 保存 / 重启读取）==");
        EventStore a = new EventStore(dir);
        a.Load();
        EventItem e1 = Ev("会议", new DateTime(2026, 10, 25, 9, 0, 0), new DateTime(2026, 10, 25, 10, 0, 0), RecurrenceType.None, DateTime.MinValue);
        e1.Location = "3A";
        e1.Notes = "备注";
        e1.ReminderMinutes = 5;
        a.Add(e1);
        EventItem e2 = Ev("每日站会", new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 9, 15, 0), RecurrenceType.Daily, new DateTime(2026, 10, 10));
        a.Add(e2);
        Check(File.Exists(Path.Combine(dir, "data.xml")), "数据文件已生成");

        EventStore b = new EventStore(dir);
        b.Load();
        Check(b.Data.Events.Count == 2, "重启后条数 = 2 (实际 " + b.Data.Events.Count + ")");
        EventItem r = b.Find(e1.Id);
        Check(r != null && r.Title == "会议" && r.Location == "3A" && r.ReminderMinutes == 5, "字段完整往返");

        // 编辑
        r.Notes = "改过的备注";
        b.Update(r);
        EventStore c = new EventStore(dir);
        c.Load();
        Check(c.Find(e1.Id).Notes == "改过的备注", "编辑后重新读取生效");

        Console.WriteLine("== 2. 每天重复 ==");
        EventStore d = new EventStore(dir);
        d.Load();
        EventItem daily = Ev("D", new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 8, 30, 0), RecurrenceType.Daily, new DateTime(2026, 10, 10));
        d.Add(daily);
        Check(CountTitle(d, "D", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)) == 10, "10/1~10/10 每天 = 10 次");
        Check(CountTitle(d, "D", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7)) == 2, "区间裁剪 10/5~10/6 = 2 次");

        Console.WriteLine("== 3. 每周重复 ==");
        EventItem weekly = Ev("W", new DateTime(2026, 10, 5, 14, 0, 0), new DateTime(2026, 10, 5, 15, 0, 0), RecurrenceType.Weekly, new DateTime(2026, 10, 26));
        d.Add(weekly);
        Check(CountTitle(d, "W", new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)) == 4, "每周 = 4 次 (10/5,12,19,26)");

        Console.WriteLine("== 4. 每月重复（月末收敛）==");
        EventItem monthly = Ev("M", new DateTime(2026, 1, 31, 10, 0, 0), new DateTime(2026, 1, 31, 11, 0, 0), RecurrenceType.Monthly, DateTime.MinValue);
        d.Add(monthly);
        List<Occurrence> jan = d.ExpandRange(new DateTime(2026, 1, 1), new DateTime(2026, 4, 1));
        int mCount = 0; bool feb28 = false, mar31 = false;
        for (int i = 0; i < jan.Count; i++)
        {
            if (jan[i].Title != "M") continue;
            mCount++;
            if (jan[i].Start == new DateTime(2026, 2, 28, 10, 0, 0)) feb28 = true;
            if (jan[i].Start == new DateTime(2026, 3, 31, 10, 0, 0)) mar31 = true;
        }
        Check(mCount == 3, "1~3月 = 3 次 (实际 " + mCount + ")");
        Check(feb28, "2 月收敛到 28 日（不漂移）");
        Check(mar31, "3 月回到 31 日");

        Console.WriteLine("== 5. 工作日重复 ==");
        EventStore w = new EventStore(dir);
        w.Load();
        EventItem wd = Ev("WD", new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 9, 30, 0), RecurrenceType.Weekdays, new DateTime(2026, 10, 10));
        w.Add(wd);
        Check(CountTitle(w, "WD", new DateTime(2026, 10, 1), new DateTime(2026, 10, 11)) == 7, "10/1(四)~10/10(六) 工作日 = 7 次（10/10 是周六，正确跳过）");
        List<Occurrence> wdo = w.ExpandRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 11));
        bool weekendFree = true;
        for (int i = 0; i < wdo.Count; i++)
        {
            if (wdo[i].Title != "WD") continue;
            if (wdo[i].Start.DayOfWeek == DayOfWeek.Saturday || wdo[i].Start.DayOfWeek == DayOfWeek.Sunday) weekendFree = false;
        }
        Check(weekendFree, "工作日展开不含周六周日");

        Console.WriteLine("== 6. 单次删除重复实例（ExcludedStarts）==");
        EventStore x = new EventStore(dir);
        x.Load();
        EventItem dd = Ev("EX", new DateTime(2026, 11, 1, 9, 0, 0), new DateTime(2026, 11, 1, 10, 0, 0), RecurrenceType.Daily, new DateTime(2026, 11, 10));
        x.Add(dd);
        int before = Count(x, new DateTime(2026, 11, 1), new DateTime(2026, 11, 11));
        Occurrence target = null;
        List<Occurrence> list = x.ExpandRange(new DateTime(2026, 11, 1), new DateTime(2026, 11, 11));
        for (int i = 0; i < list.Count; i++)
            if (list[i].Start == new DateTime(2026, 11, 3, 9, 0, 0)) target = list[i];
        Check(target != null, "找到 11/3 的实例");
        x.RemoveOccurrence(target);
        int after = Count(x, new DateTime(2026, 11, 1), new DateTime(2026, 11, 11));
        Check(after == before - 1, "删除单次后次数 -1 (" + before + " -> " + after + ")");
        EventStore y = new EventStore(dir);
        y.Load();
        Check(Count(y, new DateTime(2026, 11, 1), new DateTime(2026, 11, 11)) == before - 1, "重启后仍然排除该次");

        Console.WriteLine("== 7. ICS 导出 / 导入往返 ==");
        string ics = Path.Combine(dir, "roundtrip.ics");
        List<EventItem> export = new List<EventItem>();
        EventItem s1 = Ev("导出A", new DateTime(2026, 12, 1, 9, 0, 0), new DateTime(2026, 12, 1, 10, 30, 0), RecurrenceType.None, DateTime.MinValue);
        s1.Location = "北京;总部";
        s1.Notes = "第一行\n第二行";
        EventItem s2 = Ev("导出B", new DateTime(2026, 12, 2, 8, 0, 0), new DateTime(2026, 12, 2, 8, 30, 0), RecurrenceType.Weekdays, new DateTime(2026, 12, 20));
        export.Add(s1);
        export.Add(s2);
        y.ExportIcs(export, ics);
        Check(File.Exists(ics), "ICS 文件已写出");
        List<EventItem> parsed = Ics.Parse(ics);
        Check(parsed.Count == 2, "解析出 2 个事件 (实际 " + parsed.Count + ")");
        EventItem p1 = null, p2 = null;
        for (int i = 0; i < parsed.Count; i++)
        {
            if (parsed[i].Title == "导出A") p1 = parsed[i];
            if (parsed[i].Title == "导出B") p2 = parsed[i];
        }
        Check(p1 != null && p1.Start == s1.Start && p1.End == s1.End, "事件A 时间往返一致");
        Check(p1 != null && p1.Location == "北京;总部", "地点转义往返");
        Check(p1 != null && p1.Notes == "第一行\n第二行", "备注换行往返");
        Check(p2 != null && p2.Recurrence == RecurrenceType.Weekdays, "工作日规则往返");
        Check(p2 != null && p2.RecurrenceUntil == new DateTime(2026, 12, 20), "重复截止日往返");

        Console.WriteLine();
        Console.WriteLine("结果: " + passed + " 通过, " + failed + " 失败");
        Environment.Exit(failed == 0 ? 0 : 1);
    }
}
