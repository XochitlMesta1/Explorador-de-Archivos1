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
        internal static class DocumentViewer
        {
            public static void Open(string path)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                switch (ext)
                {
                    case ".pdf": OpenPdf(path); break;
                    case ".doc": case ".docx": OpenWord(path); break;
                    case ".xls": case ".xlsx": OpenExcel(path); break;
                    case ".ppt": case ".pptx": OpenPpt(path); break;
                    case ".rtf": OpenRtf(path); break;
                    default: OpenText(path); break;
                }
            }

            public static void OpenText(string path)
            {
                var (form, rtb) = CreateTextViewer(path, new Font("Cascadia Code", 10f),
                    wordWrap: false, RichTextBoxScrollBars.Both);
                try { rtb.Text = File.ReadAllText(path); }
                catch (Exception ex) { rtb.Text = "Error: " + ex.Message; }
                form.Show();
            }

            private static void OpenPdf(string path)
            {
                // WebView2: Edge embebido, renderiza PDF nativamente
                // NuGet: Install-Package Microsoft.Web.WebView2 (ya en csproj)
                var form = CreateViewerForm("PDF - " + Path.GetFileName(path));
                form.WindowState = FormWindowState.Maximized;
                try
                {
                    var web = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
                    form.Controls.AddRange(new Control[] { CreateHeaderBar(path), web });
                    form.Show();
                    web.EnsureCoreWebView2Async().ContinueWith(_ =>
                    {
                        if (web.IsDisposed) return;
                        form.BeginInvoke(() => web.Source = new Uri(path));
                    });
                }
                catch
                {
                    // Fallback: abrir con visor predeterminado del sistema
                    var label = new Label
                    {
                        Text = "WebView2 no disponible. Abriendo con el visor del sistema...",
                        ForeColor = Theme.TxtMuted,
                        Font = Theme.FontNormal,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                    };
                    form.Controls.AddRange(new Control[] { CreateHeaderBar(path), label });
                    form.Show();
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }


            private static void OpenExcel(string path)
            {
                var form = CreateViewerForm("Excel - " + Path.GetFileName(path));
                form.Size = new Size(1100, 700);
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    BackgroundColor = Theme.BgMain,
                    ForeColor = Theme.TxtMain,
                    BorderStyle = BorderStyle.None,
                    AllowUserToAddRows = false,
                    ReadOnly = true,
                    RowHeadersVisible = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                };
                grid.DefaultCellStyle.BackColor = Theme.BgMain;
                grid.DefaultCellStyle.ForeColor = Theme.TxtMain;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 212);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.BgPanel;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TxtMain;
                grid.EnableHeadersVisualStyles = false;

                try { LoadExcelToGrid(path, grid); }
                catch (Exception ex)
                {
                    var label = new Label
                    {
                        Text = "Error al leer Excel: " + ex.Message + "\n\n(Recomendacion: instala 'Microsoft.Data.SqlClient' o usa el convertidor para pasar a CSV)",
                        ForeColor = Theme.TxtMuted,
                        Font = Theme.FontNormal,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    form.Controls.AddRange(new Control[] { CreateHeaderBar(path), label });
                    form.Show();
                    return;
                }
                form.Controls.AddRange(new Control[] { CreateHeaderBar(path), grid });
                form.Show();
            }

            private static void LoadExcelToGrid(string path, DataGridView grid)
            {
                // Lectura simple de XLSX: extrae texto del sheet1.xml + sharedStrings.xml
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var sharedStrings = ReadSharedStrings(zip);
                var sheetEntry = zip.Entries.FirstOrDefault(e =>
                    e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml"));
                if (sheetEntry == null) { grid.Columns.Add("vacio", "Vacio"); return; }

                using var reader = new StreamReader(sheetEntry.Open());
                var xml = reader.ReadToEnd();
                var rows = new List<List<string>>();
                int maxCols = 0;

                foreach (Match row in Regex.Matches(xml, @"<row[^>]*>(.*?)</row>", RegexOptions.Singleline))
                {
                    var cells = new List<string>();
                    foreach (Match cell in Regex.Matches(row.Groups[1].Value, @"<c[^>]*(?:\s+t=""([^""]*)"")?[^>]*>(.*?)</c>", RegexOptions.Singleline))
                    {
                        var type = cell.Groups[1].Value;
                        var val = Regex.Match(cell.Groups[2].Value, @"<v>([^<]*)</v>").Groups[1].Value;
                        if (type == "s" && int.TryParse(val, out var idx) && idx < sharedStrings.Count)
                            val = sharedStrings[idx];
                        else if (type == "inlineStr")
                            val = Regex.Match(cell.Groups[2].Value, @"<t[^>]*>([^<]*)</t>").Groups[1].Value;
                        cells.Add(System.Net.WebUtility.HtmlDecode(val));
                    }
                    rows.Add(cells);
                    if (cells.Count > maxCols) maxCols = cells.Count;
                }

                for (int i = 0; i < maxCols; i++)
                    grid.Columns.Add("c" + i, ((char)('A' + i)).ToString());
                foreach (var r in rows)
                {
                    var arr = new object[maxCols];
                    for (int i = 0; i < r.Count && i < maxCols; i++) arr[i] = r[i];
                    grid.Rows.Add(arr);
                }
            }

            private static List<string> ReadSharedStrings(System.IO.Compression.ZipArchive zip)
            {
                var result = new List<string>();
                var entry = zip.GetEntry("xl/sharedStrings.xml");
                if (entry == null) return result;
                using var reader = new StreamReader(entry.Open());
                var xml = reader.ReadToEnd();
                foreach (Match m in Regex.Matches(xml, @"<si[^>]*>(.*?)</si>", RegexOptions.Singleline))
                {
                    var text = string.Concat(Regex.Matches(m.Groups[1].Value, @"<t[^>]*>([^<]*)</t>")
                        .Cast<Match>().Select(x => System.Net.WebUtility.HtmlDecode(x.Groups[1].Value)));
                    result.Add(text);
                }
                return result;
            }

            private static void OpenWord(string path)
            {
                var (form, rtb) = CreateTextViewer(path, new Font("Segoe UI", 10.5f),
                    wordWrap: true, RichTextBoxScrollBars.Vertical);
                try { rtb.Text = ExtractDocxText(path); }
                catch (Exception ex) { rtb.Text = "Error: " + ex.Message; }
                form.Show();
            }

            private static void OpenPpt(string path)
            {
                var (form, rtb) = CreateTextViewer(path, new Font("Segoe UI", 10.5f),
                    wordWrap: true, RichTextBoxScrollBars.Vertical);
                try { rtb.Text = ExtractPptText(path); }
                catch (Exception ex) { rtb.Text = "Error: " + ex.Message; }
                form.Show();
            }

            private static void OpenRtf(string path)
            {
                var (form, rtb) = CreateTextViewer(path, new Font("Segoe UI", 10.5f),
                    wordWrap: true, RichTextBoxScrollBars.Vertical);
                try { rtb.LoadFile(path, RichTextBoxStreamType.RichText); }
                catch (Exception ex) { rtb.Text = "Error: " + ex.Message; }
                form.Show();
            }

            private static (Form form, RichTextBox rtb) CreateTextViewer(
                string path, Font font, bool wordWrap, RichTextBoxScrollBars scrollBars)
            {
                var form = CreateViewerForm(Path.GetFileName(path));
                var rtb = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BackColor = Theme.BgControl,
                    ForeColor = Theme.TxtMain,
                    Font = font,
                    BorderStyle = BorderStyle.None,
                    WordWrap = wordWrap,
                    ScrollBars = scrollBars
                };
                form.Controls.AddRange(new Control[] { CreateHeaderBar(path), rtb });
                return (form, rtb);
            }

            private static Form CreateViewerForm(string title)
            {
                var form = new Form
                {
                    Text = title,
                    Size = new Size(1100, 800),
                    MinimumSize = new Size(600, 400),
                    BackColor = Theme.BgMain,
                    StartPosition = FormStartPosition.CenterScreen,
                    WindowState = FormWindowState.Maximized,
                };
                Theme.ApplyForm(form);
                return form;
            }

            private static FlowLayoutPanel CreateHeaderBar(string path)
            {
                var bar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 38,
                    BackColor = Theme.BgPanel,
                    Padding = new Padding(8, 4, 0, 0)
                };
                bar.Controls.Add(new Label
                {
                    Text = Path.GetFileName(path),
                    ForeColor = Theme.TxtMain,
                    Font = Theme.FontBold,
                    AutoSize = true
                });
                return bar;
            }

            private static void ForceBrowserIE11()
            {
                try
                {
                    var exe = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                    using var key = Registry.CurrentUser.CreateSubKey(
                        @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                    key?.SetValue(exe, 11001, RegistryValueKind.DWord);
                }
                catch { /* fallback silencioso intencional */ }
            }

            private static string ExtractDocxText(string path)
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) return "No se pudo leer el contenido.";
                using var reader = new StreamReader(entry.Open());
                var xml = reader.ReadToEnd();
                xml = Regex.Replace(xml, @"<w:p[ >]", "\r\n");
                xml = Regex.Replace(xml, @"<[^>]+>", "");
                return System.Net.WebUtility.HtmlDecode(xml).Trim();
            }

            private static string ExtractPptText(string path)
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var sb = new System.Text.StringBuilder();
                int slide = 1;
                foreach (var entry in zip.Entries
                    .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                    .OrderBy(e => e.FullName))
                {
                    using var reader = new StreamReader(entry.Open());
                    var xml = reader.ReadToEnd();
                    xml = Regex.Replace(xml, @"<[^>]+>", " ");
                    sb.AppendLine("-- Diapositiva " + slide++ + " --");
                    sb.AppendLine(Regex.Replace(xml, @"\s+", " ").Trim());
                    sb.AppendLine();
                }
                return sb.Length > 0 ? sb.ToString() : "Sin texto extraible.";
            }
        }

        /// <summary>
        /// Visor de base de datos multi-motor.
        /// Conecta automáticamente a los motores configurados en DatabaseConfig.
        /// Para activar SQL Server: llena los campos en DatabaseConfig.cs
        /// </summary>
    }
}