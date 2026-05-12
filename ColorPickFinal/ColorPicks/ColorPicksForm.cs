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
        private readonly TextBox _hexField  = new() { Width = 90,  ReadOnly = true };
        private readonly TextBox _rgbField  = new() { Width = 165, ReadOnly = true };
        private readonly TextBox _hslField  = new() { Width = 200, ReadOnly = true };

        // ── current swatch + copy buttons ────────────────────────────────────
        private Panel   _currentSwatchBox = null!;
        private Button  _copyHexBtn       = null!;
        private Button  _copyRgbBtn       = null!;
        private Label   _fileInfoLabel    = null!;

        // ── gradient ─────────────────────────────────────────────────────────
        private Color         _startColor     = Color.White;
        private Color         _endColor       = Color.Black;
        private GradientPanel _gradientPreview = new();

        // ── history ───────────────────────────────────────────────────────────
        private readonly string             _historyFile         = Path.Combine(
                                                                       Environment.GetFolderPath(
                                                                           Environment.SpecialFolder.UserProfile),
                                                                       ".ColorPicks_history.txt");
        private readonly ListBox            _historyList         = new();
        private readonly FlowLayoutPanel    _historySwatchPanel  = new() { AutoScroll = true };
        private readonly List<Color>        _historySwatches     = new();

        // ── image + magnifier ─────────────────────────────────────────────────
        private Bitmap?              _loadedImage    = null;
        private Image?               _displayedImage = null;
        private double               _imageScale     = 1.0;
        private readonly PictureBox  _imageBox       = new();
        private readonly ImageMagnifierPanel _magnifier = new();
        private readonly TrackBar    _zoomSlider     = new() { Minimum = 2, Maximum = 40, Value = 12 };

        // ═════════════════════════════════════════════════════════════════════
        public ColorPicksForm()
        {
            Text            = "ColorPicks";
            Size            = new Size(1150, 760);
            MinimumSize     = new Size(900, 600);
            StartPosition   = FormStartPosition.CenterScreen;

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
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 2,
                Padding     = new Padding(8)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,  42));

            root.Controls.Add(BuildToolsPanel(),   0, 0);
            root.Controls.Add(BuildCentrePanel(),  1, 0);
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

            // Outer table: rows lock heights so nothing can shift or wrap
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 6,
                Padding     = new Padding(6)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  34)); // Upload btn
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  34)); // Color Picker btn
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  24)); // "Gradient Generator" label
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  90)); // gradient buttons (3 rows)
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent,  100)); // Current Color (fills rest)
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  22)); // file info label

            // ── row 0: Upload ──
            var uploadBtn = new Button { Text = "Upload Image", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
            uploadBtn.Click += (_, _) => UploadImage();
            tbl.Controls.Add(uploadBtn, 0, 0);

            // ── row 1: Color Picker ──
            var pickerBtn = new Button { Text = "Color Picker", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
            pickerBtn.Click += (_, _) => OpenColorPicker();
            tbl.Controls.Add(pickerBtn, 0, 1);

            // ── row 2: Gradient label ──
            tbl.Controls.Add(new Label
            {
                Text      = "Gradient Generator",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font      = new Font(Font, FontStyle.Bold)
            }, 0, 2);

            // ── row 3: gradient buttons stacked ──
            var gradTbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                Margin      = Padding.Empty
            };
            gradTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gradTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gradTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            gradTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var pickStartBtn = new Button { Text = "Pick Start", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 2, 2) };
            pickStartBtn.Click += (_, _) =>
            {
                using var cd = new ColorDialog { Color = _startColor };
                if (cd.ShowDialog() != DialogResult.OK) return;
                _startColor = cd.Color;
                UpdateFromColor(cd.Color);
                _gradientPreview.StartColor = cd.Color;
            };
            var pickEndBtn = new Button { Text = "Pick End", Dock = DockStyle.Fill, Margin = new Padding(2, 0, 0, 2) };
            pickEndBtn.Click += (_, _) =>
            {
                using var cd = new ColorDialog { Color = _endColor };
                if (cd.ShowDialog() != DialogResult.OK) return;
                _endColor = cd.Color;
                UpdateFromColor(cd.Color);
                _gradientPreview.EndColor = cd.Color;
            };
            var copyGradBtn = new Button { Text = "Copy Gradient CSS", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0) };
            copyGradBtn.Click += (_, _) => CopyToClipboard(GradientCss());

            gradTbl.Controls.Add(pickStartBtn,  0, 0);
            gradTbl.Controls.Add(pickEndBtn,    1, 0);
            gradTbl.Controls.Add(copyGradBtn,   0, 1);
            gradTbl.SetColumnSpan(copyGradBtn, 2);
            tbl.Controls.Add(gradTbl, 0, 3);

            // ── row 4: Current Color group ──
            var currentGb = Titled("Current Color");

            var currentTbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 6,
                Padding     = new Padding(4)
            };
            currentTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  70)); // swatch
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  26)); // HEX
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  26)); // RGB
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  26)); // HSL
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  30)); // copy buttons
            currentTbl.RowStyles.Add(new RowStyle(SizeType.Percent,  100)); // (spacer)

            _currentSwatchBox = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin      = new Padding(0, 0, 0, 4)
            };
            currentTbl.Controls.Add(_currentSwatchBox, 0, 0);

            // HEX / RGB / HSL rows — each is a 2-col mini table so label+field stay aligned
            currentTbl.Controls.Add(FieldRow("HEX:", _hexField),  0, 1);
            currentTbl.Controls.Add(FieldRow("RGB:", _rgbField),  0, 2);
            currentTbl.Controls.Add(FieldRow("HSL:", _hslField),  0, 3);

            _copyHexBtn = new Button { Text = "Copy HEX", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 2, 0) };
            _copyRgbBtn = new Button { Text = "Copy RGB", Dock = DockStyle.Fill, Margin = new Padding(2, 0, 0, 0) };
            _copyHexBtn.Click += (_, _) => CopyToClipboard(_hexField.Text);
            _copyRgbBtn.Click += (_, _) => CopyToClipboard(_rgbField.Text);

            var copyRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            copyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            copyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            copyRow.Controls.Add(_copyHexBtn, 0, 0);
            copyRow.Controls.Add(_copyRgbBtn, 1, 0);
            currentTbl.Controls.Add(copyRow, 0, 4);

            currentGb.Controls.Add(currentTbl);
            tbl.Controls.Add(currentGb, 0, 4);

            // ── row 5: file info ──
            _fileInfoLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font.FontFamily, 7.5f) };
            tbl.Controls.Add(_fileInfoLabel, 0, 5);

            gb.Controls.Add(tbl);
            return gb;
        }

        // Two-column row: fixed label width + textbox fills rest
        private static TableLayoutPanel FieldRow(string label, Control field)
        {
            var row = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                Margin      = new Padding(0, 0, 0, 2)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            field.Dock = DockStyle.Fill;
            row.Controls.Add(field, 1, 0);
            return row;
        }

        // ── CENTRE: Image + Magnifier ─────────────────────────────────────────
        private Control BuildCentrePanel()
        {
            var gb     = Titled("Image");
            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));

            // ── image picturebox ──
            _imageBox.Dock      = DockStyle.Fill;
            _imageBox.SizeMode  = PictureBoxSizeMode.CenterImage;
            _imageBox.BackColor = Color.LightGray;
            _imageBox.MouseMove  += ImageBox_MouseMove;
            _imageBox.MouseClick += ImageBox_MouseClick;

            layout.Controls.Add(_imageBox, 0, 0);

            // ── magnifier ──
            var magGb     = Titled("Magnifier");
            var magLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 2,
                ColumnCount = 1
            };
            magLayout.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
            magLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,  32));

            _magnifier.Dock = DockStyle.Fill;

            _zoomSlider.Dock           = DockStyle.Fill;
            _zoomSlider.TickFrequency  = 5;
            _zoomSlider.ValueChanged  += (_, _) =>
            {
                _magnifier.SetZoom(_zoomSlider.Value);
                _magnifier.Invalidate();
            };

            var zoomRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            zoomRow.Controls.Add(new Label { Text = "Zoom:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            zoomRow.Controls.Add(_zoomSlider);

            magLayout.Controls.Add(_magnifier, 0, 0);
            magLayout.Controls.Add(zoomRow,    0, 1);
            magGb.Controls.Add(magLayout);
            layout.Controls.Add(magGb, 0, 1);

            gb.Controls.Add(layout);
            return gb;
        }

        // ── RIGHT: History + Gradient preview ────────────────────────────────
        private Control BuildHistoryPanel()
        {
            var gb     = Titled("History");
            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,  38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));

            _historySwatchPanel.Dock        = DockStyle.Fill;
            _historySwatchPanel.BorderStyle = BorderStyle.FixedSingle;
            _historySwatchPanel.Padding     = new Padding(2);

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            btnRow.Controls.Add(Btn("Save History", SaveHistory));
            btnRow.Controls.Add(Btn("Load History", LoadHistory));
            btnRow.Controls.Add(Btn("Clear",        ClearHistory));

            _gradientPreview.Dock       = DockStyle.Fill;
            _gradientPreview.StartColor = _startColor;
            _gradientPreview.EndColor   = _endColor;
            var gradGb = Titled("Gradient Preview");
            gradGb.Controls.Add(_gradientPreview);

            layout.Controls.Add(_historySwatchPanel, 0, 0);
            layout.Controls.Add(btnRow,              0, 1);
            layout.Controls.Add(gradGb,              0, 2);
            gb.Controls.Add(layout);
            return gb;
        }

        // ── BOTTOM: export buttons ────────────────────────────────────────────
        private Control BuildBottomBar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            bar.Controls.Add(Btn("Copy Color CSS",      () => CopyToClipboard(ColorCss(_hexField.Text))));
            bar.Controls.Add(Btn("Export Gradient PNG", ExportGradientPng));
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

            int imgW = (int)Math.Round(_loadedImage!.Width  * _imageScale);
            int imgH = (int)Math.Round(_loadedImage!.Height * _imageScale);

            int offsetX = (_imageBox.Width  - imgW) / 2;
            int offsetY = (_imageBox.Height - imgH) / 2;

            int px = clientX - offsetX;
            int py = clientY - offsetY;

            if (px < 0 || py < 0 || px >= imgW || py >= imgH) return false;

            origX = Math.Max(0, Math.Min((int)Math.Round(px / _imageScale), _loadedImage.Width  - 1));
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
                Title  = "Open Image",
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

                int w = Math.Max(1, (int)Math.Round(img.Width  * _imageScale));
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
            _hexField.Text  = ColorUtils.ColorToHex(c);
            _rgbField.Text  = $"rgb({c.R}, {c.G}, {c.B})";
            _hslField.Text  = ColorUtils.RgbToHslString(c);
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
            if (!HistoryContains(hex))
            {
     d           _historyList.Items.Add(hex);
                DatabaseHelper.SaveColor(_hexField.Text, _rgbField.Text, _hslField.Text);
            }
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
                Width       = 34,
                Height      = 34,
                BackColor   = c,
                BorderStyle = BorderStyle.FixedSingle,
                Margin      = new Padding(3),
                Cursor      = Cursors.Hand
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
                Filter   = "PNG Image|*.png"
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

        private static Button Btn(string text, Action handler)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
            b.Click += (_, _) => handler();
            return b;
        }


    }
}
