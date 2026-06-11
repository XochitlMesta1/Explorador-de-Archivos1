using Explorador_de_Archivo.Models;
using Explorador_de_Archivo.Services;
using Microsoft.Win32;
using System.Data;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Explorador_de_Archivo
{
    /// <summary>
    /// Abre PDF, DOCX, PPTX, RTF y texto sin salir del explorador.
    namespace Explorador_de_Archivo
    {
        public class StatisticsForm : Form
        {
            private readonly string _folder;
            private readonly Dictionary<string, int> _counts;

            public StatisticsForm(string folder)
            {
                _folder = folder;
                _counts = CountByExtension(folder);
                Theme.ApplyForm(this);
                Text = "Estadisticas - " + folder;
                Size = new Size(860, 580);
                BuildUI();
            }

            private static Dictionary<string, int> CountByExtension(string folder)
            {
                var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext)) ext = "(sin ext)";
                        result[ext] = result.TryGetValue(ext, out var v) ? v + 1 : 1;
                    }
                }
                catch { /* fallback silencioso intencional */ }
                return result;
            }

            private void BuildUI()
            {
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMain };
                panel.Paint += (_, e) => DrawChart(e.Graphics, panel.Size);
                Controls.AddRange(new Control[] { BuildHeader(), panel });
            }

            private FlowLayoutPanel BuildHeader()
            {
                var bar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 38,
                    BackColor = Theme.BgPanel,
                    Padding = new Padding(8, 5, 0, 0)
                };
                bar.Controls.Add(new Label
                {
                    Text = "Archivos en: " + _folder,
                    ForeColor = Theme.TxtMuted,
                    Font = Theme.FontSmall,
                    AutoSize = true
                });
                return bar;
            }

            private void DrawChart(Graphics g, Size size)
            {
                g.Clear(Theme.BgMain);
                if (_counts.Count == 0)
                { using var b = new SolidBrush(Theme.TxtMuted); g.DrawString("Sin archivos.", Theme.FontNormal, b, 40, 40); return; }
                var sorted = _counts.OrderByDescending(x => x.Value).Take(15).ToList();
                int maxVal = sorted.Max(x => x.Value);
                int pad = 60, barW = Math.Max(30, (size.Width - pad * 2) / sorted.Count), chartH = size.Height - 100;
                for (int i = 0; i < sorted.Count; i++) DrawBar(g, sorted[i], i, barW, chartH, maxVal, pad);
                using var pen = new Pen(Color.FromArgb(60, 60, 80), 1);
                g.DrawLine(pen, pad - 5, 20 + chartH, size.Width - pad + 5, 20 + chartH);
            }

            private static void DrawBar(Graphics g, KeyValuePair<string, int> kv,
                int i, int barW, int chartH, int maxVal, int pad)
            {
                var cols = new[] { Color.FromArgb(0,120,212), Color.FromArgb(48,201,106),
                               Color.FromArgb(255,140,0), Color.FromArgb(220,50,50),
                               Color.FromArgb(150,100,220), Color.FromArgb(0,180,200) };
                int h = maxVal > 0 ? (int)((double)kv.Value / maxVal * chartH) : 4;
                int x = pad + i * barW, y = 20 + chartH - h;
                var col = cols[i % cols.Length];
                using var fill = new SolidBrush(Color.FromArgb(210, col));
                using var bord = new Pen(col, 1);
                using var vb = new SolidBrush(Theme.TxtMain);
                using var lb = new SolidBrush(Theme.TxtMuted);
                using var vf = new Font("Segoe UI", 9f, FontStyle.Bold);
                using var lf = new Font("Segoe UI", 8f);
                g.FillRectangle(fill, x, y, barW - 6, h); g.DrawRectangle(bord, x, y, barW - 6, h);
                g.DrawString(kv.Value.ToString(), vf, vb, x + 2, y - 18);
                var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(kv.Key, lf, lb, new RectangleF(x, chartH + 28, barW - 2, 20), sf);
            }
        }

        /// <summary>Exporta los metadatos de un archivo a la base de datos seleccionada.</summary>
    }
}
