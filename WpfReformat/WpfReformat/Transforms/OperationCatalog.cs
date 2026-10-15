using System.Text;
using static WpfReformat.Transforms.TextTransforms;

namespace WpfReformat.Transforms
{
    /// <summary>
    /// Every operation shown in the Options dropdown, in display order.
    /// To add a new operation: write a pure function in <see cref="TextTransforms"/> and add one line here.
    /// </summary>
    public static class OperationCatalog
    {
        public const string DefaultOperation = "SQL Reformat";

        public const string JsonEvaluateName = "JSON Beautify/Evaluate";

        private const string Sql = "SQL";
        private const string Lines = "Line tools";
        private const string Json = "JSON / XML";
        private const string Hl7 = "HL7 / EDI / Mirth";
        private const string Code = "Code snippets";
        private const string Encode = "Encoding & encryption";

        public static IReadOnlyList<Operation> All { get; } = new List<Operation>
        {
            new("SQL Reformat",                    Sql, (s, _) => SqlReformat(s)),
            new("Add Commas",                      Sql, (s, _) => AddCommas(s, ",\n")),
            new("Add Commas - Flat",               Sql, (s, _) => AddCommas(s, ", ")),
            new("Add Commas - Flat - No Spaces",   Sql, (s, _) => AddCommas(s, ",")),
            new("Ticks",                           Sql, (s, _) => Ticks(s, ",\n")),
            new("Ticks - Flat",                    Sql, (s, _) => Ticks(s, ", ")),
            new("Ticks - Flat - No Spaces",        Sql, (s, _) => Ticks(s, ",")),
            new("SQL IN (...) - Distinct",         Sql, (s, _) => SqlInList(s)),
            new("Quotes",                          Sql, (s, _) => Quotes(s, ",\n")),
            new("Quotes - Flat",                   Sql, (s, _) => Quotes(s, ", ")),
            new("QUOTENAME([column_name], '\"')",  Sql, (s, _) => QuoteName(s)),
            new("SQL NVARCHAR[255]",               Sql, (s, _) => SqlNvarchar255(s)),

            new("Trim Lines / Remove Blanks",      Lines, (s, _) => TrimLines(s)),
            new("Distinct Lines",                  Lines, (s, _) => DistinctLines(s)),
            new("Sort Lines",                      Lines, (s, _) => SortLines(s)),
            new("UPPERCASE",                       Lines, (s, _) => ToUpper(s)),
            new("lowercase",                       Lines, (s, _) => ToLower(s)),
            new("Comma Split",                     Lines, (s, _) => CommaSplit(s)),

            new(JsonEvaluateName,                  Json, (s, ctx) => JsonEvaluate(s, ctx.Mask), ExtraInput.JsonPath),
            new("JSON Shrink",                     Json, (s, _) => JsonShrink(s)),
            new("XML => JSON",                     Json, (s, _) => XmlToJson(s)),
            new("Json Split Variables",            Json, (s, _) => JsonSplitVariables(s)),
            new("Json Class Objects",              Json, (s, _) => JsonClassObjects(s)),

            new("837/835",                         Hl7, (s, _) => Edi837(s)),
            new("HL7 Breakdown",                   Hl7, (s, _) => Hl7Breakdown(s)),
            new("MSH Reformat",                    Hl7, (s, _) => MshReformat(s)),
            new("Replace Feeds",                   Hl7, (s, _) => ReplaceFeeds(s)),
            new("Mirth | separate",                Hl7, (s, _) => AddCommas(s, "|")),
            new("Mirth , separate",                Hl7, (s, _) => AddCommas(s, ",")),
            new("Mirth delimiter",                 Hl7, (s, _) => MirthDelimiter(s)),
            new("Mirth quoted delimiter",          Hl7, (s, _) => MirthQuotedDelimiter(s)),
            new("GUID",                            Hl7, (s, _) => TextTransforms.Guid(s)),

            new("{ get; set; }",                   Code, (s, _) => GetSetWithBackingField(s)),
            new("{ get; set; } => Properties only", Code, (s, _) => AutoProperties(s)),
            new("Class list",                      Code, (s, _) => ClassList(s)),
            new("CopySQLtoC#",                     Code, (s, _) => CopySqlToCSharp(s)),
            new("CopySQLtoJavaScript",             Code, (s, _) => CopySqlToJavaScript(s)),

            new("Base64 Encode (UTF-16)",          Encode, (s, _) => Base64Encode(s, Encoding.Unicode)),
            new("Base64 Decode (UTF-16)",          Encode, (s, _) => Base64Decode(s, Encoding.Unicode)),
            new("Base64 Encode (UTF-8)",           Encode, (s, _) => Base64Encode(s, Encoding.UTF8)),
            new("Base64 Decode (UTF-8)",           Encode, (s, _) => Base64Decode(s, Encoding.UTF8)),
            new("URL Encode",                      Encode, (s, _) => UrlEncode(s)),
            new("URL Decode",                      Encode, (s, _) => UrlDecode(s)),
            new("SHA-256 Hash",                    Encode, (s, _) => Sha256Hex(s)),
            new("AES-256 Encrypt",                 Encode, (s, ctx) => AesText.EncryptLegacy(s, ctx.Password), ExtraInput.Password),
            new("AES-256 Encrypt (Strong)",        Encode, (s, ctx) => AesText.EncryptStrong(s, ctx.Password), ExtraInput.StrongPassword),
            new("AES-256 Decrypt",                 Encode, (s, ctx) => AesText.Decrypt(s, ctx.Password), ExtraInput.StrongPassword),
        };

        public static Operation? Find(string? name) => All.FirstOrDefault(o => o.Name == name);
    }
}
