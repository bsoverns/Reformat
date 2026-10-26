using WpfReformat.Transforms;
using Xunit;

namespace WpfReformat.Tests
{
    public class TransformTests
    {
        private static string Run(string operation, string input, string password = "", string mask = "") =>
            OperationCatalog.Find(operation)!.Run(input, new OperationContext(password, mask));

        [Fact]
        public void Catalog_names_are_unique_and_default_exists()
        {
            var names = OperationCatalog.All.Select(o => o.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.NotNull(OperationCatalog.Find(OperationCatalog.DefaultOperation));
            Assert.NotNull(OperationCatalog.Find(OperationCatalog.JsonEvaluateName));
        }

        [Theory]
        [InlineData("Add Commas", "a\r\nb\r\nc", "a,\nb,\nc")]
        [InlineData("Add Commas - Flat", "a\nb\nc", "a, b, c")]
        [InlineData("Add Commas - Flat - No Spaces", "a\nb\nc", "a,b,c")]
        [InlineData("Ticks", "a\nb\n", "'a',\n'b'")]
        [InlineData("Ticks - Flat", "a\nb", "'a', 'b'")]
        [InlineData("Ticks - Flat - No Spaces", "a\nb", "'a','b'")]
        [InlineData("Quotes - Flat", "a\nb", "\"a\", \"b\"")]
        [InlineData("Mirth | separate", "a\nb", "a|b")]
        [InlineData("Comma Split", "a,b", "a,\nb")]
        [InlineData("837/835", "ISA*00~GS*HC~", "ISA*00~\nGS*HC~\n")]
        public void Simple_list_operations(string operation, string input, string expected) =>
            Assert.Equal(expected, Run(operation, input));

        [Fact]
        public void Ticks_escape_apostrophes() =>
            Assert.Equal("'O''Brien', 'Smith'", Run("Ticks - Flat", "O'Brien\nSmith"));

        [Fact]
        public void SqlReformat_flattens_columns() =>
            Assert.Equal("a, b, c", Run("SQL Reformat", "a,\r\n  b,\r\n  c\r\n"));

        [Fact]
        public void SqlInList_trims_dedupes_and_escapes() =>
            Assert.Equal("IN ('a', 'O''Brien')", Run("SQL IN (...) - Distinct", " a \n\nO'Brien\na\n"));

        [Fact]
        public void SqlNvarchar_includes_type() =>
            Assert.Equal("[Id] [nvarchar](255) NULL,\n\t[Name] [nvarchar](255) NULL,\n\t", Run("SQL NVARCHAR[255]", "Id\nName"));

        [Fact]
        public void QuoteName_wraps_each_column() =>
            Assert.Equal("QUOTENAME([Id], '\"'),\nQUOTENAME([Name], '\"')", Run("QUOTENAME([column_name], '\"')", "Id\n[Name]\n"));

        [Fact]
        public void Line_tools()
        {
            Assert.Equal("b\na", Run("Trim Lines / Remove Blanks", "  b \n\n a\n"));
            Assert.Equal("b\na", Run("Distinct Lines", "b\na\nb"));
            Assert.Equal("a\nB\nc", Run("Sort Lines", "c\nB\na"));
        }

        [Theory]
        [InlineData("dan peterson", "Dan Peterson")]
        [InlineData("brad o'doyle", "Brad O'Doyle")]
        [InlineData("BRAD O'DOYLE\nmary-kate smith", "Brad O'Doyle\nMary-Kate Smith")]
        [InlineData("don't stop", "Don't Stop")]
        public void ProperCase_capitalizes_names(string input, string expected) =>
            Assert.Equal(expected, Run("Proper Case", input));

        [Fact]
        public void JsonShrink_keeps_whitespace_inside_strings() =>
            Assert.Equal("{\"a\":\"x y\",\"b\":[1,2]}", Run("JSON Shrink", "{\n  \"a\": \"x y\",\n  \"b\": [ 1, 2 ]\n}"));

        [Fact]
        public void JsonEvaluate_applies_mask()
        {
            const string json = "{\"orders\":[{\"id\":1},{\"id\":2}]}";
            Assert.Equal("1\r\n2\r\n", Run(OperationCatalog.JsonEvaluateName, json, mask: "$.orders[*].id"));
            Assert.Equal("[No matches]", Run(OperationCatalog.JsonEvaluateName, json, mask: "$.nope"));
        }

        [Fact]
        public void Hl7Breakdown_numbers_msh_per_spec()
        {
            var output = Run("HL7 Breakdown", "MSH|^~\\&|App|Fac\rPID|1||123^^^MRN");
            Assert.Contains("MSH.2.1: ^~\\&", output);
            Assert.Contains("MSH.3.1: App", output);
            Assert.Contains("PID.1.1: 1", output);
            Assert.Contains("PID.3.1: 123", output);
            Assert.Contains("PID.3.4: MRN", output);
        }

        [Fact]
        public void MshReformat_and_ReplaceFeeds_round_trip() =>
            Assert.Equal("\r\nMSH|a\r\nMSH|b", Run("Replace Feeds", Run("MSH Reformat", "MSH|a\nMSH|b")));

        [Theory]
        [InlineData("UTF-16")]
        [InlineData("UTF-8")]
        public void Base64_round_trips(string encoding)
        {
            var encoded = Run($"Base64 Encode ({encoding})", "héllo");
            Assert.Equal("héllo", Run($"Base64 Decode ({encoding})", encoded));
        }

        [Fact]
        public void Base64_decode_reports_bad_input() =>
            Assert.Throws<InvalidOperationException>(() => Run("Base64 Decode (UTF-8)", "not base64!"));

        // Smallest valid PNG: 1x1 transparent pixel.
        private static readonly byte[] TinyPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private static string RunImage(string operation, byte[] image) =>
            OperationCatalog.Find(operation)!.Run(string.Empty, new OperationContext(Image: image));

        [Fact]
        public void Base64_image_encode_raw_and_data_uri()
        {
            var raw = RunImage(OperationCatalog.ImageEncodeName, TinyPng);
            Assert.Equal(Convert.ToBase64String(TinyPng), raw);
            Assert.Equal("data:image/png;base64," + raw, RunImage(OperationCatalog.ImageEncodeDataUriName, TinyPng));
        }

        [Fact]
        public void Base64_image_encode_reads_a_file_path()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
            File.WriteAllBytes(path, TinyPng);
            try
            {
                Assert.Equal(Convert.ToBase64String(TinyPng), Run(OperationCatalog.ImageEncodeName, $"\"{path}\""));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Base64_image_encode_without_an_image_explains_what_to_do() =>
            Assert.Equal(ImageBase64.NoImageMessage,
                Assert.Throws<InvalidOperationException>(() => Run(OperationCatalog.ImageEncodeName, "hello")).Message);

        [Theory]
        [InlineData("")]                          // raw
        [InlineData("data:image/png;base64,")]    // data URI
        public void Base64_image_decode_round_trips(string prefix)
        {
            var raw = Convert.ToBase64String(TinyPng);
            var wrapped = prefix + raw[..20] + "\r\n" + raw[20..].TrimEnd('=');   // wrapped lines, no padding
            var (bytes, mime) = ImageBase64.Decode(wrapped);
            Assert.Equal(TinyPng, bytes);
            Assert.Equal("image/png", mime);
            Assert.Equal("PNG image, 70 bytes", Run(OperationCatalog.ImageDecodeName, wrapped));
        }

        [Fact]
        public void Base64_image_adds_and_strips_the_data_uri_prefix()
        {
            var raw = Convert.ToBase64String(TinyPng);
            var dataUri = "data:image/png;base64," + raw;
            Assert.Equal(dataUri, Run("Base64 Image → data URI", raw));
            Assert.Equal(dataUri, Run("Base64 Image → data URI", dataUri));   // already prefixed: unchanged
            Assert.Equal(raw, Run("data URI → Base64 Image", dataUri));
            Assert.Equal(raw, Run("data URI → Base64 Image", raw));           // already raw: unchanged
        }

        [Theory]
        [InlineData("not base64!", "unexpected '!' at position 11")]   // spaces/line breaks are allowed
        [InlineData("iVBORw0KG", "looks cut off")]
        [InlineData("iVBO=Rw0K", "padding can only appear at the end")]
        [InlineData("aGVsbG8gd29ybGQ=", "isn't a recognized image")]   // valid Base64, but "hello world" isn't an image
        [InlineData("Qk1XIGNhcnMgYXJlIGZhc3Q=", "isn't a recognized image")]   // "BMW cars are fast" isn't a BMP
        [InlineData("   ", "Paste Base64 image text")]
        public void Base64_image_decode_explains_bad_input(string input, string expectedMessage) =>
            Assert.Contains(expectedMessage, Assert.Throws<InvalidOperationException>(() => ImageBase64.Decode(input)).Message);

        [Fact]
        public void Base64_image_encode_rejects_a_non_image_file()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
            File.WriteAllText(path, "just text");
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => Run(OperationCatalog.ImageEncodeName, path));
                Assert.Contains("isn't a recognized image", ex.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Text_base64_decode_points_images_to_image_decode()
        {
            var raw = Convert.ToBase64String(TinyPng);
            Assert.Contains("Base64 Image Decode", Assert.Throws<InvalidOperationException>(() => Run("Base64 Decode (UTF-8)", raw)).Message);
            Assert.Contains("data: URI", Assert.Throws<InvalidOperationException>(() => Run("Base64 Decode (UTF-8)", "data:image/png;base64," + raw)).Message);
        }

        [Theory]
        [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg", ".jpg")]
        [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, "image/gif", ".gif")]
        [InlineData(new byte[] { (byte)'B', (byte)'M', 0x46, 0, 0, 0, 0, 0, 0, 0, 0x36, 0, 0, 0 }, "image/bmp", ".bmp")]
        [InlineData(new byte[] { 0, 0, 1, 0, 1, 0 }, "image/x-icon", ".ico")]
        public void Detects_image_formats(byte[] header, string mime, string ext)
        {
            Assert.Equal(mime, ImageBase64.DetectMime(header));
            Assert.Equal(ext, ImageBase64.Extension(mime));
        }

        [Fact]
        public void Sha256_matches_known_vector() =>
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Run("SHA-256 Hash", "abc"));

        [Fact]
        public void Legacy_aes_matches_original_algorithm()
        {
            // Reference value produced independently with:
            // openssl enc -aes-256-cbc -K <"secret" + built-in key[6..]> -iv <built-in IV> -base64
            const string reference = "7Xn1fUKGT/usClCS6Zb8tmsC2Fmp7iLapUwV5M8avVE=";
            Assert.Equal(reference, Run("AES-256 Encrypt", "Hello, Reformat!", "secret"));
            Assert.Equal("Hello, Reformat!", Run("AES-256 Decrypt", reference, "secret"));
        }

        [Fact]
        public void Legacy_aes_rejects_long_password() =>
            Assert.Throws<InvalidOperationException>(() => Run("AES-256 Encrypt", "x", new string('p', 33)));

        [Fact]
        public void Strong_aes_round_trips_and_is_randomized()
        {
            var a = Run("AES-256 Encrypt (Strong)", "top secret", "a long passphrase that is over 32 characters");
            var b = Run("AES-256 Encrypt (Strong)", "top secret", "a long passphrase that is over 32 characters");
            Assert.StartsWith(AesText.StrongPrefix, a);
            Assert.NotEqual(a, b);
            Assert.Equal("top secret", Run("AES-256 Decrypt", a, "a long passphrase that is over 32 characters"));
        }

        [Fact]
        public void Strong_aes_wrong_password_fails_cleanly()
        {
            var ct = Run("AES-256 Encrypt (Strong)", "top secret", "right");
            var ex = Assert.Throws<InvalidOperationException>(() => Run("AES-256 Decrypt", ct, "wrong"));
            Assert.Contains("password", ex.Message);
        }

        [Fact]
        public void XmlToJson_converts() =>
            Assert.Contains("\"name\": \"x\"", Run("XML => JSON", "<root><name>x</name></root>"));

        [Fact]
        public void Code_snippets()
        {
            Assert.Equal("public string a { get; set; }\npublic string b { get; set; }\n", Run("{ get; set; } => Properties only", "a, b"));
            Assert.Equal("this._a = a;\n", Run("Class list", "a"));
            Assert.Equal("select * \\n\" + \n\"from t", Run("CopySQLtoC#", "select *\nfrom t"));
        }
    }
}
