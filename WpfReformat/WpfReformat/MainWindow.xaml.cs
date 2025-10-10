using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;

namespace WpfReformat
{
    public partial class MainWindow : Window
    {
        private const string Key = "alio1j304klsk49fnhcvlslwie8tgxzj"; // 32
        private const string IV = "fjdkwnsksdjklfsk";                 // 16

        private JToken? _jsonRoot = null;        // cached JSON for JSON Evaluate
        private DispatcherTimer? _debounceTimer; // debounce for live mask typing

        private readonly List<string> processList = new List<string>() { "SQL Reformat", "Add Commas", "Add Commas - Flat", "Add Commas - Flat - No Spaces", "Ticks", "Ticks - Flat", "Ticks - Flat - No Spaces", "JSON Beautify", "JSON Shrink", "JSON Evaluate", "Quotes", "Quotes - Flat", "QUOTENAME([column_name], '\"')", "SQL NVARCHAR[255]", "837/835", "Comma Split", "{ get; set; }", "{ get; set; } => Properties only", "Class list", "CopySQLtoC#", "CopySQLtoJavaScript", "Json Split Variables", "Json Class Objects", "Mirth | seperate", "Mirth , seperate", "Mirth deliminator", "Mirth quoted deliminator", "Base64 Encrypt", "Base64 Decrypt", "AES-256 Encrypt", "AES-256 Decrypt", "MSH Reformat", "Replace Feeds", "HL7 Breakdown", "XML => JSON" };
        private string defaultProcess = "SQL Reformat";

        public MainWindow()
        {
            InitializeComponent();
            SetDefaultsAsync();

            // Debounce (250ms)
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer!.Stop();
                if (GetSelectedOptionText(cmbOptions) == "JSON Evaluate")
                {
                    EvaluateJsonPathToBottomBox(txtMask.Text);
                }
            };

            tbInput.TextChanged += (s, e) =>
            {
                if (GetSelectedOptionText(cmbOptions) == "JSON Evaluate")
                {
                    _jsonRoot = null; // force re-parse next time
                    Debounce();
                }
            };
            
            txtMask.TextChanged += (s, e) =>
            {
                if (GetSelectedOptionText(cmbOptions) == "JSON Evaluate")
                    Debounce();
            };
        }

        private async Task SetDefaultsAsync()
        {
            foreach (var process in processList)
                cmbOptions.Items.Add(process);

            cmbOptions.SelectedItem = defaultProcess;
        }

        private async void BtnProcessClick(object sender, RoutedEventArgs e)
        {
            await OnClickProcessInput();
        }

        private void CmbOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var opt = GetSelectedOptionText(cmbOptions);

