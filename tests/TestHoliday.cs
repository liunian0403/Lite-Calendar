using System;
using System.Collections.Generic;
using System.IO;
using QingLi;

/// <summary>节假日与搜索关键词回归测试：JSON 解析、数据合并、缓存回退、calendar/calander 关键词、英文别名搜索。</summary>
class TestHoliday
{
    static int failed = 0;
    static int passed = 0;

    static void Check(bool cond, string name)
    {
        if (cond) { passed++; Console.WriteLine("  PASS  " + name); }
        else { failed++; Console.WriteLine("  FAIL  " + name); }
    }

    static int Main()
    {
        Console.WriteLine("== 1. JsonLite 基础解析 ==");
        object obj = JsonLite.Parse("{\"a\": 1, \"b\": \"文本\", \"c\": true, \"d\": null, \"e\": [1, 2.5], \"f\": {\"g\": \"\\u4e2d\"}}");
        Dictionary<string, object> map = obj as Dictionary<string, object>;
        Check(map != null, "对象解析为 Dictionary");
        Check(JsonLite.GetString(obj, "a", "") == "" , "非字符串字段取 string 返回默认值");
        Check(JsonLite.GetString(obj, "b", "") == "文本", "字符串字段解析");
        Check(JsonLite.GetBool(obj, "c", false) == true, "布尔字段解析");
        Check(JsonLite.GetArray(obj, "e") != null && JsonLite.GetArray(obj, "e").Count == 2, "数组字段解析");
        Check(JsonLite.GetString(map["f"], "g", "") == "中", "\\uXXXX 转义解析");
        Check(JsonLite.GetArray(obj, "missing") == null, "缺失数组返回 null");

        bool threw = false;
        try { JsonLite.Parse("{bad json}"); } catch (FormatException) { threw = true; }
        Check(threw, "非法 JSON 抛 FormatException");

        Console.WriteLine("== 2. HolidayService 数据合并（数据源 {year,days} 形态）==");
        string dir = Path.Combine(Path.GetTempPath(), "qingli-holiday-test-" + Guid.NewGuid().ToString("N"));
        HolidayService svc = new HolidayService(dir);
        string srcJson = "{\"year\": 2030, \"days\": [" +
            "{\"name\": \"元旦\", \"date\": \"2030-01-01\", \"isOffDay\": true}," +
            "{\"name\": \"元旦\", \"date\": \"2030-01-06\", \"isOffDay\": false}," +
            "{\"name\": \"春节\", \"date\": \"2030-02-17\", \"isOffDay\": true}]}";
        int merged = svc.MergeJson(srcJson);
        Check(merged == 3, "合并 3 条 (实际 " + merged + ")");
        HolidayInfo d1 = svc.GetDay(new DateTime(2030, 1, 1));
        Check(d1 != null && d1.Name == "元旦" && d1.IsOffDay, "放假日期查询正确（isOffDay=true）");
        HolidayInfo d2 = svc.GetDay(new DateTime(2030, 1, 6));
        Check(d2 != null && d2.Name == "元旦" && !d2.IsOffDay, "调休上班查询正确（isOffDay=false）");
        Check(svc.GetDay(new DateTime(2030, 3, 1)) == null, "非节假日返回 null");
        // 同日重复数据以最后一条为准
        svc.MergeJson("{\"year\": 2030, \"days\": [{\"name\": \"调休测试\", \"date\": \"2030-01-01\", \"isOffDay\": false}]}");
        Check(svc.GetDay(new DateTime(2030, 1, 1)).Name == "调休测试", "同日期重复合并取后者");

        Console.WriteLine("== 3. 内置快照解析（{year: [...]} 形态）==");
        HolidayService svc2 = new HolidayService(dir);
        svc2.MergeJson("{\"2029\": [{\"name\": \"国庆节\", \"date\": \"2029-10-01\", \"isOffDay\": true}]}");
        Check(svc2.GetDay(new DateTime(2029, 10, 1)) != null, "内置快照形态解析");

        Console.WriteLine("== 4. 本地缓存回退 ==");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "holidays-2031.json"),
            "{\"year\": 2031, \"days\": [{\"name\": \"端午节\", \"date\": \"2031-06-19\", \"isOffDay\": true}]}",
            new System.Text.UTF8Encoding(false));
        HolidayService svc3 = new HolidayService(dir);
        svc3.Load();
        Check(svc3.GetDay(new DateTime(2031, 6, 19)) != null, "Load 读取本地缓存文件");
        Check(svc3.StatusText == "本地缓存", "状态标记为本地缓存 (实际 " + svc3.StatusText + ")");
        // 损坏缓存不影响内置数据
        string dir2 = Path.Combine(Path.GetTempPath(), "qingli-holiday-test2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir2);
        File.WriteAllText(Path.Combine(dir2, "holidays-2031.json"), "{corrupt");
        HolidayService svc4 = new HolidayService(dir2);
        svc4.Load();
        Check(svc4.StatusText == "内置数据", "损坏缓存回退到内置数据");

        Console.WriteLine("== 5. calendar / calander 关键词 ==");
        Check(HolidayService.IsCalendarKeyword("calendar"), "calendar 命中");
        Check(HolidayService.IsCalendarKeyword("calander"), "calander 命中");
        Check(HolidayService.IsCalendarKeyword("Calendar"), "Calendar 大小写不敏感");
        Check(HolidayService.IsCalendarKeyword(" calander "), "前后空白容错");
        Check(HolidayService.IsCalendarKeyword("CAL"), "缩写 cal 命中");
        Check(!HolidayService.IsCalendarKeyword("calendarr"), "多余字符不命中");
        Check(!HolidayService.IsCalendarKeyword("日历"), "中文不误命中该关键词");

        Console.WriteLine("== 6. 节假日搜索（中文 + 英文别名）==");
        HolidayService svc5 = new HolidayService(dir);
        svc5.MergeJson("{\"year\": 2026, \"days\": [" +
            "{\"name\": \"国庆节\", \"date\": \"2026-10-01\", \"isOffDay\": true}," +
            "{\"name\": \"国庆节\", \"date\": \"2026-10-02\", \"isOffDay\": true}," +
            "{\"name\": \"国庆节\", \"date\": \"2026-10-10\", \"isOffDay\": false}," +
            "{\"name\": \"春节\", \"date\": \"2026-02-17\", \"isOffDay\": true}]}");
        DateTime from = new DateTime(2026, 1, 1);
        List<HolidayInfo> r1 = svc5.SearchHolidays("国庆", from, 10);
        Check(r1.Count == 1, "同名节日去重只返回 1 条 (实际 " + r1.Count + ")");
        Check(r1.Count == 1 && r1[0].Date == new DateTime(2026, 10, 1) && r1[0].IsOffDay, "优先返回最早的放假日");
        List<HolidayInfo> r2 = svc5.SearchHolidays("national day", from, 10);
        Check(r2.Count == 1 && r2[0].Name == "国庆节", "英文别名 national day 命中国庆节");
        List<HolidayInfo> r3 = svc5.SearchHolidays("spring festival", from, 10);
        Check(r3.Count == 1 && r3[0].Name == "春节", "英文别名 spring festival 命中春节");
        List<HolidayInfo> r4 = svc5.SearchHolidays("春节", from, 10);
        Check(r4.Count == 1 && r4[0].Date == new DateTime(2026, 2, 17), "中文名搜索命中");
        List<HolidayInfo> r5 = svc5.SearchHolidays("holiday", from, 10);
        Check(r5.Count == 2, "通配 holiday 列出全部 (实际 " + r5.Count + ")");
        List<HolidayInfo> r6 = svc5.SearchHolidays("不存在的节日", from, 10);
        Check(r6.Count == 0, "无命中返回空列表");
        // 只有调休记录的节日（无放假记录）也应能搜到
        svc5.MergeJson("{\"year\": 2027, \"days\": [{\"name\": \"中秋节\", \"date\": \"2027-09-25\", \"isOffDay\": false}]}");
        List<HolidayInfo> r7 = svc5.SearchHolidays("中秋", from, 10);
        Check(r7.Count == 1 && !r7[0].IsOffDay, "仅调休记录的节日可搜到");
        // 范围限制：from 之后两年
        List<HolidayInfo> r8 = svc5.SearchHolidays("春节", new DateTime(2028, 1, 1), 10);
        Check(r8.Count == 0, "超出两年范围不返回");

        Console.WriteLine();
        Console.WriteLine("结果: " + passed + " 通过, " + failed + " 失败");
        return failed == 0 ? 0 : 1;
    }
}
