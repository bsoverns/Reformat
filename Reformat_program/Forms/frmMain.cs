using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Drawing;
using System.Net;
using System.Net.Mime;
using System.Net.Mail;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Globalization;


namespace Reformat_program
{
    public partial class frmMain : Form
    {
        string Key = "alio1j304klsk49fnhcvlslwie8tgxzj"; //32 CHARACTERS
        string IV = "fjdkwnsksdjklfsk"; //16 CHARACTERS                                        
        private JToken _jsonRoot = null; // Cached JSON root (in-memory only)        
        private System.Windows.Forms.Timer _debounceTimer = null; // Debounce timer for live mask typing


        public frmMain()
        {
            InitializeComponent();
            cmbOptions.Items.Add("SQL Reformat");
            cmbOptions.Items.Add("Add Commas");
            cmbOptions.Items.Add("Add Commas - Flat");
            cmbOptions.Items.Add("Add Commas - Flat - No Spaces");
            cmbOptions.Items.Add("Ticks");
            cmbOptions.Items.Add("Ticks - Flat");
            cmbOptions.Items.Add("Ticks - Flat - No Spaces");
            cmbOptions.Items.Add("JSON Beautify");
            cmbOptions.Items.Add("JSON Shrink");
            cmbOptions.Items.Add("JSON Evaluate");
            cmbOptions.Items.Add("Quotes");
            cmbOptions.Items.Add("Quotes - Flat");
            cmbOptions.Items.Add("QUOTENAME([column_name], '\"')");
            cmbOptions.Items.Add("SQL NVARCHAR[255]");
            cmbOptions.Items.Add("837/835");
            cmbOptions.Items.Add("Comma Split");
            cmbOptions.Items.Add("{ get; set; }");
            cmbOptions.Items.Add("{ get; set; } => Properties only");
            cmbOptions.Items.Add("Class list");
            cmbOptions.Items.Add("CopySQLtoC#");
            cmbOptions.Items.Add("CopySQLtoJavaScript");
            cmbOptions.Items.Add("Json Split Variables");
            cmbOptions.Items.Add("Json Class Objects");
            cmbOptions.Items.Add("Mirth | seperate");
            cmbOptions.Items.Add("Mirth , seperate");
            cmbOptions.Items.Add("Mirth deliminator");
            cmbOptions.Items.Add("Mirth quoted deliminator");
            cmbOptions.Items.Add("Base64 Encrypt");
            cmbOptions.Items.Add("Base64 Decrypt");
            cmbOptions.Items.Add("AES-256 Encrypt");
            cmbOptions.Items.Add("AES-256 Decrypt");
            cmbOptions.Items.Add("MSH Reformat");
            cmbOptions.Items.Add("Replace Feeds");
            cmbOptions.Items.Add("HL7 Breakdown");
            //cmbOptions.Items.Add("ASCII Art");
            //cmbOptions.SelectedItem = "Add Commas";
            cmbOptions.SelectedItem = "SQL Reformat";
            //Mail_message();  

            // Debounce for live updates while typing mask
            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 250; // ms
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                if (cmbOptions.SelectedItem?.ToString() == "JSON Evaluate")
                {
                    EvaluateJsonPathToBottomBox(txtPassword.Text);
                }
            };

            // When typing in the "mask" box (reusing txtPassword), auto-evaluate
            txtPassword.TextChanged += (s, e) =>
            {
                if (cmbOptions.SelectedItem?.ToString() == "JSON Evaluate")
                {
                    Debounce();
                }
            };

