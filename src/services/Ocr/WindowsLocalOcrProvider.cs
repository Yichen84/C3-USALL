using System.Diagnostics;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

#pragma warning disable CA1416
namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class WindowsLocalOcrProvider : IClearBridgeOcrProvider
    {
        public string Id => "windows-local";

        public string DisplayName => "Local OCR (Windows)";

        public bool IsCloudBased => false;

        public async Task<ClearBridgeOcrResult> ExtractTextAsync(
            ClearBridgeImageInput input,
            CancellationToken cancellationToken)
        {
            var started = Stopwatch.StartNew();
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null)
                throw new ClearBridgeOcrException(
                    "LocalOcrUnavailable",
                    "Windows OCR is not available for the current user languages.");

            try
            {
                using var softwareBitmap = await CreateSoftwareBitmapAsync(input.Image, cancellationToken);
                var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
                started.Stop();

                return new ClearBridgeOcrResult
                {
                    Text = result.Text?.Trim() ?? string.Empty,
                    EngineId = Id,
                    EngineName = DisplayName,
                    IsCloudBased = IsCloudBased,
                    Duration = started.Elapsed,
                    ImageWidth = input.Width,
                    ImageHeight = input.Height,
                    ImageBytes = input.ByteLength
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ClearBridgeOcrException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ClearBridgeOcrException("LocalOcrFailed", "Local OCR failed.", ex);
            }
        }

        private static async Task<SoftwareBitmap> CreateSoftwareBitmapAsync(
            BitmapSource source,
            CancellationToken cancellationToken)
        {
            var bitmap = OcrImageUtility.NormalizeForOcr(source);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                stream).AsTask(cancellationToken);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth,
                (uint)bitmap.PixelHeight,
                bitmap.DpiX,
                bitmap.DpiY,
                pixels);
            await encoder.FlushAsync().AsTask(cancellationToken);

            stream.Seek(0);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
            return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied).AsTask(cancellationToken);
        }
    }
}
