using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfReformat.Transforms;

namespace WpfReformat
{
    public partial class MainWindow : Window
    {
        private static readonly string LastOperationFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reformat", "last-operation.txt");

        private JToken? _jsonRoot = null;                 // cached JSON for live JSON Beautify/Evaluate
        private byte[]? _inputImage = null;               // pasted/dropped image, shown in place of the input text
        private byte[]? _outputImage = null;              // decoded image, shown in place of the output text
        private string _outputImageMime = string.Empty;
        // Debounce for live mask typing. Created as a field so it exists before any event handler can run.
        private readonly DispatcherTimer _debounceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

        public MainWindow()
        {
            InitializeComponent();

            // CI builds stamp the version (e.g. 2026.10.16.12); local builds stay 1.0.0.0.
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title += version is null || version.Major <= 1 ? " (dev build)" : $" (v{version})";

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
                else if (IsPasteGesture(e) && !pbPassword.IsKeyboardFocusWithin && !txtMask.IsKeyboardFocusWithin)
                {
                    if (TryGetClipboardImage() is { } image)
                    {
                        e.Handled = true;
                        await SetInputImageAsync(image);
                    }
                    else if (_inputImage is not null)
                    {
                        // Pasting text replaces the image.
                        e.Handled = true;
                        ClearInputImage();
                        tbInput.Paste();
                    }
                }
            };

            Closing += (s, e) => SaveLastOperation();

