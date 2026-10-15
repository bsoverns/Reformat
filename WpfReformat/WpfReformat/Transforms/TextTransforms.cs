using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WpfReformat.Transforms
{
    /// <summary>
    /// Pure text transformations. No UI code lives here so everything can be unit tested.
    /// </summary>
    public static class TextTransforms
    {
        /// <summary>Normalize CRLF / CR line endings to LF.</summary>
        public static string NL(string? s) => (s ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

        /// <summary>Split on newlines and commas, strip spaces, drop empties (used by the C# snippet generators).</summary>
        private static IEnumerable<string> NameTokens(string input) =>
            NL(input).Replace(" ", "")
                     .Split(new[] { "\n", "," }, StringSplitOptions.None)
                     .Where(t => !string.IsNullOrWhiteSpace(t));

        private static string WrapLines(string input, string open, string separator, string close, Func<string, string>? escape = null)
        {
            var lines = NL(input).Trim('\n').Split('\n');
            if (escape != null) lines = lines.Select(escape).ToArray();
            return open + string.Join(close + separator + open, lines) + close;
        }

        private static string SqlEscape(string value) => value.Replace("'", "''");

        // ---- SQL ----

        public static string SqlReformat(string input)
        {
            string text = NL(input).Trim();
            text = Regex.Replace(text, @"\s+on\b", " on", RegexOptions.IgnoreCase);
            text = text.Replace("\n", "");
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                            .Where(p => p.Length > 0);
            return string.Join(", ", parts);
        }

        public static string AddCommas(string input, string separator) => NL(input).Replace("\n", separator);

        /// <summary>Wrap each line in single quotes. Embedded apostrophes are doubled (O'Brien → 'O''Brien').</summary>
        public static string Ticks(string input, string separator) => WrapLines(input, "'", separator, "'", SqlEscape);

        public static string Quotes(string input, string separator) => WrapLines(input, "\"", separator, "\"");

        /// <summary>Ready-to-paste <c>IN (...)</c> clause. Lines are trimmed, blanks and duplicates removed.</summary>
        public static string SqlInList(string input)
        {
            var values = CleanLines(input).Distinct().Select(v => "'" + SqlEscape(v) + "'");
            return "IN (" + string.Join(", ", values) + ")";
        }

        public static string QuoteName(string input)
        {
            var cols = CleanLines(input).Select(c => $"QUOTENAME([{c.Trim('[', ']')}], '\"')");
            return string.Join(",\n", cols);
        }

        public static string SqlNvarchar255(string input)
        {
            var sb = new StringBuilder();
            foreach (var col in NameTokens(input))
                sb.Append('[').Append(col).Append("] [nvarchar](255) NULL,\n\t");
            return sb.ToString();
        }

        // ---- Line tools ----

        /// <summary>Lines trimmed of surrounding whitespace, with blank lines removed.</summary>
        public static IEnumerable<string> CleanLines(string input) =>
            NL(input).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

        public static string TrimLines(string input) => string.Join("\n", CleanLines(input));

        public static string DistinctLines(string input) => string.Join("\n", CleanLines(input).Distinct());

        public static string SortLines(string input) =>
            string.Join("\n", CleanLines(input).OrderBy(l => l, StringComparer.OrdinalIgnoreCase));

        public static string ToUpper(string input) => input.ToUpperInvariant();

        public static string ToLower(string input) => input.ToLowerInvariant();

        public static string CommaSplit(string input) => input.Replace(",", ",\n");

        // ---- C# / JS snippets ----

        public static string GetSetWithBackingField(string input)
        {
            var sb = new StringBuilder();
            foreach (var name in NameTokens(input))
            {
                sb.Append("public string ").Append(name).Append('\n')
                  .Append("{\n")
                  .Append("    get { return _").Append(name).Append("; }\n")
                  .Append("    set { this._").Append(name).Append(" = value; }\n")
                  .Append("}\n\n");
            }
            return sb.ToString();
        }

        public static string AutoProperties(string input)
        {
            var sb = new StringBuilder();
            foreach (var name in NameTokens(input))
                sb.Append("public string ").Append(name).Append(" { get; set; }\n");
            return sb.ToString();
        }

        public static string ClassList(string input)
        {
            var sb = new StringBuilder();
            foreach (var name in NameTokens(input))
                sb.Append("this._").Append(name).Append(" = ").Append(name).Append(";\n");
            return sb.ToString();
        }

        public static string CopySqlToCSharp(string input) => NL(input).Replace("\n", " \\n\" + \n\"");

        public static string CopySqlToJavaScript(string input) => NL(input).Replace("\n", " \" \n+ \"\\r\\n");

        // ---- JSON / XML ----

        public static string JsonShrink(string input) =>
            Regex.Replace(input, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1");

        /// <summary>Pretty-print the whole document, or list every token matching a JSONPath mask.</summary>
        public static string JsonEvaluate(JToken root, string? mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                return root.ToString(Formatting.Indented);

            var sb = new StringBuilder();
            int count = 0;
            foreach (var t in root.SelectTokens(mask.Trim(), errorWhenNoMatch: false))
            {
                sb.AppendLine(t is JValue v ? v.ToString(CultureInfo.InvariantCulture) : t.ToString(Formatting.Indented));
                count++;
            }
            return count == 0 ? "[No matches]" : sb.ToString();
        }

        public static string JsonEvaluate(string input, string? mask) => JsonEvaluate(JToken.Parse(input), mask);

        public static string XmlToJson(string input) => JsonConvert.SerializeXNode(XDocument.Parse(input), Formatting.Indented);

        public static string JsonSplitVariables(string input)
        {
            var keys = NL(input).Replace(" ", "")
                                .Split(new[] { "\n", "," }, StringSplitOptions.None)
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .Select(l => l.Split(':')[0].Replace("\"", "").Trim());
            return string.Join("\n", keys);
        }

        public static string JsonClassObjects(string input)
        {
            var sb = new StringBuilder();
            foreach (var name in NameTokens(input))
            {
                sb.Append("[JsonProperty(\"").Append(name).Append("\")]\n")
                  .Append("public string ").Append(name).Append(" { get; set; }")
                  .Append("\r\n\r\n");
            }
            return sb.ToString();
        }

        // ---- HL7 / EDI / Mirth ----

        public static string Edi837(string input) => input.Replace("~", "~\n");

        public static string Guid(string input) => input.Replace("~", "\n");

        public static string MshReformat(string input) => input.Replace("MSH", "~~~MSH");

        public static string ReplaceFeeds(string input) =>
            input.Replace("\r\n", "").Replace("\r", "").Replace("\n", "").Replace("~~~MSH", "\r\nMSH");

        /// <summary>
        /// Expand every field/component into <c>SEG.field.component: value</c> lines.
        /// MSH is numbered per the HL7 spec: MSH.1 is the field separator, so the first
        /// value after "MSH|" (the encoding characters) is MSH.2 and is not split on '^'.
        /// </summary>
        public static string Hl7Breakdown(string input)
        {
            var sb = new StringBuilder();
            foreach (var line in NL(input).Split('\n'))
            {
                if (line.Length == 0) continue;
                var columns = line.Split('|');
                var segment = columns[0];
                bool isMsh = segment == "MSH";

                for (int i = 1; i < columns.Length; i++)
                {
                    int fieldNumber = isMsh ? i + 1 : i;
                    var components = isMsh && i == 1 ? new[] { columns[i] } : columns[i].Split('^');

                    for (int c = 0; c < components.Length; c++)
                    {
                        if (string.IsNullOrEmpty(components[c])) continue;
                        sb.Append("\r\n")
                          .Append(segment).Append('.')
                          .Append(fieldNumber).Append('.')
                          .Append(c + 1).Append(": ")
                          .Append(components[c]);
                    }
                }
            }
            return sb.ToString();
        }

        public static string MirthDelimiter(string input) =>
            NL(input).Replace("\n", "') + delimiter + results.getString('");

        public static string MirthQuotedDelimiter(string input) =>
            NL(input).Replace("\n", "') + quote + delimiter + quote + results.getString('");

        // ---- Encoding / hashing ----

        public static string Base64Encode(string input, Encoding encoding) => Convert.ToBase64String(encoding.GetBytes(input));

        public static string Base64Decode(string input, Encoding encoding)
        {
            try
            {
                return encoding.GetString(Convert.FromBase64String(input.Trim()));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Input is not valid Base64.");
            }
        }

        public static string UrlEncode(string input) => WebUtility.UrlEncode(input);

        public static string UrlDecode(string input) => WebUtility.UrlDecode(input);

        public static string Sha256Hex(string input) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
