using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfReformat.Transforms;

namespace WpfReformat
{
    public partial class MainWindow : Window
    {
        private static readonly string LastOperationFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reformat", "last-operation.txt");

        private JToken? _jsonRoot = null;                 // cached JSON for live JSON Beautify/Evaluate
        private readonly DispatcherTimer _debounceTimer;  // debounce for live mask typing

        public MainWindow()
        {
            InitializeComponent();

            // CI builds stamp the version (e.g. 2026.10.16.12); local builds stay 1.0.0.0.
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title += version is null || version.Major <= 1 ? " (dev build)" : $" (v{version})";

            var view = new ListCollectionView(OperationCatalog.All.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Operation.Category)));
            cmbOptions.ItemsSource = view;
            cmbOptions.SelectedItem = OperationCatalog.Find(LoadLastOperation())
                                   ?? OperationCatalog.Find(OperationCatalog.DefaultOperation);

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                if (IsJsonEvaluateSelected) EvaluateJsonLive();
            };

            tbInput.TextChanged += (s, e) =>
            {
                UpdateStatus();
                if (IsJsonEvaluateSelected)
                {
                    _jsonRoot = null; // force re-parse next time
                    Debounce();
                }
            };

            txtMask.TextChanged += (s, e) =>
            {
                if (IsJsonEvaluateSelected) Debounce();
            };

            tbOutput.TextChanged += (s, e) => UpdateStatus();

            PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    await ProcessInputAsync();
                }
            };

            Closing += (s, e) => SaveLastOperation();

            UpdateStatus();
            tbInput.Focus();
        }

        private Operation? SelectedOperation => cmbOptions.SelectedItem as Operation;

        private bool IsJsonEvaluateSelected => SelectedOperation?.Name == OperationCatalog.JsonEvaluateName;

        // ---- Event handlers ----

        private async void BtnProcessClick(object sender, RoutedEventArgs e) => await ProcessInputAsync();

        private void BtnCopyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbOutput.Text)) return;
            try
            {
                Clipboard.SetText(tbOutput.Text);
                txtStatus.Text = "Output copied to clipboard.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Copy failed: " + ex.Message;
            }
        }

        private void BtnSwapClick(object sender, RoutedEventArgs e)
        {
            tbInput.Text = tbOutput.Text;
            tbOutput.Clear();
            tbInput.Focus();
        }

        private void BtnClearClick(object sender, RoutedEventArgs e)
        {
            tbInput.Clear();
            tbOutput.Clear();
            tbInput.Focus();
        }

        private void CmbOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var extra = SelectedOperation?.Extra ?? ExtraInput.None;

            bool showPassword = extra is ExtraInput.Password or ExtraInput.StrongPassword;
            bool showMask = extra == ExtraInput.JsonPath;

            lblPassword.Content = extra switch
            {
                ExtraInput.Password => $"Password (max {AesText.LegacyMaxPasswordLength} chars)",
                ExtraInput.StrongPassword => "Password",
                ExtraInput.JsonPath => "JSONPath mask",
                _ => string.Empty
            };
            SetVisible(lblPassword, showPassword || showMask);
            SetVisible(pbPassword, showPassword);
            SetVisible(txtMask, showMask);

            pbPassword.MaxLength = extra == ExtraInput.Password ? AesText.LegacyMaxPasswordLength : 0;
            if (!showMask) txtMask.Text = string.Empty;

            if (showMask)
            {
                _jsonRoot = null; // kick an initial evaluation
                Debounce();
            }
        }

        private static void SetVisible(UIElement element, bool visible)
        {
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            element.IsEnabled = visible;
        }

        private void Debounce()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // ---- Status / settings ----

        private void UpdateStatus()
        {
            txtStatus.Text = $"Input: {Describe(tbInput.Text)}    Output: {Describe(tbOutput.Text)}";
        }

        private static string Describe(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "empty";
            int lines = TextTransforms.NL(text).TrimEnd('\n').Split('\n').Length;
            return $"{lines:N0} line{(lines == 1 ? "" : "s")}, {text.Length:N0} chars";
        }

        private static string? LoadLastOperation()
        {
            try { return File.Exists(LastOperationFile) ? File.ReadAllText(LastOperationFile).Trim() : null; }
            catch { return null; }
        }

        private void SaveLastOperation()
        {
            try
            {
                if (SelectedOperation is null) return;
                Directory.CreateDirectory(Path.GetDirectoryName(LastOperationFile)!);
                File.WriteAllText(LastOperationFile, SelectedOperation.Name);
            }
            catch { /* not worth bothering the user about */ }
        }

        // ---- JSON Beautify/Evaluate (live, re-uses the parsed document while the mask changes) ----

        private void EvaluateJsonLive()
        {
            var src = tbInput.Text;
            if (string.IsNullOrWhiteSpace(src))
            {
                tbOutput.Text = string.Empty;
                return;
            }

            try
            {
                _jsonRoot ??= JToken.Parse(src);
            }
            catch (Exception ex)
            {
                tbOutput.Text = $"[Parse error] {ex.Message}";
                return;
            }

            try
            {
                tbOutput.Text = TextTransforms.JsonEvaluate(_jsonRoot, txtMask.Text);
            }
            catch (Exception ex)
            {
                tbOutput.Text = $"[JSONPath error] {ex.Message}";
            }
        }

        // ---- Convert ----

        private async Task ProcessInputAsync()
        {
            var operation = SelectedOperation;
            if (operation is null)
            {
                MessageBox.Show("Please select a process from the dropdown.");
                tbOutput.Clear();
                return;
            }

            if (operation.Name == OperationCatalog.JsonEvaluateName)
            {
                _jsonRoot = null;
                EvaluateJsonLive();
                return;
            }

            // Read UI values on the UI thread, run the transform in the background so big inputs don't freeze the window.
            var input = tbInput.Text ?? string.Empty;
            var context = new OperationContext(pbPassword.Password ?? string.Empty, txtMask.Text ?? string.Empty);

            btnConvert.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                tbOutput.Text = await Task.Run(() => operation.Run(input, context));
            }
            catch (InvalidOperationException ex)
            {
                tbOutput.Text = "Error: " + ex.Message;
            }
            catch (Exception ex)
            {
                tbOutput.Text = "Error: " + ex.Message + "\r\n\r\nTry again!!!";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                btnConvert.IsEnabled = true;
            }
        }
    }
}
