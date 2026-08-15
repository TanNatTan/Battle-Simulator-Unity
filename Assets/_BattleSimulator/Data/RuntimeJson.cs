using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BattleSimulator.Data
{
    /// <summary>Small allocation-conscious JSON reader used to keep Unity free of a runtime JSON package dependency.</summary>
    public static class RuntimeJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using (var parser = new Parser(json)) return parser.ParseValue();
        }

        private sealed class Parser : IDisposable
        {
            private readonly StringReader reader;
            public Parser(string json) { reader = new StringReader(json); }
            public void Dispose() { reader.Dispose(); }

            public object ParseValue()
            {
                SkipWhite();
                int token = reader.Peek();
                switch (token)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Consume("true"); return true;
                    case 'f': Consume("false"); return false;
                    case 'n': Consume("null"); return null;
                    default: return token == '-' || token >= '0' && token <= '9' ? ParseNumber() : null;
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                reader.Read();
                while (true)
                {
                    SkipWhite();
                    if (reader.Peek() == '}') { reader.Read(); return result; }
                    string key = ParseString();
                    SkipWhite();
                    if (reader.Read() != ':') throw new FormatException("Expected ':' in JSON object.");
                    result[key] = ParseValue();
                    SkipWhite();
                    int separator = reader.Read();
                    if (separator == '}') return result;
                    if (separator != ',') throw new FormatException("Expected ',' in JSON object.");
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                reader.Read();
                while (true)
                {
                    SkipWhite();
                    if (reader.Peek() == ']') { reader.Read(); return result; }
                    result.Add(ParseValue());
                    SkipWhite();
                    int separator = reader.Read();
                    if (separator == ']') return result;
                    if (separator != ',') throw new FormatException("Expected ',' in JSON array.");
                }
            }

            private string ParseString()
            {
                if (reader.Read() != '"') throw new FormatException("Expected JSON string.");
                var result = new StringBuilder();
                while (true)
                {
                    int value = reader.Read();
                    if (value < 0) throw new EndOfStreamException("Unterminated JSON string.");
                    if (value == '"') return result.ToString();
                    if (value != '\\') { result.Append((char)value); continue; }
                    int escaped = reader.Read();
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            var hex = new char[4];
                            for (int i = 0; i < 4; i++) hex[i] = (char)reader.Read();
                            result.Append((char)Convert.ToInt32(new string(hex), 16));
                            break;
                        default: throw new FormatException("Invalid JSON escape sequence.");
                    }
                }
            }

            private double ParseNumber()
            {
                var result = new StringBuilder();
                while (true)
                {
                    int value = reader.Peek();
                    if (value < 0 || !"-+0123456789.eE".Contains(((char)value).ToString())) break;
                    result.Append((char)reader.Read());
                }
                return double.Parse(result.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private void Consume(string expected)
            {
                for (int i = 0; i < expected.Length; i++) if (reader.Read() != expected[i]) throw new FormatException("Invalid JSON token.");
            }

            private void SkipWhite()
            {
                while (reader.Peek() >= 0 && char.IsWhiteSpace((char)reader.Peek())) reader.Read();
            }
        }
    }

    public static class JsonData
    {
        public static Dictionary<string, object> Object(object value) => value as Dictionary<string, object> ?? EmptyObject;
        public static List<object> Array(object value) => value as List<object> ?? EmptyArray;
        public static Dictionary<string, object> Child(this Dictionary<string, object> value, string key) => value != null && value.TryGetValue(key, out object child) ? Object(child) : EmptyObject;
        public static List<object> Children(this Dictionary<string, object> value, string key) => value != null && value.TryGetValue(key, out object child) ? Array(child) : EmptyArray;
        public static string String(this Dictionary<string, object> value, string key, string fallback = "") => value != null && value.TryGetValue(key, out object child) && child != null ? Convert.ToString(child, CultureInfo.InvariantCulture) : fallback;
        public static float Float(this Dictionary<string, object> value, string key, float fallback = 0f) => value != null && value.TryGetValue(key, out object child) && child != null ? Convert.ToSingle(child, CultureInfo.InvariantCulture) : fallback;
        public static int Int(this Dictionary<string, object> value, string key, int fallback = 0) => value != null && value.TryGetValue(key, out object child) && child != null ? Convert.ToInt32(child, CultureInfo.InvariantCulture) : fallback;
        public static bool Bool(this Dictionary<string, object> value, string key, bool fallback = false) => value != null && value.TryGetValue(key, out object child) && child != null ? Convert.ToBoolean(child, CultureInfo.InvariantCulture) : fallback;
        public static readonly Dictionary<string, object> EmptyObject = new Dictionary<string, object>();
        public static readonly List<object> EmptyArray = new List<object>();
    }
}