            // Also re-parse the JSON whenever the top box changes (kept simple)
            richTextBox1.TextChanged += (s, e) =>
            {
                if (cmbOptions.SelectedItem?.ToString() == "JSON Evaluate")
                {
                    _jsonRoot = null; // force re-parse on next evaluation
                    Debounce();
                }
            };
        }
        private void Debounce()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private bool EnsureJsonParsed()
        {
            if (_jsonRoot != null) return true;

            try
            {
                if (string.IsNullOrWhiteSpace(richTextBox1.Text))
                {
                    richTextBox2.Clear();
                    return false;
                }

                _jsonRoot = JToken.Parse(richTextBox1.Text);
                return true;
            }
            catch (Exception ex)
            {
                _jsonRoot = null;
                richTextBox2.Text = $"[Parse error] {ex.Message}";
                return false;
            }
        }
        private void EvaluateJsonPathToBottomBox(string mask)
        {
            try
            {
                if (!EnsureJsonParsed()) return;

                // Empty mask => pretty print entire JSON
                if (string.IsNullOrWhiteSpace(mask))
                {
                    richTextBox2.Text = _jsonRoot.ToString(Formatting.Indented);
                    return;
                }

                var matches = _jsonRoot.SelectTokens(mask, errorWhenNoMatch: false);

                var sb = new StringBuilder();
                int count = 0;
                foreach (var t in matches)
                {
                    if (t is JValue v)
                    {
                        sb.AppendLine(v.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.AppendLine(t.ToString(Formatting.Indented));
                    }
                    count++;
                }

                richTextBox2.Text = (count == 0) ? "[No matches]" : sb.ToString();
            }
            catch (Exception ex)
            {
                richTextBox2.Text = $"[JSONPath error] {ex.Message}";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //MessageBox.Show(cmbOptions.SelectedItem.ToString());                
                if (cmbOptions.SelectedItem.ToString() == "SQL Reformat")
                {
                    string[] separatingStrings = { "," };

                    string oldText = richTextBox1.Text;
                    string newText = "";

                    string[] words = oldText.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);                    

                    foreach (var word in words)
                    {
                        if (newText.Length.Equals(0))
                        {
                            newText = word.Trim();
                        }
                        else
                        {
                            newText = newText + ", " + word.Trim();
                        }
                    }

                    richTextBox2.Clear();
                    richTextBox2.Text = newText;//richTextBox1.Text.Replace("	", "");
                    richTextBox2.Text = richTextBox2.Text.Replace("\r\n,", ", ");
                    richTextBox2.Text = richTextBox2.Text.Replace("\r,", ", ");
                    richTextBox2.Text = richTextBox2.Text.Replace("\n,", ", ");
                    richTextBox2.Text = richTextBox2.Text.Replace("\r\non", " on");
                    richTextBox2.Text = richTextBox2.Text.Replace("\ron", " on");
                    richTextBox2.Text = richTextBox2.Text.Replace("\non", " on");
                    richTextBox2.Text = richTextBox2.Text.Replace("\r", "");
                    richTextBox2.Text = richTextBox2.Text.Replace("\n", "");
                }
                
                else if (cmbOptions.SelectedItem.ToString() == "Add Commas")
                {
                    richTextBox2.Clear();                    
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Add Commas - Flat")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ", ");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Add Commas - Flat - No Spaces")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ",");
                }                

