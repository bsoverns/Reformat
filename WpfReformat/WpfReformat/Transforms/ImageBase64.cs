using System.IO;
using System.Text;

namespace WpfReformat.Transforms
{
    /// <summary>Image bytes ⇄ Base64 text. Pure functions so they can be unit tested without a window.</summary>
    public static class ImageBase64
    {
        public const string NoImageMessage =
            "Paste an image (Ctrl+V), drop an image file onto the input box, or type the path to an image file.";

        /// <summary>Recognizes the image format from its first bytes. Returns null when it isn't an image we know.</summary>
        public static string? DetectMime(byte[] bytes)
        {
            bool StartsWith(params byte[] magic) => bytes.Length >= magic.Length && bytes.AsSpan(0, magic.Length).SequenceEqual(magic);

            if (StartsWith(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return "image/png";
            if (StartsWith(0xFF, 0xD8, 0xFF)) return "image/jpeg";
            if (StartsWith((byte)'G', (byte)'I', (byte)'F', (byte)'8')) return "image/gif";
            // BMP and ICO signatures are short (text can start with "BM"), so also require their reserved bytes / image count.
            if (StartsWith((byte)'B', (byte)'M') && bytes.Length >= 14 && bytes.AsSpan(6, 4).IndexOfAnyExcept((byte)0) < 0) return "image/bmp";
            if (StartsWith(0x00, 0x00, 0x01, 0x00) && bytes.Length >= 6 && (bytes[4] | bytes[5]) != 0) return "image/x-icon";
            if (StartsWith((byte)'I', (byte)'I', 0x2A, 0x00) || StartsWith((byte)'M', (byte)'M', 0x00, 0x2A)) return "image/tiff";
            if (bytes.Length >= 12 && StartsWith((byte)'R', (byte)'I', (byte)'F', (byte)'F')
                && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "image/webp";
            return null;
        }

        /// <summary>File extension (with dot) for a MIME type from <see cref="DetectMime"/>.</summary>
        public static string Extension(string mime) => mime switch
        {
            "image/jpeg" => ".jpg",
            "image/x-icon" => ".ico",
            _ => "." + mime["image/".Length..]
        };

        /// <summary>Short name for the status/caption, e.g. "PNG".</summary>
        public static string FormatName(string mime) => Extension(mime).TrimStart('.').ToUpperInvariant();

        public static string Encode(byte[] bytes, bool dataUri)
        {
            var mime = DetectMime(bytes) ?? throw new InvalidOperationException("That isn't a recognized image (PNG, JPEG, GIF, BMP, ICO, TIFF, WEBP).");
            var base64 = Convert.ToBase64String(bytes);
            return dataUri ? $"data:{mime};base64,{base64}" : base64;
        }

        /// <summary>
        /// Accepts raw Base64, a data: URI, wrapped lines, URL-safe Base64 and missing padding.
        /// Throws <see cref="InvalidOperationException"/> when it isn't Base64 or doesn't decode to an image.
        /// </summary>
        public static (byte[] Bytes, string Mime) Decode(string input)
        {
            var text = input.Trim().Trim('"', '\'');
            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = text.IndexOf(',');
                if (comma < 0) throw new InvalidOperationException("The data: URI has no ',' before the Base64.");
                text = text[(comma + 1)..];
            }

            var sb = new StringBuilder(text.Length + 3);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;
                if (!IsBase64Char(c))
                    throw new InvalidOperationException($"Input is not valid Base64: unexpected '{c}' at position {i + 1:N0}.");
                sb.Append(c switch { '-' => '+', '_' => '/', _ => c });
            }

            var body = sb.ToString().TrimEnd('=');
            if (body.Length == 0) throw new InvalidOperationException("Paste Base64 image text into the input box first.");
            if (body.Contains('=')) throw new InvalidOperationException("Input is not valid Base64: '=' padding can only appear at the end.");
            if (body.Length % 4 == 1) throw new InvalidOperationException("Input is not valid Base64: it looks cut off (one character too many or too few).");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(body.PadRight((body.Length + 3) / 4 * 4, '='));
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Input is not valid Base64: " + ex.Message);
            }

            var mime = DetectMime(bytes) ?? throw new InvalidOperationException(
                "The Base64 decoded fine, but it isn't a recognized image (PNG, JPEG, GIF, BMP, ICO, TIFF, WEBP). Try a text Base64 Decode instead.");
            return (bytes, mime);
        }

        private static bool IsBase64Char(char c) =>
            c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/' or '-' or '_' or '=';

        /// <summary>Reads an image file when the input box holds a path (Explorer's "Copy as path" adds quotes).</summary>
        public static byte[] ReadImageFile(string input)
        {
            var path = input.Trim().Trim('"');
            if (path.Length == 0 || path.Contains('\n') || !File.Exists(path)) throw new InvalidOperationException(NoImageMessage);
            return TryReadImageFile(path, out var error) ?? throw new InvalidOperationException(error);
        }

        /// <summary>
        /// Reads a file only if it starts like an image, so a huge non-image file isn't loaded into memory.
        /// Returns null with a user-facing reason otherwise.
        /// </summary>
        public static byte[]? TryReadImageFile(string path, out string error)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var header = new byte[16];
                    int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
                    if (DetectMime(header[..read]) is null)
                    {
                        error = $"\"{Path.GetFileName(path)}\" isn't a recognized image (PNG, JPEG, GIF, BMP, ICO, TIFF, WEBP).";
                        return null;
                    }
                }
                error = string.Empty;
                return File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                error = $"Couldn't read \"{Path.GetFileName(path)}\": {ex.Message}";
                return null;
            }
        }

        /// <summary>One-line description, e.g. "PNG image, 12.3 KB".</summary>
        public static string Describe((byte[] Bytes, string Mime) image) => $"{FormatName(image.Mime)} image, {FormatSize(image.Bytes.Length)}";

        public static string FormatSize(long bytes) => bytes switch
        {
            < 1024 => $"{bytes:N0} bytes",
            < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
            _ => $"{bytes / (1024.0 * 1024):N1} MB"
        };
    }
}
