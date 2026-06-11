using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Explorador_de_Archivo.Forms
{
    public class ChartForm : Form
    {
        private DataTable _tabla = new DataTable();
        private DataGridView _grid = null!;
        private Panel _canvas = null!;
        private Label _lblResumen = null!;
        private Label _lblDesc = null!;
        private Label _status = null!;
        private ComboBox _cmbTipo = null!;
        private ComboBox _cmbColumna = null!;
        private ComboBox _cmbEjeX = null!;

        // Datos procesados para dibujar
        private string[] _labels = new string[0];
        private double[] _values = new double[0];
        private string _tipo = "Barras";
        private string _titulo = "";

        private static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(0,120,212),  Color.FromArgb(220,60,60),
            Color.FromArgb(40,180,80),  Color.FromArgb(255,165,0),
            Color.FromArgb(130,80,200), Color.FromArgb(0,180,200),
            Color.FromArgb(255,100,150),Color.FromArgb(100,200,100),
        };

        public ChartForm(string? path = null)
        {
            Theme.ApplyForm(this);
            Text = "Graficador de Datos";
            Size = new Size(1100, 700);
            MinimumSize = new Size(800, 500);
            Build();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                LoadFile(path);
        }

        private void Build()
        {
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Theme.BgPanel,
                Padding = new Padding(8, 8, 0, 0)
            };

            Button Btn(string t, int w, Action a, bool accent = false)
            { var b = accent ? Theme.AccentBtn(t, w, 30) : Theme.FlatBtn(t, w, 30); b.Click += (_, _) => a(); return b; }

            Label Lbl(string t) => new Label
            { Text = t, ForeColor = Theme.TxtSub, Font = Theme.FontSmall, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

            ComboBox Cmb(string[] items, int w)
            {
                var c = new ComboBox
                {
                    Width = w,
                    BackColor = Theme.BgControl,
                    ForeColor = Theme.TxtMain,
                    Font = Theme.FontNormal,
                    FlatStyle = FlatStyle.Flat,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                if (items.Length > 0) { c.Items.AddRange(items); c.SelectedIndex = 0; }
                return c;
            }

            top.Controls.Add(Btn("📂 Abrir", 100, OpenFile, true));
            top.Controls.Add(Btn("💾 PNG", 80, ExportPng));
            top.Controls.Add(Lbl("Tipo:"));
            _cmbTipo = Cmb(new[] { "Barras", "Líneas", "Área", "Pastel", "Dispersión" }, 110);
            _cmbTipo.SelectedIndexChanged += (_, _) => Graficar();
            top.Controls.Add(_cmbTipo);
            top.Controls.Add(Lbl(" Eje X:"));
            _cmbEjeX = Cmb(new string[0], 120); top.Controls.Add(_cmbEjeX);
            top.Controls.Add(Lbl(" Valores:"));
            _cmbColumna = Cmb(new string[0], 120); top.Controls.Add(_cmbColumna);
            top.Controls.Add(Btn("📊 Graficar", 100, Graficar, true));
            _status = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true };
            top.Controls.Add(_status);

            var split = new SplitContainer
            { Dock = DockStyle.Fill, SplitterWidth = 4, BackColor = Theme.Border, SplitterDistance = 340 };

            _grid = Theme.Grid();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = true;
            _grid.AllowUserToDeleteRows = true;
            _grid.ReadOnly = false;
            split.Panel1.Controls.Add(_grid);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 18) };

            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 22, 22) };
            _canvas.Paint += OnPaint;
            _canvas.Resize += (_, _) => _canvas.Invalidate();

            _lblResumen = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Theme.TxtSub,
                Font = Theme.FontMono,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Text = "Carga un archivo para ver el resumen"
            };
            _lblDesc = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Theme.TxtMuted,
                Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            right.Controls.AddRange(new Control[] { _canvas, _lblDesc, _lblResumen });
            split.Panel2.Controls.Add(right);
            Controls.AddRange(new Control[] { split, top });
        }

        // ── Carga ─────────────────────────────────────────────────────
        private void OpenFile()
        {
            var path = FilePicker.Open(filter: ".csv,.json,.txt,.xlsx,.xls");
            if (path != null) LoadFile(path);
        }

        private void LoadFile(string path)
        {
            try
            {
                _tabla = Path.GetExtension(path).ToLower() == ".json" ? LeerJson(path) : LeerCsv(path);
                _grid.DataSource = null; _grid.DataSource = _tabla;
                RefreshCombos();
                SetStatus("✓ " + Path.GetFileName(path) + "  —  " + _tabla.Rows.Count + " filas");
                Text = "Graficador — " + Path.GetFileName(path);
                Graficar();
            }
            catch (Exception ex) { SetStatus("✗ " + ex.Message); }
        }

        private static DataTable LeerCsv(string path)
        {
            var dt = new DataTable();
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) return dt;
            char sep = lines[0].Contains(';') ? ';' : ',';
            foreach (var h in lines[0].Split(sep)) dt.Columns.Add(h.Trim().Trim('"'));
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var v = lines[i].Split(sep); var row = dt.NewRow();
                for (int j = 0; j < dt.Columns.Count; j++)
                    row[j] = j < v.Length ? v[j].Trim().Trim('"') : (object)DBNull.Value;
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static DataTable LeerJson(string path)
        {
            var dt = new DataTable();
            var tok = JToken.Parse(File.ReadAllText(path));
            JArray arr;
            if (tok is JArray ja) arr = ja;
            else if (tok is JObject jo)
                arr = new JArray(jo.Properties().Select(p => (JToken)new JObject(
                    new JProperty("clave", p.Name), new JProperty("valor", p.Value))));
            else return dt;
            if (arr.Count == 0) return dt;
            var first = arr[0] as JObject; if (first == null) return dt;
            foreach (var prop in first.Properties()) dt.Columns.Add(prop.Name);
            foreach (JToken item in arr)
            {
                if (!(item is JObject obj)) continue;
                var row = dt.NewRow();
                foreach (DataColumn col in dt.Columns)
                    row[col.ColumnName] = obj[col.ColumnName]?.ToString() ?? "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private void RefreshCombos()
        {
            _cmbColumna.Items.Clear(); _cmbEjeX.Items.Clear();
            _cmbEjeX.Items.Add("(índice)");
            foreach (DataColumn col in _tabla.Columns)
            { _cmbColumna.Items.Add(col.ColumnName); _cmbEjeX.Items.Add(col.ColumnName); }
            _cmbEjeX.SelectedIndex = _tabla.Columns.Count > 0 ? 1 : 0;
            int n = -1;
            for (int i = 0; i < _tabla.Columns.Count; i++)
            {
                var s = _tabla.Rows.Count > 0 ? _tabla.Rows[0][i]?.ToString()?.Replace(",", ".") : "";
                if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)) { n = i; break; }
            }
            _cmbColumna.SelectedIndex = n >= 0 ? n : (_tabla.Columns.Count > 0 ? 0 : -1);
        }

        private void Graficar()
        {
            if (_tabla.Rows.Count == 0 || _cmbColumna.SelectedItem == null) return;
            string colVal = _cmbColumna.SelectedItem.ToString();
            bool useX = _cmbEjeX.SelectedIndex > 0;
            string colX = useX ? _cmbEjeX.SelectedItem.ToString() : "";
            _tipo = _cmbTipo.SelectedItem?.ToString() ?? "Barras";
            _titulo = colVal;

            var datos = _tabla.AsEnumerable()
                .Where(r => double.TryParse(r[colVal]?.ToString()?.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                .GroupBy(r => useX ? r[colX]?.ToString() ?? "?" : _tabla.Rows.IndexOf(r).ToString())
                .Select(g => new
                {
                    Cat = g.Key,
                    Tot = g.Sum(r => double.Parse(r[colVal].ToString().Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture))
                })
                .OrderByDescending(x => x.Tot).Take(20).ToList();

            if (datos.Count == 0) { SetStatus("Sin valores numéricos"); return; }

            _labels = datos.Select(d => d.Cat).ToArray();
            _values = datos.Select(d => d.Tot).ToArray();

            _lblResumen.Text = string.Format(
                "  Suma: {0:N2}  |  Promedio: {1:N2}  |  Máx: {2:N2}  |  Mín: {3:N2}  |  N: {4}",
                _values.Sum(), _values.Average(), _values.Max(), _values.Min(), _values.Length);

            _lblDesc.Text = _tipo == "Barras" ? "  Barras: compara categorías." :
                            _tipo == "Líneas" ? "  Líneas: muestra tendencias." :
                            _tipo == "Área" ? "  Área: magnitud acumulada." :
                            _tipo == "Pastel" ? "  Pastel: proporciones del total." :
                                            "  Dispersión: distribución de valores.";

            SetStatus("  " + datos.Count + " categorías  —  " + _tipo);
            _canvas.Invalidate();
        }

        // ── Dibujo con GDI+ ───────────────────────────────────────────
        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_values.Length == 0)
            {
                using var fBig = new Font("Segoe UI", 13f);
                string msg = "Abre un CSV o JSON y presiona  📊 Graficar";
                var sz = g.MeasureString(msg, fBig);
                g.DrawString(msg, fBig, new SolidBrush(Color.FromArgb(80, 80, 80)),
                    (_canvas.Width - sz.Width) / 2f, (_canvas.Height - sz.Height) / 2f);
                return;
            }

            int ml = 60, mr = 20, mt = 40, mb = 60;
            var area = new Rectangle(ml, mt, _canvas.Width - ml - mr, _canvas.Height - mt - mb);
            if (area.Width < 10 || area.Height < 10) return;

            DrawGrid(g, area);
            DrawTitle(g, area);

            if (_tipo == "Barras") DrawBars(g, area);
            else if (_tipo == "Líneas") DrawLines(g, area, false);
            else if (_tipo == "Área") DrawLines(g, area, true);
            else if (_tipo == "Pastel") DrawPie(g, area);
            else if (_tipo == "Dispersión") DrawScatter(g, area);
        }

        private void DrawGrid(Graphics g, Rectangle area)
        {
            if (_tipo == "Pastel") return;
            double mn = _values.Min(), mx = _values.Max();
            double range = mx - mn; if (range == 0) range = 1;
            using var gridPen = new Pen(Color.FromArgb(45, 45, 45));
            using var fSmall = new Font("Segoe UI", 7.5f);
            using var brMuted = new SolidBrush(Color.FromArgb(100, 100, 100));
            int ticks = 5;
            for (int i = 0; i <= ticks; i++)
            {
                double val = mn + range * (i / (double)ticks);
                int y = area.Bottom - (int)(area.Height * (i / (double)ticks));
                g.DrawLine(gridPen, area.Left, y, area.Right, y);
                string lbl = val >= 1000 ? string.Format("{0:N0}", val) : string.Format("{0:N1}", val);
                g.DrawString(lbl, fSmall, brMuted, 2, y - 8);
            }
            // Etiquetas eje X
            int step = (int)Math.Ceiling(_labels.Length / 10.0);
            using var fX = new Font("Segoe UI", 7.5f);
            for (int i = 0; i < _labels.Length; i += Math.Max(1, step))
            {
                float x = area.Left + area.Width * (i + 0.5f) / _labels.Length;
                string lbl = _labels[i].Length > 10 ? _labels[i].Substring(0, 9) + "…" : _labels[i];
                g.DrawString(lbl, fX, brMuted, x - 18, area.Bottom + 4);
            }
        }

        private void DrawTitle(Graphics g, Rectangle area)
        {
            using var f = new Font("Segoe UI", 11f, FontStyle.Bold);
            var sz = g.MeasureString(_titulo, f);
            g.DrawString(_titulo, f, new SolidBrush(Color.White),
                area.Left + (area.Width - sz.Width) / 2f, 8);
        }

        private int ValY(double v, Rectangle area)
        {
            double mn = _values.Min(), mx = _values.Max();
            double range = mx - mn; if (range == 0) range = 1;
            return area.Bottom - (int)(area.Height * (v - mn) / range);
        }

        private void DrawBars(Graphics g, Rectangle area)
        {
            int n = _values.Length;
            float gw = area.Width / (float)n;
            float bw = gw * 0.65f;
            for (int i = 0; i < n; i++)
            {
                int y = ValY(_values[i], area);
                var rect = new RectangleF(area.Left + i * gw + (gw - bw) / 2f, y, bw, area.Bottom - y);
                using var br = new SolidBrush(Color.FromArgb(200, Palette[i % Palette.Length]));
                g.FillRectangle(br, rect);
                using var pen = new Pen(Color.FromArgb(255, Palette[i % Palette.Length]), 1);
                g.DrawRectangle(pen, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            }
        }

        private void DrawLines(Graphics g, Rectangle area, bool fill)
        {
            if (_values.Length < 2) return;
            var pts = new PointF[_values.Length];
            for (int i = 0; i < _values.Length; i++)
                pts[i] = new PointF(area.Left + area.Width * (i + 0.5f) / _values.Length, ValY(_values[i], area));

            if (fill)
            {
                var poly = new List<PointF>(pts);
                poly.Add(new PointF(pts[pts.Length - 1].X, area.Bottom));
                poly.Add(new PointF(pts[0].X, area.Bottom));
                using var br = new SolidBrush(Color.FromArgb(70, 0, 120, 212));
                g.FillPolygon(br, poly.ToArray());
            }
            using var pen = new Pen(Palette[0], 2.5f);
            g.DrawLines(pen, pts);
            foreach (var p in pts)
            {
                using var br = new SolidBrush(Palette[0]);
                g.FillEllipse(br, p.X - 4, p.Y - 4, 8, 8);
            }
        }

        private void DrawScatter(Graphics g, Rectangle area)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                float x = area.Left + area.Width * (i + 0.5f) / _values.Length;
                int y = ValY(_values[i], area);
                using var br = new SolidBrush(Color.FromArgb(200, Palette[i % Palette.Length]));
                g.FillEllipse(br, x - 5, y - 5, 10, 10);
            }
        }

        private void DrawPie(Graphics g, Rectangle area)
        {
            double total = _values.Sum(); if (total == 0) return;
            int sz = Math.Min(area.Width, area.Height) - 60;
            int px = area.Left + (area.Width - sz) / 2;
            int py = area.Top + (area.Height - sz) / 2;
            var pie = new Rectangle(px, py, sz, sz);
            float start = -90f;
            using var fLbl = new Font("Segoe UI", 8f);
            for (int i = 0; i < _values.Length; i++)
            {
                float sweep = (float)(_values[i] / total * 360);
                using var br = new SolidBrush(Palette[i % Palette.Length]);
                g.FillPie(br, pie, start, sweep);
                using var pen = new Pen(Color.FromArgb(22, 22, 22), 1.5f);
                g.DrawPie(pen, pie, start, sweep);
                // Etiqueta %
                float mid = start + sweep / 2f;
                float rad = sz / 2f * 0.65f;
                float lx = px + sz / 2f + (float)(rad * Math.Cos(mid * Math.PI / 180));
                float ly = py + sz / 2f + (float)(rad * Math.Sin(mid * Math.PI / 180));
                string pct = string.Format("{0:F1}%", _values[i] / total * 100);
                var psz = g.MeasureString(pct, fLbl);
                g.DrawString(pct, fLbl, Brushes.White, lx - psz.Width / 2, ly - psz.Height / 2);
                start += sweep;
            }
            // Leyenda
            using var fLeg = new Font("Segoe UI", 8f);
            int legX = area.Right - 160, legY = area.Top + 10;
            int maxLeg = Math.Min(_labels.Length, 8);
            for (int i = 0; i < maxLeg; i++)
            {
                using var br = new SolidBrush(Palette[i % Palette.Length]);
                g.FillRectangle(br, legX, legY + i * 18, 14, 14);
                string name = _labels[i].Length > 16 ? _labels[i].Substring(0, 15) + "…" : _labels[i];
                g.DrawString(name, fLeg, new SolidBrush(Color.FromArgb(180, 180, 180)), legX + 18, legY + i * 18);
            }
        }

        private void ExportPng()
        {
            var savePath = FilePicker.Save(defaultName: "grafica.png");
            if (savePath == null) return;
            var bmp = new Bitmap(_canvas.Width, _canvas.Height);
            _canvas.DrawToBitmap(bmp, _canvas.ClientRectangle);
            bmp.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
            SetStatus("✓ Exportado: " + Path.GetFileName(savePath));
        }

        private void SetStatus(string msg) => _status.Text = "  " + msg;
    }
}