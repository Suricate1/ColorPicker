using System;
using System.Drawing;
using System.Windows.Forms;

namespace ColorPicks
{
    /// <summary>
    /// Custom panel that draws an enlarged pixel-grid view of a region of a source Bitmap,
    /// centred on a chosen (X, Y) coordinate.
    /// Mirrors Java ImageMagnifierPanel (extends JPanel + paintComponent).
    /// </summary>
    public class ImageMagnifierPanel : Panel
    {
        private int _centerX = -1;
        private int _centerY = -1;
        private Bitmap? _source = null;
        private int _zoom = 12;

        public ImageMagnifierPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.DarkGray;
        }

        /// <summary>Sets the pixel coordinate of interest and the source bitmap.</summary>
        public void SetCenter(int ox, int oy, Bitmap src)
        {
            _centerX = ox;
            _centerY = oy;
            _source  = src;
        }

        /// <summary>Sets the zoom level (minimum 2).</summary>
        public void SetZoom(int z) => _zoom = Math.Max(2, z);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int w = Width, h = Height;
            var g = e.Graphics;

            g.FillRectangle(Brushes.DarkGray, 0, 0, w, h);

            if (_source == null || _centerX < 0 || _centerY < 0)
            {
                g.DrawString("Load an image and hover to magnify", Font, Brushes.LightGray, 12, 20);
                return;
            }

            // ---- determine how many source pixels to sample (odd number) ----
            int samplePixels = Math.Max(9, Math.Min(33, Math.Min(w, h) / Math.Max(6, _zoom / 2)));
            if (samplePixels % 2 == 0) samplePixels++;
            int half = samplePixels / 2;

            // top-left of sampling window (clamped)
            int sx = Math.Max(0, _centerX - half);
            int sy = Math.Max(0, _centerY - half);
            if (sx + samplePixels > _source.Width)  sx = Math.Max(0, _source.Width  - samplePixels);
            if (sy + samplePixels > _source.Height) sy = Math.Max(0, _source.Height - samplePixels);

            // ---- draw enlarged pixel blocks ----
            int pixelSize = _zoom;
            int drawSize  = samplePixels * pixelSize;
            int drawX     = (w - drawSize) / 2;
            int drawY     = (h - drawSize) / 2;

            for (int yy = 0; yy < samplePixels; yy++)
            {
                for (int xx = 0; xx < samplePixels; xx++)
                {
                    Color pixel = _source.GetPixel(sx + xx, sy + yy);
                    using var brush = new SolidBrush(pixel);
                    g.FillRectangle(brush,
                        drawX + xx * pixelSize,
                        drawY + yy * pixelSize,
                        pixelSize, pixelSize);
                }
            }

            // ---- grid lines ----
            using (var gridPen = new Pen(Color.FromArgb(100, 0, 0, 0)))
            {
                for (int i = 0; i <= samplePixels; i++)
                {
                    int gx = drawX + i * pixelSize;
                    g.DrawLine(gridPen, gx, drawY, gx, drawY + drawSize);
                    int gy = drawY + i * pixelSize;
                    g.DrawLine(gridPen, drawX, gy, drawX + drawSize, gy);
                }
            }

            // ---- centre crosshair ----
            int cx = drawX + (samplePixels / 2) * pixelSize + pixelSize / 2;
            int cy = drawY + (samplePixels / 2) * pixelSize + pixelSize / 2;
            g.DrawLine(Pens.White, cx - 12, cy, cx + 12, cy);
            g.DrawLine(Pens.White, cx, cy - 12, cx, cy + 12);
            g.DrawEllipse(Pens.Black,
                cx - pixelSize / 2, cy - pixelSize / 2,
                pixelSize, pixelSize);

            // ---- colour swatch + hex label at bottom-left ----
            int safeX = Math.Max(0, Math.Min(_centerX, _source.Width  - 1));
            int safeY = Math.Max(0, Math.Min(_centerY, _source.Height - 1));
            Color center = _source.GetPixel(safeX, safeY);

            int sw   = 36;
            int swX  = 12;
            int swY  = h - sw - 12;

            using (var swBrush = new SolidBrush(center))
                g.FillRectangle(swBrush, swX, swY, sw, sw);

            g.DrawRectangle(Pens.White, swX, swY, sw, sw);

            string hex = ColorUtils.ColorToHex(center);
            g.DrawString(hex, Font, Brushes.White, swX + sw + 8, swY + sw / 2 - 6);
        }
    }
}