            if (opt is "AES-256 Encrypt" or "AES-256 Decrypt")
            {
                lblPassword.Content = "Password (Limit - 32 characters)";
                lblPassword.Visibility = Visibility.Visible;
                lblPassword.IsEnabled = true;

                pbPassword.Visibility = Visibility.Visible;
                pbPassword.IsEnabled = true;

                txtMask.Visibility = Visibility.Collapsed;
                txtMask.IsEnabled = false;
            }
            else if (opt == "JSON Evaluate")
            {
                lblPassword.Content = "JSON Evaluate - Input Mask";
                lblPassword.Visibility = Visibility.Visible;
                lblPassword.IsEnabled = true;

                txtMask.Visibility = Visibility.Visible;
                txtMask.IsEnabled = true;

                pbPassword.Visibility = Visibility.Collapsed;
                pbPassword.IsEnabled = false;

                _jsonRoot = null; // kick an initial evaluation
                Debounce();
            }
            else
            {
                lblPassword.Content = "Password (Limit - 32 characters)";
                txtMask.Text = string.Empty;

                lblPassword.Visibility = Visibility.Collapsed;
                lblPassword.IsEnabled = false;

                pbPassword.Visibility = Visibility.Collapsed;
                pbPassword.IsEnabled = false;

                txtMask.Visibility = Visibility.Collapsed;
                txtMask.IsEnabled = false;
            }
        }

        private void Debounce()
        {
            _debounceTimer!.Stop();
            _debounceTimer!.Start();
        }

        // ---- Text helpers (TextBox-only) ----
        private static string GetSelectedOptionText(ComboBox combo)
        {
            if (combo?.SelectedItem is string s) return s;
            if (combo?.SelectedItem is ComboBoxItem cbi) return cbi.Content?.ToString() ?? "";
            return combo?.Text ?? "";
        }

        private string GetInputText() => tbInput.Text ?? string.Empty;
        private void SetOutputText(string text) => tbOutput.Text = text ?? string.Empty;

        private static string NL(string s) => (s ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

        // ---- JSON Evaluate ----
        private bool EnsureJsonParsed()
        {
            if (_jsonRoot != null) return true;

            var src = GetInputText();
            if (string.IsNullOrWhiteSpace(src))
            {
                SetOutputText(string.Empty);
                return false;
            }

            try
            {
                _jsonRoot = JToken.Parse(src);
                return true;
            }
            catch (Exception ex)
            {
                _jsonRoot = null;
                SetOutputText($"[Parse error] {ex.Message}");
                return false;
            }
        }

        private void EvaluateJsonPathToBottomBox(string mask)
        {
            try
            {
                if (!EnsureJsonParsed()) return;

                if (string.IsNullOrWhiteSpace(mask))
                {
                    SetOutputText(_jsonRoot!.ToString(Formatting.Indented));
                    return;
                }

                var matches = _jsonRoot!.SelectTokens(mask, errorWhenNoMatch: false);
                var sb = new StringBuilder();
                int count = 0;
                foreach (var t in matches)
                {
                    if (t is JValue v)
                        sb.AppendLine(v.ToString(CultureInfo.InvariantCulture));
                    else
                        sb.AppendLine(t.ToString(Formatting.Indented));
                    count++;
                }

                SetOutputText(count == 0 ? "[No matches]" : sb.ToString());
            }
            catch (Exception ex)
            {
                SetOutputText($"[JSONPath error] {ex.Message}");
            }
        }

        // ---- Base64 (Unicode) ----
        private static string ToBase64Unicode(string text)
            => Convert.ToBase64String(Encoding.Unicode.GetBytes(text ?? string.Empty));

        private static string FromBase64Unicode(string text)
        {
            try
            {
                var bytes = Convert.FromBase64String(text ?? "");
                return Encoding.Unicode.GetString(bytes);
            }
            catch { return string.Empty; }
        }

        // ---- AES-256-CBC (PKCS7) ----
        private static byte[] DeriveKeyFromPassword(string password)
        {
            if (password.Length > 32) password = password[..32];
            if (password.Length < 32) password = password + Key.Substring(password.Length);
            return Encoding.UTF8.GetBytes(password); // 32 bytes
        }

        private static byte[] GetIvBytes() => Encoding.UTF8.GetBytes(IV); // 16 bytes

        private static string Aes256Encrypt(string plaintext, string password)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DeriveKeyFromPassword(password);
            aes.IV = GetIvBytes();

            using var enc = aes.CreateEncryptor();
            var pt = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
            var ct = enc.TransformFinalBlock(pt, 0, pt.Length);
            return Convert.ToBase64String(ct);
        }

        private static string Aes256Decrypt(string base64Cipher, string password)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DeriveKeyFromPassword(password);
            aes.IV = GetIvBytes();

            using var dec = aes.CreateDecryptor();
            var ct = Convert.FromBase64String(base64Cipher ?? "");
            var pt = dec.TransformFinalBlock(ct, 0, ct.Length);
            return Encoding.UTF8.GetString(pt);
        }

        // ---- Convert button entry point ----
        public async Task OnClickProcessInput()
        {
            await Task.Yield();

            try
            {
                var option = GetSelectedOptionText(cmbOptions);
                if (string.IsNullOrWhiteSpace(option))
                {
                    MessageBox.Show("Please select a process from the dropdown.");
                    SetOutputText(string.Empty);
                    return;
                }

                var input = GetInputText();
                string output = string.Empty;

                switch (option)
                {
                    case "SQL Reformat":
                        {
                            string oldText = NL(input).Trim();
                            oldText = Regex.Replace(oldText, @"\s+on\b", " on", RegexOptions.IgnoreCase);
                            oldText = oldText.Replace("\n", "");
                            var parts = oldText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(p => p.Trim()).Where(p => p.Length > 0);
                            output = string.Join(", ", parts);
                            break;
                        }

                    case "Add Commas": output = NL(input).Replace("\n", ",\n"); break;
                    case "Add Commas - Flat": output = NL(input).Replace("\n", ", "); break;
                    case "Add Commas - Flat - No Spaces": output = NL(input).Replace("\n", ","); break;

                    case "Ticks":
                        {
                            var s = NL(input).Trim('\n');
                            output = "'" + s.Replace("\n", "',\n'") + "'";
                            break;
                        }
                    case "Ticks - Flat":
                        {
                            var s = NL(input).Trim('\n');
                            output = "'" + s.Replace("\n", "', '") + "'";
                            break;
                        }
                    case "Ticks - Flat - No Spaces":
                        {
                            var s = NL(input).Trim('\n');
                            output = "'" + s.Replace("\n", "','") + "'";
                            break;
                        }

                    case "Quotes":
                        {
                            var s = NL(input).Trim('\n');
                            output = "\"" + s.Replace("\n", "\",\n\"") + "\"";
                            break;
                        }
                    case "Quotes - Flat":
                        {
                            var s = NL(input).Trim('\n');
                            output = "\"" + s.Replace("\n", "\", \"") + "\"";
                            break;
                        }

                    case "QUOTENAME([column_name], '\"')":
                        output = NL(input).Replace("\n", ", '\"'),\nQUOTENAME(\"");
                        break;

                    case "SQL NVARCHAR[255]":
                        {
                            var cleaned = NL(input).Replace(" ", "");
                            var tokens = cleaned.Split(new[] { "\n", "," }, StringSplitOptions.None)
                                                .Where(t => !string.IsNullOrWhiteSpace(t));
                            var sb = new StringBuilder();
                            foreach (var col in tokens)
                            {
                                sb.Append('[')
                                  .Append(col)
                                  .Append("]  NULL,")
                                  .Append("\n\t");
                            }
                            output = sb.ToString();
                            break;
                        }

                    case "837/835": output = input.Replace("~", "~\n"); break;
                    case "Comma Split": output = input.Replace(",", ",\n"); break;
                    case "GUID": output = input.Replace("~", "\n"); break;

                    case "{ get; set; }":
                        {
                            var tokens = NL(input).Replace(" ", "")
                                                  .Split(new[] { "\n", "," }, StringSplitOptions.None)
                                                  .Where(t => !string.IsNullOrWhiteSpace(t));
                            var sb = new StringBuilder();
                            foreach (var name in tokens)
                            {
                                sb.Append("public string ").Append(name).Append('\n')
                                  .Append("{\n")
                                  .Append("    get { return _").Append(name).Append("; }\n")
                                  .Append("    set { this._").Append(name).Append(" = value; }\n")
                                  .Append("}\n\n");
                            }
                            output = sb.ToString();
                            break;
                        }

                    case "{ get; set; } => Properties only":
                        {
                            var tokens = NL(input).Replace(" ", "")
                                                  .Split(new[] { "\n", "," }, StringSplitOptions.None)
                                                  .Where(t => !string.IsNullOrWhiteSpace(t));
                            var sb = new StringBuilder();
                            foreach (var name in tokens)
                                sb.Append("public string ").Append(name).Append(" { get; set; }").Append('\n');
                            output = sb.ToString();
                            break;
                        }

                    case "Class list":
                        {
                            var tokens = NL(input).Replace(" ", "")
                                                  .Split(new[] { "\n", "," }, StringSplitOptions.None)
                                                  .Where(t => !string.IsNullOrWhiteSpace(t));
                            var sb = new StringBuilder();
                            foreach (var name in tokens)
                                sb.Append("this._").Append(name).Append(" = ").Append(name).Append(";").Append('\n');
                            output = sb.ToString();
                            break;
                        }

                    case "CopySQLtoC#":
                        {
                            var s = NL(input);
                            const string character = @"\n";
                            output = s.Replace("\n", " " + character + "\" + \n\"");
                            break;
                        }

                    case "CopySQLtoJavaScript":
                        output = NL(input).Replace("\n", " \" \n+ \"\\r\\n");
                        break;

                    case "JSON Beautify":
                        {
                            var jsonObj = JsonConvert.DeserializeObject(input);
                            output = jsonObj != null ? JsonConvert.SerializeObject(jsonObj, Formatting.Indented)
                                                     : "[Parse error]";
                            break;
                        }

                    case "JSON Shrink":
                        output = Regex.Replace(input, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1");
                        break;

                    case "JSON Evaluate":
                        EvaluateJsonPathToBottomBox(txtMask.Text?.Trim() ?? "");
                        return; // already wrote to bottom box

                    case "XML => JSON":
                        {
                            var doc = XDocument.Parse(input);
                            output = JsonConvert.SerializeXNode(doc);
                            break;
                        }

                    case "HL7 Breakdown":
                        {
                            int columnCounter, fieldCounter;
                            var reformatted = new StringBuilder();

                            var lines = NL(input).Split(new[] { "\n" }, StringSplitOptions.None);
                            foreach (var line in lines)
                            {
                                columnCounter = 0;
                                var columns = line.Split(new[] { "|" }, StringSplitOptions.None);
                                string columnName = "";

                                foreach (var column in columns)
                                {
                                    fieldCounter = 1;
                                    var fields = column.Split(new[] { "^" }, StringSplitOptions.None);

                                    if (columnCounter == 0)
                                    {
                                        columnName = column;
                                    }
                                    else
                                    {
                                        foreach (var field in fields)
                                        {
                                            if (!string.IsNullOrEmpty(field))
                                                reformatted.Append("\r\n")
                                                           .Append(columnName).Append('.')
                                                           .Append(columnCounter).Append('.')
                                                           .Append(fieldCounter).Append(": ")
                                                           .Append(field);
                                            fieldCounter++;
                                        }
                                    }
                                    columnCounter++;
                                }
                            }
                            output = reformatted.ToString();
                            break;
                        }

                    case "MSH Reformat":
                        output = input.Replace("MSH", "~~~MSH");
                        break;

                    case "Replace Feeds":
                        {
                            var tmp = input.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                            output = tmp.Replace("~~~MSH", "\r\nMSH");
                            break;
                        }

                    case "Mirth | seperate": output = NL(input).Replace("\n", "|"); break;
                    case "Mirth , seperate": output = NL(input).Replace("\n", ","); break;
                    case "Mirth deliminator":
                        {
                            string ending = @"') + delimiter + results.getString('";
                            output = NL(input).Replace("\n", ending);
                            break;
                        }
                    case "Mirth quoted deliminator":
                        {
                            string ending = @"') + quote + delimiter + quote + results.getString('";
                            output = NL(input).Replace("\n", ending);
                            break;
                        }

                    case "Json Split Variables":
                        {
                            var lines = NL(input).Replace(" ", "")
                                                 .Split(new[] { "\n", "," }, StringSplitOptions.None);
                            var sb = new StringBuilder();
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                var item = line.Split(new[] { ":" }, StringSplitOptions.None);
                                if (item.Length > 0)
                                {
                                    var key = item[0].Replace("\"", "").Trim();
                                    if (sb.Length > 0) sb.Append('\n');
                                    sb.Append(key);
                                }
                            }
                            output = sb.ToString();
                            break;
                        }

                    case "Json Class Objects":
                        {
                            var lines = NL(input).Replace(" ", "")
                                                 .Split(new[] { "\n", "," }, StringSplitOptions.None);
                            var sb = new StringBuilder();
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                var name = line.Trim();
                                sb.Append("[JsonProperty(\"").Append(name).Append("\")]\n")
                                  .Append("public string ").Append(name).Append(" { get; set; }")
                                  .Append("\r\n\r\n");
                            }
                            output = sb.ToString();
                            break;
                        }

                    case "Base64 Encrypt": output = ToBase64Unicode(input); break;
                    case "Base64 Decrypt": output = FromBase64Unicode(input); break;

                    case "AES-256 Encrypt":
                        {
                            var pwd = pbPassword.Password ?? string.Empty;
                            if (pwd.Length > 32)
                            {
                                MessageBox.Show("The password is too long.  It must be less than 32 characters.");
                                return;
                            }
                            output = Aes256Encrypt(input, pwd);
                            break;
                        }

                    case "AES-256 Decrypt":
                        {
                            var pwd = pbPassword.Password ?? string.Empty;
                            if (pwd.Length > 32)
                            {
                                MessageBox.Show("The password is too long.  It must be less than 32 characters.");
                                return;
                            }
                            output = Aes256Decrypt(input, pwd);
                            break;
                        }

                    default:
                        output = input;
                        break;
                }

                SetOutputText(output);
            }
            catch (Exception ex)
            {
                tbOutput.Text = "Error: " + ex.Message + "\r\n\r\nTry again!!!";
            }
        }
    }
}