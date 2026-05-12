using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ColorPicks
{
    /// <summary>
    /// A Panel that paints a horizontal linear gradient from StartColor to EndColor.
    /// Mirrors Java GradientPanel (extends JPanel + paintComponent).
    /// </summary>
    public class GradientPanel : Panel
    {
        private Color _startColor = Color.White;
        private Color _endColor = Color.Black;

        public GradientPanel()
        {
            // Enable double-buffering to avoid flicker on resize
            DoubleBuffered = true;
        }

        public Color StartColor
        {
            get => _startColor;
            set
            {
                if (value != Color.Empty) { _startColor = value; Invalidate(); }
            }
        }

        public Color EndColor
        {
            get => _endColor;
            set
            {
                if (value != Color.Empty) { _endColor = value; Invalidate(); }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1) return;

            using var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(Width, 0),
                _startColor,
                _endColor);

            e.Graphics.FillRectangle(brush, 0, 0, Width, Height);
        }
    }
}
