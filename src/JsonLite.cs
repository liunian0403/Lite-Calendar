using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QingLi
{
    /// <summary>
    /// 极简 JSON 解析器（仅解析，无序列化需求）。
    /// 支持对象 / 数组 / 字符串（含 \uXXXX 转义）/ 数字 / true / false / null。
    /// 目的是不引入任何第三方依赖即可解析节假日数据源返回的 JSON。
    /// </summary>
    internal static class JsonLite
    {
        /// <summary>解析 JSON 文本。对象→Dictionary&lt;string,object&gt;，数组→List&lt;object&gt;，
        /// 数字→double，字符串→string，布尔→bool，null→null。格式非法时抛 FormatException。</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON 文本为 null");
            int pos = 0;
            object value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length) throw new FormatException("JSON 结尾存在多余内容（位置 " + pos + "）");
            return value;
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) throw new FormatException("JSON 意外结束");
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't': return ParseKeyword(s, ref pos, "true", true);
                case 'f': return ParseKeyword(s, ref pos, "false", false);
                case 'n': return ParseKeyword(s, ref pos, "null", null);
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);
                    throw new FormatException("JSON 非法字符 '" + c + "'（位置 " + pos + "）");
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
            pos++; // '{'
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return map; }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != '"') throw new FormatException("对象缺少键名（位置 " + pos + "）");
                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw new FormatException("键名后缺少冒号（位置 " + pos + "）");
                pos++;
                map[key] = ParseValue(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("对象未闭合");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return map; }
                throw new FormatException("对象中存在非法字符 '" + s[pos] + "'（位置 " + pos + "）");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            List<object> list = new List<object>();
            pos++; // '['
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("数组未闭合");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return list; }
                throw new FormatException("数组中存在非法字符 '" + s[pos] + "'（位置 " + pos + "）");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            pos++; // 开引号
            while (true)
            {
                if (pos >= s.Length) throw new FormatException("字符串未闭合");
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (pos >= s.Length) throw new FormatException("转义序列不完整");
                    char e = s[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > s.Length) throw new FormatException("\\u 转义不完整");
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw new FormatException("未知转义 \\" + e);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && s[pos] == '-') pos++;
            while (pos < s.Length && ((s[pos] >= '0' && s[pos] <= '9') || s[pos] == '.' ||
                                       s[pos] == 'e' || s[pos] == 'E' || s[pos] == '+' || s[pos] == '-'))
                pos++;
            double d;
            if (!double.TryParse(s.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("数字格式非法（位置 " + start + "）");
            return d;
        }

        private static object ParseKeyword(string s, ref int pos, string keyword, object value)
        {
            if (pos + keyword.Length > s.Length || s.Substring(pos, keyword.Length) != keyword)
                throw new FormatException("JSON 非法字面量（位置 " + pos + "）");
            pos += keyword.Length;
            return value;
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\r' || s[pos] == '\n'))
                pos++;
        }

        // ---------- 便捷取值辅助 ----------

        /// <summary>从对象中安全取字符串，缺失或类型不符返回 defaultValue。</summary>
        public static string GetString(object obj, string key, string defaultValue)
        {
            Dictionary<string, object> map = obj as Dictionary<string, object>;
            object v;
            if (map != null && map.TryGetValue(key, out v) && v is string) return (string)v;
            return defaultValue;
        }

        /// <summary>从对象中安全取布尔，缺失或类型不符返回 defaultValue。</summary>
        public static bool GetBool(object obj, string key, bool defaultValue)
        {
            Dictionary<string, object> map = obj as Dictionary<string, object>;
            object v;
            if (map != null && map.TryGetValue(key, out v) && v is bool) return (bool)v;
            return defaultValue;
        }

        /// <summary>从对象中安全取数组，缺失或类型不符返回 null。</summary>
        public static List<object> GetArray(object obj, string key)
        {
            Dictionary<string, object> map = obj as Dictionary<string, object>;
            object v;
            if (map != null && map.TryGetValue(key, out v)) return v as List<object>;
            return null;
        }
    }
}
