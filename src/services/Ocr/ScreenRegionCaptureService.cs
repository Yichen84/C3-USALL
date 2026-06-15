using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class ScreenRegionCaptureService
    {
        private const int MinimumCaptureSize = 8;

        public ClearBridgeImageInput CaptureRegion(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var form = new RegionSelectionForm();
            var result = form.ShowDialog();
            cancellationToken.ThrowIfCancellationRequested();

            if (result == Forms.DialogResult.Cancel)
                throw new OperationCanceledException(cancellationToken);

            if (form.SelectedBounds == null)
                throw new ClearBridgeOcrException("CaptureCancelled", "Capture was cancelled.");

            var bounds = form.SelectedBounds.Value;
            if (bounds.Width < MinimumCaptureSize || bounds.Height < MinimumCaptureSize)
                throw new ClearBridgeOcrException("CaptureTooSmall", "The selected screen area is too small.");

            try
            {
                using var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    BitmapSource source = OcrImageUtility.CreateBitmapSourceFromHBitmap(hBitmap);
                    return OcrImageUtility.FromBitmapSource(source, "Screen Region");
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex) when (ex is not ClearBridgeOcrException)
            {
                throw new ClearBridgeOcrException("CaptureFailed", "Screen capture failed.", ex);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private sealed class RegionSelectionForm : Forms.Form
        {
            private Point startPoint;
            private Point currentPoint;
            private bool selecting;

            public Rectangle? SelectedBounds { get; private set; }

            public RegionSelectionForm()
            {
                Bounds = Forms.SystemInformation.VirtualScreen;
                FormBorderStyle = Forms.FormBorderStyle.None;
                StartPosition = Forms.FormStartPosition.Manual;
                TopMost = true;
                ShowInTaskbar = false;
                BackColor = Color.Black;
                Opacity = 0.32;
                Cursor = Forms.Cursors.Cross;
                KeyPreview = true;
                DoubleBuffered = true;
            }

            protected override void OnKeyDown(Forms.KeyEventArgs e)
            {
                if (e.KeyCode == Forms.Keys.Escape)
                {
                    DialogResult = Forms.DialogResult.Cancel;
                    Close();
                    return;
                }

                base.OnKeyDown(e);
            }

            protected override void OnMouseDown(Forms.MouseEventArgs e)
            {
                if (e.Button != Forms.MouseButtons.Left)
                {
                    DialogResult = Forms.DialogResult.Cancel;
                    Close();
                    return;
                }

                selecting = true;
                startPoint = e.Location;
                currentPoint = e.Location;
                Capture = true;
                Invalidate();
                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(Forms.MouseEventArgs e)
            {
                if (!selecting)
                    return;

                currentPoint = e.Location;
                Invalidate();
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(Forms.MouseEventArgs e)
            {
                if (!selecting)
                    return;

                selecting = false;
                Capture = false;
                currentPoint = e.Location;

                var local = GetSelectionRectangle();
                SelectedBounds = new Rectangle(
                    Bounds.Left + local.Left,
                    Bounds.Top + local.Top,
                    local.Width,
                    local.Height);

                DialogResult = Forms.DialogResult.OK;
                Close();
                base.OnMouseUp(e);
            }

            protected override void OnPaint(Forms.PaintEventArgs e)
            {
                base.OnPaint(e);
                if (!selecting)
                    return;

                var rect = GetSelectionRectangle();
                using var fill = new SolidBrush(Color.FromArgb(80, Color.DeepSkyBlue));
                using var border = new Pen(Color.DeepSkyBlue, 3);
                e.Graphics.FillRectangle(fill, rect);
                e.Graphics.DrawRectangle(border, rect);
            }

            private Rectangle GetSelectionRectangle()
            {
                var left = Math.Min(startPoint.X, currentPoint.X);
                var top = Math.Min(startPoint.Y, currentPoint.Y);
                var right = Math.Max(startPoint.X, currentPoint.X);
                var bottom = Math.Max(startPoint.Y, currentPoint.Y);
                return Rectangle.FromLTRB(left, top, right, bottom);
            }
        }
    }
}
