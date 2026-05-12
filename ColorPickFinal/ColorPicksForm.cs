using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ColorPicks
{
    /// <summary>
    /// Main application window.
    /// Mirrors Java ColorPicks (extends JFrame).
    /// Layout: Tools (left) | Image + Magnifier (centre) | History + Gradient (right) | Export bar (bottom).
    /// </summary>
    public class ColorPicksForm : Form
    {
        // ── readout fields ────────────────────────────────────────────────────
        private readonly TextBox _hexField = new() { Width = 90, ReadOnly = true };
        private readonly TextBox _rgbField = new() { Width = 165, ReadOnly = true };
        private readonly TextBox _hslField = new() { Width = 200, ReadOnly = true };

        // ── current swatch + copy buttons ────────────────────────────────────
        private Panel _currentSwatchBox = null!;
        private Button _copyHexBtn = null!;
        private Button _copyRgbBtn = null!;
        private Label _fileInfoLabel = null!;

        // ── gradient ─────────────────────────────────────────────────────────
        private Color _startColor = Color.White;
        private Color _endColor = Color.Black;
        private GradientPanel _gradientPreview = new();

        // ── history ───────────────────────────────────────────────────────────
        private readonly string _historyFile = Path.Combine(
                                                                       Environment.GetFolderPath(
                                                                           Environment.SpecialFolder.UserProfile),
                                                                       ".ColorPicks_history.txt");
        private readonly ListBox _historyList = new();
        private readonly FlowLayoutPanel _historySwatchPanel = new() { AutoScroll = true };
        private readonly List<Color> _historySwatches = new();

        // ── image + magnifier ─────────────────────────────────────────────────
        private Bitmap? _loadedImage = null;
        private Image? _displayedImage = null;
        private double _imageScale = 1.0;
        private readonly PictureBox _imageBox = new();
        private readonly ImageMagnifierPanel _magnifier = new();
        private readonly TrackBar _zoomSlider = new() { Minimum = 2, Maximum = 40, Value = 12 };

        // ═════════════════════════════════════════════════════════════════════
        public ColorPicksForm()
        {
            Text = "ColorPicks";
            Size = new Size(1150, 760);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            ApplyColor(Color.White);
            LoadHistory();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI construction
        // ─────────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Root: 3-column, 2-row table
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(8)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            root.Controls.Add(BuildToolsPanel(), 0, 0);
            root.Controls.Add(BuildCentrePanel(), 1, 0);
            root.Controls.Add(BuildHistoryPanel(), 2, 0);

            var bottom = BuildBottomBar();
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 3);

            Controls.Add(root);
        }

        // ── LEFT: Tools ──────────────────────────────────────────────────────
        private Control BuildToolsPanel()
        {
            var gb = Titled("Tools");
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4)
            };

            // Upload / Color Picker buttons
            var uploadBtn = Btn("Upload Image", _ => UploadImage());
            var pickerBtn = Btn("Color Picker", _ => OpenColorPicker());

            // Gradient controls
            var gradLabel = new Label
            {
                Text = "Gradient Generator",
                AutoSize = true,
                Margin = new Padding(4, 10, 4, 2)
            };
            var gradRow = new FlowLayoutPanel { AutoSize = true };
            gradRow.Controls.Add(Btn("Pick Start", _ =>
            {
                using var cd = new ColorDialog { Color = _startColor };
                if (cd.ShowDialog() != DialogResult.OK) return;
                _startColor = cd.Color;
                UpdateFromColor(cd.Color);
                _gradientPreview.StartColor = cd.Color;
            }));
            gradRow.Controls.Add(Btn("Pick End", _ =>
            {
                using var cd = new ColorDialog { Color = _endColor };
                if (cd.ShowDialog() != DialogResult.OK) return;
                _endColor = cd.Color;
                UpdateFromColor(cd.Color);
                _gradientPreview.EndColor = cd.Color;
            }));
            gradRow.Controls.Add(Btn("Copy Gradient CSS", _ => CopyToClipboard(GradientCss())));

            // Current Color sub-panel
            var currentGb = Titled("Current Color");
            currentGb.Width = 210;
            currentGb.Height = 220;
            currentGb.Margin = new Padding(4, 10, 4, 4);

            _currentSwatchBox = new Panel
            {
                Width = 170,
                Height = 70,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4)
            };

            var readouts = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };
            readouts.Controls.Add(LabeledField("HEX:", _hexField));
            readouts.Controls.Add(LabeledField("RGB:", _rgbField));
            readouts.Controls.Add(LabeledField("HSL:", _hslField));

            _copyHexBtn = Btn("Copy HEX", _ => CopyToClipboard(_hexField.Text));
            _copyRgbBtn = Btn("Copy RGB", _ => CopyToClipboard(_rgbField.Text));
            var copyRow = new FlowLayoutPanel { AutoSize = true };
            copyRow.Controls.Add(_copyHexBtn);
            copyRow.Controls.Add(_copyRgbBtn);

            _fileInfoLabel = new Label { AutoSize = true, Margin = new Padding(4) };

            var currentFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };
            currentFlow.Controls.Add(_currentSwatchBox);
            currentFlow.Controls.Add(readouts);
            currentFlow.Controls.Add(copyRow);
            currentFlow.Controls.Add(_fileInfoLabel);
            currentGb.Controls.Add(currentFlow);

            content.Controls.Add(uploadBtn);
            content.Controls.Add(pickerBtn);
            content.Controls.Add(gradLabel);
            content.Controls.Add(gradRow);
            content.Controls.Add(currentGb);

            gb.Controls.Add(content);
            return gb;
        }

        // ── CENTRE: Image + Magnifier ─────────────────────────────────────────
        private Control BuildCentrePanel()
        {
            var gb = Titled("Image");
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));

            // ── image picturebox ──
            _imageBox.Dock = DockStyle.Fill;
            _imageBox.SizeMode = PictureBoxSizeMode.CenterImage;
            _imageBox.BackColor = Color.LightGray;
            _imageBox.MouseMove += ImageBox_MouseMove;
            _imageBox.MouseClick += ImageBox_MouseClick;

            layout.Controls.Add(_imageBox, 0, 0);

            // ── magnifier ──
            var magGb = Titled("Magnifier");
            var magLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            magLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            magLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            _magnifier.Dock = DockStyle.Fill;

            _zoomSlider.Dock = DockStyle.Fill;
            _zoomSlider.TickFrequency = 5;
            _zoomSlider.ValueChanged += (_, _) =>
            {
                _magnifier.SetZoom(_zoomSlider.Value);
                _magnifier.Invalidate();
            };

            var zoomRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            zoomRow.Controls.Add(new Label { Text = "Zoom:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            zoomRow.Controls.Add(_zoomSlider);

            magLayout.Controls.Add(_magnifier, 0, 0);
            magLayout.Controls.Add(zoomRow, 0, 1);
            magGb.Controls.Add(magLayout);
            layout.Controls.Add(magGb, 0, 1);

            gb.Controls.Add(layout);
            return gb;
        }

        // ── RIGHT: History + Gradient preview ────────────────────────────────
        private Control BuildHistoryPanel()
        {
            var gb = Titled("History");
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));

            _historySwatchPanel.Dock = DockStyle.Fill;
            _historySwatchPanel.BorderStyle = BorderStyle.FixedSingle;
            _historySwatchPanel.Padding = new Padding(2);

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            btnRow.Controls.Add(Btn("Save History", _ => SaveHistory()));
            btnRow.Controls.Add(Btn("Load History", _ => LoadHistory()));
            btnRow.Controls.Add(Btn("Clear", _ => ClearHistory()));

            _gradientPreview.Dock = DockStyle.Fill;
            _gradientPreview.StartColor = _startColor;
            _gradientPreview.EndColor = _endColor;
            var gradGb = Titled("Gradient Preview");
            gradGb.Controls.Add(_gradientPreview);

            layout.Controls.Add(_historySwatchPanel, 0, 0);
            layout.Controls.Add(btnRow, 0, 1);
            layout.Controls.Add(gradGb, 0, 2);
            gb.Controls.Add(layout);
            return gb;
        }

        // ── BOTTOM: export buttons ────────────────────────────────────────────
        private Control BuildBottomBar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            bar.Controls.Add(Btn("Copy Color CSS", _ => CopyToClipboard(ColorCss(_hexField.Text))));
            bar.Controls.Add(Btn("Export Gradient PNG", _ => ExportGradientPng()));
            return bar;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Image events
        // ─────────────────────────────────────────────────────────────────────

        private void ImageBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_loadedImage == null || _displayedImage == null) return;

            if (!TryImageCoord(e.X, e.Y, out int origX, out int origY)) return;

            _magnifier.SetCenter(origX, origY, _loadedImage);
            _magnifier.Invalidate();
            UpdateReadoutsOnly(origX, origY);
        }

        private void ImageBox_MouseClick(object? sender, MouseEventArgs e)
        {
            if (_loadedImage == null || _displayedImage == null) return;

            if (!TryImageCoord(e.X, e.Y, out int origX, out int origY)) return;

            Color c = _loadedImage.GetPixel(origX, origY);
            ApplyColor(c);
            AddHistorySwatch(c);
        }

        /// <summary>
        /// Converts a PictureBox client coordinate to the corresponding original-image pixel.
        /// Returns false when the pointer is outside the displayed image region.
        /// </summary>
        private bool TryImageCoord(int clientX, int clientY, out int origX, out int origY)
        {
            origX = origY = 0;

            int imgW = (int)Math.Round(_loadedImage!.Width * _imageScale);
            int imgH = (int)Math.Round(_loadedImage!.Height * _imageScale);

            int offsetX = (_imageBox.Width - imgW) / 2;
            int offsetY = (_imageBox.Height - imgH) / 2;

            int px = clientX - offsetX;
            int py = clientY - offsetY;

            if (px < 0 || py < 0 || px >= imgW || py >= imgH) return false;

            origX = Math.Max(0, Math.Min((int)Math.Round(px / _imageScale), _loadedImage.Width - 1));
            origY = Math.Max(0, Math.Min((int)Math.Round(py / _imageScale), _loadedImage.Height - 1));
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Upload image
        // ─────────────────────────────────────────────────────────────────────

        private void UploadImage()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tif|All Files|*.*"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var img = new Bitmap(ofd.FileName);
                _loadedImage = img;

                // Scale to fit PictureBox (never upscale beyond 1×)
                int maxW = Math.Max(50, _imageBox.Width);
                int maxH = Math.Max(50, _imageBox.Height);
                double sx = (double)maxW / img.Width;
                double sy = (double)maxH / img.Height;
                _imageScale = Math.Min(1.0, Math.Min(sx, sy));

                int w = Math.Max(1, (int)Math.Round(img.Width * _imageScale));
                int h = Math.Max(1, (int)Math.Round(img.Height * _imageScale));

                // High-quality downscale
                var scaled = new Bitmap(w, h);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, 0, 0, w, h);
                }
                _displayedImage = scaled;
                _imageBox.Image = scaled;

                var fi = new FileInfo(ofd.FileName);
                _fileInfoLabel.Text = $"Image: {img.Width}×{img.Height}  |  {fi.Length / 1024.0:0.0} KB";

                _magnifier.SetCenter(0, 0, _loadedImage);
                _magnifier.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Color readout helpers
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateFromColor(Color c)
        {
            _hexField.Text = ColorUtils.ColorToHex(c);
            _rgbField.Text = $"rgb({c.R}, {c.G}, {c.B})";
            _hslField.Text = ColorUtils.RgbToHslString(c);
            _currentSwatchBox.BackColor = c;
        }

        private void UpdateReadoutsOnly(int x, int y)
        {
            if (_loadedImage == null) return;
            Color c = _loadedImage.GetPixel(x, y);
            _hexField.Text = ColorUtils.ColorToHex(c);
            _rgbField.Text = $"rgb({c.R}, {c.G}, {c.B})";
            _hslField.Text = ColorUtils.RgbToHslString(c);
        }

        private void ApplyColor(Color c)
        {
            UpdateFromColor(c);
            string hex = ColorUtils.ColorToHex(c);
            if (!HistoryContains(hex)) _historyList.Items.Add(hex);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  History
        // ─────────────────────────────────────────────────────────────────────

        private bool HistoryContains(string hex)
        {
            foreach (var item in _historyList.Items)
                if (string.Equals(item?.ToString(), hex, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private void AddHistorySwatch(Color c)
        {
            string hex = ColorUtils.ColorToHex(c);
            foreach (var ec in _historySwatches)
                if (string.Equals(ColorUtils.ColorToHex(ec), hex, StringComparison.OrdinalIgnoreCase))
                    return;

            _historySwatches.Insert(0, c);

            var swatch = new Panel
            {
                Width = 34,
                Height = 34,
                BackColor = c,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(3),
                Cursor = Cursors.Hand
            };
            var tip = new ToolTip();
            tip.SetToolTip(swatch, hex);

            swatch.Click += (_, _) =>
            {
                ApplyColor(c);
                CopyToClipboard(hex);

                // Flash border as visual feedback
                swatch.BorderStyle = BorderStyle.None;
                var timer = new System.Windows.Forms.Timer { Interval = 180 };
                timer.Tick += (_, _) =>
                {
                    swatch.BorderStyle = BorderStyle.FixedSingle;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            };

            // Newest first
            _historySwatchPanel.Controls.Add(swatch);
            _historySwatchPanel.Controls.SetChildIndex(swatch, 0);

            if (!HistoryContains(hex)) _historyList.Items.Add(hex);
        }

        private void SaveHistory()
        {
            try
            {
                HistoryManager.SaveHistory(_historyFile, _historyList.Items);
                MessageBox.Show($"History saved to:\n{_historyFile}", "Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadHistory()
        {
            _historyList.Items.Clear();
            _historySwatches.Clear();
            _historySwatchPanel.Controls.Clear();

            try
            {
                var lines = HistoryManager.LoadHistory(_historyFile);
                foreach (var line in lines)
                {
                    string hex = line.Trim();
                    if (string.IsNullOrWhiteSpace(hex)) continue;
                    _historyList.Items.Add(hex);
                    try { AddHistorySwatch(ColorUtils.HexToColor(hex)); } catch { /* ignore bad entries */ }
                }
            }
            catch { /* silent on missing file */ }
        }

        private void ClearHistory()
        {
            _historyList.Items.Clear();
            _historySwatches.Clear();
            _historySwatchPanel.Controls.Clear();
            try { if (File.Exists(_historyFile)) File.Delete(_historyFile); } catch { }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Color picker dialog
        // ─────────────────────────────────────────────────────────────────────

        private void OpenColorPicker()
        {
            Color initial = _currentSwatchBox.BackColor == Color.Empty
                ? Color.White
                : _currentSwatchBox.BackColor;

            using var cd = new ColorDialog { Color = initial, FullOpen = true };
            if (cd.ShowDialog() != DialogResult.OK) return;

            ApplyColor(cd.Color);
            AddHistorySwatch(cd.Color);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CSS helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string ColorCss(string hex) =>
            string.IsNullOrWhiteSpace(hex) ? "" : $"--picked-color: {hex};";

        private string GradientCss()
        {
            string s = ColorUtils.ColorToHex(_startColor);
            string e = ColorUtils.ColorToHex(_endColor);
            return $"background: linear-gradient(90deg, {s}, {e});";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Export gradient PNG
        // ─────────────────────────────────────────────────────────────────────

        private void ExportGradientPng()
        {
            using var sfd = new SaveFileDialog
            {
                FileName = "gradient.png",
                Filter = "PNG Image|*.png"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            const int w = 800, h = 200;
            using var bmp = new Bitmap(w, h);
            using (var g2 = Graphics.FromImage(bmp))
            using (var brush = new LinearGradientBrush(
                       new Point(0, 0), new Point(w, 0), _startColor, _endColor))
            {
                g2.FillRectangle(brush, 0, 0, w, h);
            }

            try
            {
                bmp.Save(sfd.FileName, ImageFormat.Png);
                MessageBox.Show($"Exported to:\n{sfd.FileName}", "Exported",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Clipboard
        // ─────────────────────────────────────────────────────────────────────

        private void CopyToClipboard(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Clipboard.SetText(text);
            MessageBox.Show($"Copied: {text}", "Copied",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layout helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GroupBox Titled(string title) => new()
        {
            Text = title,
            Dock = DockStyle.Fill
        };

        private static Button Btn(string text, EventHandler handler)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
            b.Click += handler;
            return b;
        }

        private static FlowLayoutPanel LabeledField(string label, Control field)
        {
            var row = new FlowLayoutPanel { AutoSize = true };
            row.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            });
            row.Controls.Add(field);
            return row;
        }
    }
}