                else if (cmbOptions.SelectedItem.ToString() == "Ticks")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = @"'" + richTextBox1.Text.Replace("\n", "\',\n\'") + @"'";
                }

                else if (cmbOptions.SelectedItem.ToString() == "Ticks - Flat")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = @"'" + richTextBox1.Text.Replace("\n", "\', \'") + @"'";
                }

                else if (cmbOptions.SelectedItem.ToString() == "Ticks - Flat - No Spaces")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = @"'" + richTextBox1.Text.Replace("\n", "\',\'") + @"'";
                }

                else if (cmbOptions.SelectedItem.ToString() == "Quotes")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = "\"" + richTextBox1.Text.Replace("\n", "\",\n\"") + "\"";
                }

                else if (cmbOptions.SelectedItem.ToString() == "Quotes - Flat")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = "\"" + richTextBox1.Text.Replace("\n", "\", \"") + "\"";
                }

                else if (cmbOptions.SelectedItem.ToString() == "QUOTENAME([column_name], '\"')")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ", \'\"\'),\nQUOTENAME(\"");
                }

                else if (cmbOptions.SelectedItem.ToString() == "SQL NVARCHAR[255]")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        richTextBox2.Text = (richTextBox2.Text + "[" + line.ToString() + "] [nvarchar](255) NULL," + "\n\t");
                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "837/835")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("~", "~\n");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Comma Split")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace(",", ",\n");
                }

                else if (cmbOptions.SelectedItem.ToString() == "GUID")
                {
                    //E9B98DD0-CB1F-40AD-A5C0-ED42B4EF0A0E
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("~", "\n");
                }

                else if (cmbOptions.SelectedItem.ToString() == "{ get; set; }")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        richTextBox2.Text = (richTextBox2.Text + "public string " + line + "\n{\nget { return _" + line.ToString() + "; } \nset { this._" + line.ToString() + " = value; }\n}\n\n");
                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "{ get; set; } => Properties only")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        richTextBox2.Text = (richTextBox2.Text + "public string " + line + " { get; set; }\n");
                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "Class list")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        richTextBox2.Text = (richTextBox2.Text + "this._" + line.ToString() + " = " + line.ToString() + ";" + "\n");
                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "CopySQLtoC#")
                {
                    string character = @"\n";
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", " " + character + "\" + \n\"");
                }
                
                else if (cmbOptions.SelectedItem.ToString() == "CopySQLtoJavaScript")
                {
                    richTextBox2.Clear();
                    //richTextBox2.Text = richTextBox1.Text.Replace("\n", "\" + \n\"");
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", " \" \n+ \"\\r\\n");
                }
                                
                else if (cmbOptions.SelectedItem.ToString() == "Json Split Variables")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\r\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        string[] item = line.Split(new string[] { ":" }, StringSplitOptions.None);

                        if (richTextBox2.Text == "")
                        {
                            richTextBox2.Text = item[0].ToString().Replace("\"","").Trim();
                        }

                        else
                        {
                            richTextBox2.Text = richTextBox2.Text + "\n" + item[0].ToString().Replace("\"", "").Trim();
                        }

                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");

                        //[JsonProperty("regularMarketTime")]
                        //public string regularMarketTime { get; set; } //"regularMarketTime": 1582318848,    
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "Json Class Objects")
                {
                    string[] lines = richTextBox1.Text.Replace(" ", "").Split(new string[] { "\n", "," }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        richTextBox2.Text = (richTextBox2.Text + "[JsonProperty(\"" + line.ToString() + "\")]\npublic string " + line.ToString() + " { get; set; }" + "\r\n\r\n");
                        //richTextBox2.Text = richTextBox1.Text.Replace("\n", ",\n");

                     //[JsonProperty("regularMarketTime")]
                     //public string regularMarketTime { get; set; } //"regularMarketTime": 1582318848,    
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "Mirth | seperate")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", "|");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Mirth , seperate")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ",");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Mirth deliminator")
                {                    
                    string ending = @"') + delimiter + results.getString('";
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ending);
                }

                else if (cmbOptions.SelectedItem.ToString() == "Mirth quoted deliminator")
                {
                    string ending = @"') + quote + delimiter + quote + results.getString('";
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("\n", ending);
                }
                    
                else if (cmbOptions.SelectedItem.ToString() == "Base64 Encrypt")
                {                                       
                    richTextBox2.Clear();
                    richTextBox2.Text = ToBase64UNICODE(richTextBox1.Text);
                }

                else if (cmbOptions.SelectedItem.ToString() == "Base64 Decrypt")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = FromBase64UNICODE(richTextBox1.Text);
                }

                else if (cmbOptions.SelectedItem.ToString() == "AES-256 Encrypt")
                {
                    string password = txtPassword.Text;
                    if (password.Length <= 32)
                    {
                        password = password + Key.Substring(password.Length);
                        richTextBox2.Clear();
                        richTextBox2.Text = EncryptionClass.Encrypt(richTextBox1.Text, password, IV);                        
                    }

                    else
                    {
                        MessageBox.Show("The password is too long.  It must be less than 32 characters.");
                    }
                }

                else if (cmbOptions.SelectedItem.ToString() == "AES-256 Decrypt")
                {
                    string password = txtPassword.Text;
                    if (password.Length <= 32)
                    {
                        password = password + Key.Substring(password.Length);
                        richTextBox2.Clear();
                        richTextBox2.Text = EncryptionClass.Decrypt(richTextBox1.Text, password, IV);
                    }

                    else
                    {
                        MessageBox.Show("The password is too long.  It must be less than 32 characters.");
                    }
                }

              
                else if (cmbOptions.SelectedItem.ToString() == "MSH Reformat")
                {
                    richTextBox2.Clear();
                    richTextBox2.Text = richTextBox1.Text.Replace("MSH", "~~~MSH");
                }

                else if (cmbOptions.SelectedItem.ToString() == "Replace Feeds")
                {
                    RichTextBox tempBox1 = new RichTextBox();
                    RichTextBox tempBox2 = new RichTextBox();
                    RichTextBox tempBox3 = new RichTextBox();

                    richTextBox2.Clear();
                    tempBox1.Text = richTextBox1.Text.Replace("\r\n", "");
                    tempBox2.Text = tempBox1.Text.Replace("\r", "").Replace("\n", "");
                    tempBox3.Text = tempBox2.Text.Replace("~~~MSH", "\r\nMSH");
                    richTextBox2.Text = tempBox3.Text;
                    //richTextBox2.Text.Replace("\r\n", "");
                }

                else if (cmbOptions.SelectedItem.ToString() == "JSON Beautify")                {
                    var jsonObject = JsonConvert.DeserializeObject<dynamic>(richTextBox1.Text); 
                    
                    richTextBox2.Clear();
                    richTextBox2.Text = jsonObject.ToString();                                       
                }

                else if (cmbOptions.SelectedItem.ToString() == "JSON Shrink")
                {
                    JsonSerializer serializer = new JsonSerializer();
                    
                    richTextBox2.Clear();
                    richTextBox2.Text = Regex.Replace(richTextBox1.Text, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1");
                }

                else if (cmbOptions.SelectedItem.ToString() == "HL7 Breakdown")
                {
                    int columnCounter = 0;
                    int fieldCounter = 1;
                    string reformatedData = "";
                    string[] lines = richTextBox1.Text.Split(new string[] { "\n", "" }, StringSplitOptions.None);
                    richTextBox2.Clear();

                    foreach (string line in lines)
                    {
                        columnCounter = 0;
                        string columnName = "";
                        string[] columns = line.Split(new string[] { "|" }, StringSplitOptions.None);

                        foreach (string column in columns)
                        {
                            fieldCounter = 1;
                            string[] fields = column.Split(new string[] { "^" }, StringSplitOptions.None);

                            if (columnCounter == 0)
                            {
                                columnName = column.ToString();                                
                            }

                            else
                            {
                                foreach (string field in fields)
                                {
                                    if (field != "")
                                    {
                                        reformatedData = reformatedData + "\r\n" + columnName.ToString() + "." + columnCounter.ToString() + "." + fieldCounter.ToString() + ": " + field.ToString();
                                    }

                                    fieldCounter++;
                                }                              
                            }

                            columnCounter++;
                        }                        
                    }

                    richTextBox2.Text = reformatedData.ToString();
                }

                else if (cmbOptions.SelectedItem.ToString() == "JSON Evaluate")
                {
                    // Reuse txtPassword as the JSONPath mask input
                    var mask = txtPassword.Text?.Trim() ?? "";
                    EvaluateJsonPathToBottomBox(mask);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Contact Brad!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string Compress(string s)
        {
            var dict = new Dictionary<string, int>();
            char[] data = s.ToCharArray();
            var output = new List<char>();
            char currChar;
            string phrase = data[0].ToString();
            int code = 256;

            for (var i = 1; i < data.Length; i++)
            {
                currChar = data[i];
                var temp = phrase + currChar;
                if (dict.ContainsKey(temp))
                    phrase += currChar;
                else
                {
                    if (phrase.Length > 1)
                        output.Add((char)dict[phrase]);
                    else
                        output.Add((char)phrase[0]);
                    dict[phrase + currChar] = code;
                    code++;
                    phrase = currChar.ToString();
                }
            }

            if (phrase.Length > 1)
                output.Add((char)dict[phrase]);
            else
                output.Add((char)phrase[0]);

            return new string(output.ToArray());
        }

        private string ToBase64UNICODE(string text)
        {
            byte[] _DataArray = ASCIIEncoding.Unicode.GetBytes(text);
            text = Convert.ToBase64String(_DataArray);

            return text;
        }

        private string FromBase64UNICODE(string text)
        {
            try
            {
                byte[] _Base64Array = Convert.FromBase64String(text);
                text = Encoding.Unicode.GetString(_Base64Array);

                return text;
            }

            catch (Exception)
            {
                return "";
            }
        }

        //This section disabled
        private void Mail_message()
        {
            if (Internet.IsConnectedToInternet())
            {
                Rtf2Html rtf = new Rtf2Html();
                string Html = (@"A login with the name of " + Environment.UserName + @" is using the Reformat program on the domain " + Environment.UserDomainName + @" and it is being ran from the system with the name of " + Environment.MachineName + ".");

                string textBody = "<H4>Please open a case for " + Environment.UserName + " for the following errors and list the contact as Emmett Schulte</H4> \n" +
                "<table border=\"1\"> \n" +
                "<tr><th>Practice_name</th><th>Job_number</th><th>Job_name</th><th>Exit_code</th><th>Exit_description</th></tr> \n" +
                "<td>Practice</td><td>job</td><td>job name</td><td>Exit</td><td>exit description</td>";
                

                MailMessage mail_message = new MailMessage();
                mail_message.From = @"@gmail.com";
                mail_message.To = @"@gmail.com";
                //mail_message.To = @"@tsttmobile.co.tt";
                mail_message.Subject = @"Reformat is started " + Environment.UserName;
                mail_message.MailType = MailEncodingType.HTML;
                mail_message.MailPriority = MailSendPriority.NORMAL;                     
                mail_message.Message = Html;              

                Thread thread = new Thread(new ParameterizedThreadStart(this.SendEmail));

                try
                {
                    thread.Start(mail_message);
                }

                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            }
            else
            {
                return;
            }
        }

        //This section disabled
        private void SendEmail(object mail_msg)
        {            
            try
            {
                MailMessage mail_message = (MailMessage)mail_msg;                

                SmtpClient smtp = new SmtpClient(@"smtp.gmail.com", Convert.ToInt32(@"587"));                
                smtp.UserName = @"@gmail.com";
                smtp.Password = @"";  
                smtp.SendMail(mail_message);                
            }

            catch (SmtpClientException obj)
            {
                MessageBox.Show(this, obj.ErrorMessage, "Email Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckConvertType(object sender, EventArgs e)
        {            
            if (cmbOptions.SelectedItem.ToString() == "AES-256 Encrypt" || cmbOptions.SelectedItem.ToString() == "AES-256 Decrypt" )
            {
                lblPassword.Visible = true;
                lblPassword.Enabled = true;
                txtPassword.Visible = true;
                txtPassword.Enabled = true;
            }

            else if (cmbOptions.SelectedItem.ToString() == "JSON Evaluate")
            {
                lblPassword.Text = "JSON Evaluate - Input Mask";
                txtPassword.PasswordChar = '\0'; // show plain text for mask
                lblPassword.Visible = true;
                lblPassword.Enabled = true;
                txtPassword.Visible = true;
                txtPassword.Enabled = true;
                
            }          

            else
            {
                lblPassword.Text = "Password(Limit - 32 characters)";
                txtPassword.Text = "";
                lblPassword.Visible = false;
                lblPassword.Enabled = false;
                txtPassword.Visible = false;
                txtPassword.Enabled = false;
            }
        }
    }

}
