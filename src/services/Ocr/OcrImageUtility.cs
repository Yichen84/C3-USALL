using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiveCaptionsTranslator.services.Ocr
{
    public static class OcrImageUtility
    {
        public const long MaxImageBytes = 10 * 1024 * 1024;
        public const int MaxLongEdgePixels = 4096;

        public static ClearBridgeImageInput FromFile(string filePath)
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
                throw new ClearBridgeOcrException("ImageNotFound", "The selected image file was not found.");
            if (info.Length > MaxImageBytes)
                throw new ClearBridgeOcrException("ImageTooLarge", "The selected image is larger than 10 MB.");

            try
            {
                using var fileStream = File.OpenRead(filePath);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = fileStream;
                bitmap.EndInit();
                bitmap.Freeze();

                var normalized = NormalizeForOcr(bitmap);
                return new ClearBridgeImageInput(
                    normalized,
                    EncodePng(normalized),
                    Path.GetFileName(filePath));
            }
            catch (ClearBridgeOcrException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ClearBridgeOcrException("InvalidImage", "The selected image could not be opened.", ex);
            }
        }

        public static ClearBridgeImageInput FromBitmapSource(
            BitmapSource source,
            string sourceName,
            System.Drawing.Rectangle? sourceScreenBounds = null)
        {
            var normalized = NormalizeForOcr(source);
            return new ClearBridgeImageInput(
                normalized,
                EncodePng(normalized),
                sourceName,
                sourceScreenBounds);
        }

        public static byte[] EncodePng(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        public static BitmapSource NormalizeForOcr(BitmapSource source)
        {
            BitmapSource normalized = source;
            var longEdge = Math.Max(source.PixelWidth, source.PixelHeight);
            if (longEdge > MaxLongEdgePixels)
            {
                var scale = MaxLongEdgePixels / (double)longEdge;
                normalized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                normalized.Freeze();
            }

            if (normalized.Format != PixelFormats.Bgra32)
            {
                normalized = new FormatConvertedBitmap(normalized, PixelFormats.Bgra32, null, 0);
                normalized.Freeze();
            }

            return normalized;
        }

        public static BitmapSource CreateBitmapSourceFromHBitmap(IntPtr hBitmap)
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
    }
}
