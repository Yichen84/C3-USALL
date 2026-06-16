using System.IO;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class ClearBridgeImageInput
    {
        public BitmapSource Image { get; init; }

        public byte[] PngBytes { get; init; }

        public string SourceName { get; init; } = string.Empty;

        public int Width => Image.PixelWidth;

        public int Height => Image.PixelHeight;

        public long ByteLength => PngBytes.LongLength;

        public Rectangle? SourceScreenBounds { get; init; }

        public ClearBridgeImageInput(
            BitmapSource image,
            byte[] pngBytes,
            string sourceName,
            Rectangle? sourceScreenBounds = null)
        {
            Image = image;
            PngBytes = pngBytes;
            SourceName = sourceName;
            SourceScreenBounds = sourceScreenBounds;
        }

        public BitmapImage ToPreviewImage()
        {
            using var stream = new MemoryStream(PngBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