            // Populate the dropdown last: selecting an item raises SelectionChanged, which relies on
            // everything above (e.g. JSON Beautify/Evaluate starts the debounce timer).
            var view = new ListCollectionView(OperationCatalog.All.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Operation.Category)));
            cmbOptions.ItemsSource = view;
            cmbOptions.SelectedItem = OperationCatalog.Find(LoadLastOperation())
                                   ?? OperationCatalog.Find(OperationCatalog.DefaultOperation);

            UpdateStatus();
            tbInput.Focus();
        }

        private Operation? SelectedOperation => cmbOptions.SelectedItem as Operation;

        private bool IsJsonEvaluateSelected => SelectedOperation?.Name == OperationCatalog.JsonEvaluateName;

        // ---- Event handlers ----

        private async void BtnProcessClick(object sender, RoutedEventArgs e) => await ProcessInputAsync();

        private void BtnCopyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_outputImage is not null)
                {
                    CopyImageToClipboard(_outputImage, _outputImageMime);
                    txtStatus.Text = "Image copied to clipboard.";
                    return;
                }
                if (string.IsNullOrEmpty(tbOutput.Text)) return;
                Clipboard.SetText(tbOutput.Text);
                txtStatus.Text = "Output copied to clipboard.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Copy failed: " + ex.Message;
            }
        }

        private void BtnSaveImageClick(object sender, RoutedEventArgs e)
        {
            if (_outputImage is null) return;
            var ext = ImageBase64.Extension(_outputImageMime);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "image" + ext,
                DefaultExt = ext,
                Filter = $"{ImageBase64.FormatName(_outputImageMime)} image (*{ext})|*{ext}|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllBytes(dialog.FileName, _outputImage);
                txtStatus.Text = "Saved " + dialog.FileName;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Save failed: " + ex.Message;
            }
        }

        private async void BtnSwapClick(object sender, RoutedEventArgs e)
        {
            if (_outputImage is { } image)
            {
                ClearOutputImage();
                await SetInputImageAsync(image);
                return;
            }
            ClearInputImage();
            tbInput.Text = tbOutput.Text;
            tbOutput.Clear();
            tbInput.Focus();
        }

        private void BtnClearClick(object sender, RoutedEventArgs e)
        {
            ClearInputImage();
            ClearOutputImage();
            tbInput.Clear();
            tbOutput.Clear();
            tbInput.Focus();
        }

        private void ImagePanel_MouseDown(object sender, MouseButtonEventArgs e) => Keyboard.Focus((IInputElement)sender);

        private void Input_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private async void Input_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
            e.Handled = true;
            string error = string.Empty;
            foreach (var file in files)
            {
                if (ImageBase64.TryReadImageFile(file, out error) is { } image)
                {
                    await SetInputImageAsync(image);
                    return;
                }
            }
            txtStatus.Text = error;
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
            var input = _inputImage is null ? Describe(tbInput.Text) : txtInputImageInfo.Text;
            var output = _outputImage is null ? Describe(tbOutput.Text) : txtOutputImageInfo.Text;
            txtStatus.Text = $"Input: {input}    Output: {output}";
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
            ClearOutputImage();
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
            var context = new OperationContext(pbPassword.Password ?? string.Empty, txtMask.Text ?? string.Empty, _inputImage);

            ClearOutputImage();
            btnConvert.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (operation.Name == OperationCatalog.ImageDecodeName)
                {
                    var (bytes, mime) = await Task.Run(() => ImageBase64.Decode(input));
                    tbOutput.Clear();
                    SetOutputImage(bytes, mime);
                }
                else
                {
                    tbOutput.Text = await Task.Run(() => operation.Run(input, context));
                }
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

        // ---- Images (Base64 Image Encode/Decode) ----

        private static bool IsPasteGesture(KeyEventArgs e) =>
            (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) ||
            (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.Shift);

        /// <summary>Shows a pasted/dropped image in the input box and immediately encodes it.</summary>
        private async Task SetInputImageAsync(byte[] image)
        {
            if (ImageBase64.DetectMime(image) is not { } mime)
            {
                txtStatus.Text = "The clipboard/file doesn't hold a recognized image (PNG, JPEG, GIF, BMP, ICO, TIFF, WEBP).";
                return;
            }

            _inputImage = image;
            tbInput.Clear();
            ShowImage(imgInput, txtInputImageInfo, image, mime);
            SetVisible(tbInput, false);
            bdInputImage.Visibility = Visibility.Visible;
            Keyboard.Focus(bdInputImage);

            if (SelectedOperation?.Name is not (OperationCatalog.ImageEncodeName or OperationCatalog.ImageEncodeDataUriName))
                cmbOptions.SelectedItem = OperationCatalog.Find(OperationCatalog.ImageEncodeName);

            UpdateStatus();
            await ProcessInputAsync();
        }

        private void ClearInputImage()
        {
            if (_inputImage is null) return;
            _inputImage = null;
            imgInput.Source = null;
            bdInputImage.Visibility = Visibility.Collapsed;
            SetVisible(tbInput, true);
            tbInput.Focus();
            UpdateStatus();
        }

        private void SetOutputImage(byte[] image, string mime)
        {
            _outputImage = image;
            _outputImageMime = mime;
            ShowImage(imgOutput, txtOutputImageInfo, image, mime);
            bdOutputImage.Visibility = Visibility.Visible;
            btnSaveImage.Visibility = Visibility.Visible;
            UpdateStatus();
        }

        private void ClearOutputImage()
        {
            if (_outputImage is null) return;
            _outputImage = null;
            imgOutput.Source = null;
            bdOutputImage.Visibility = Visibility.Collapsed;
            btnSaveImage.Visibility = Visibility.Collapsed;
            UpdateStatus();
        }

        /// <summary>Returns false when Windows can't draw the image (corrupt/truncated data, or e.g. WEBP without the codec).</summary>
        private static bool ShowImage(Image target, TextBlock caption, byte[] image, string mime)
        {
            var size = ImageBase64.FormatSize(image.Length);
            try
            {
                var bitmap = LoadBitmap(image);
                target.Source = bitmap;
                caption.Text = $"{ImageBase64.FormatName(mime)} image, {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0}, {size}";
                return true;
            }
            catch (Exception ex)
            {
                target.Source = null;
                caption.Text = $"{ImageBase64.FormatName(mime)} image, {size}. Can't display it: {ex.Message} " +
                               "The data may be cut off or corrupted.";
                return false;
            }
        }

        private static BitmapSource LoadBitmap(byte[] image)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(image);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static byte[] EncodePng(BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// The clipboard's image, if it holds one and no text. Text wins because apps like Excel and Word
        /// put a picture of the selection on the clipboard alongside the text.
        /// </summary>
        private static byte[]? TryGetClipboardImage()
        {
            try
            {
                if (Clipboard.ContainsText()) return null;

                // A file copied in Explorer: keep its original bytes/format.
                if (Clipboard.ContainsFileDropList())
                    return Clipboard.GetFileDropList().Cast<string>()
                        .Select(path => ImageBase64.TryReadImageFile(path, out _)).FirstOrDefault(b => b is not null);

                // Browsers and the Snipping Tool provide PNG, which keeps transparency. Some apps label other data
                // "PNG", so only trust it when it really starts like a PNG.
                if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is MemoryStream png
                    && png.ToArray() is var pngBytes && ImageBase64.DetectMime(pngBytes) is not null) return pngBytes;

                if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap) return EncodePng(bitmap);
            }
            catch (Exception)
            {
                // Clipboard busy or holding something unreadable: fall back to a normal paste.
            }
            return null;
        }

        private static void CopyImageToClipboard(byte[] image, string mime)
        {
            BitmapSource bitmap;
            try
            {
                bitmap = LoadBitmap(image);
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Windows can't read this image, so it can't go on the clipboard. Use Save Image... instead.");
            }
            var data = new DataObject();
            data.SetImage(bitmap);
            data.SetData("PNG", new MemoryStream(mime == "image/png" ? image : EncodePng(bitmap)));
            Clipboard.SetDataObject(data, true);
        }
    }
}
