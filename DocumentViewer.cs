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
        public class DatabaseViewerForm : Form
        {
            private readonly DatabaseService _localDb;
            private DataGridView _grid = null!;
            private Label _statusLabel = null!;
            private TabControl _tabs = null!;
            private RichTextBox _sqlBox = null!, _sqlOutput = null!;
            private string _activeEngine = "sqlite";

            public DatabaseViewerForm(DatabaseService localDb)
            {
                _localDb = localDb;
                Theme.ApplyForm(this);
                Text = "Base de Datos";
                Size = new Size(1200, 750);
                MinimumSize = new Size(900, 600);
                BuildUI();
                AutoConnectAll();
            }

            private void BuildUI()
            {
                Controls.AddRange(new Control[] { BuildEngineBar(), BuildTabs() });
            }

            // ── Barra de motores disponibles ──────────────────────────

            private FlowLayoutPanel BuildEngineBar()
            {
                var bar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = Theme.BgPanel,
                    Padding = new Padding(8, 7, 0, 0)
                };

                _statusLabel = new Label
                {
                    ForeColor = Theme.TxtMuted,
                    Font = Theme.FontSmall,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                bar.Controls.AddRange(new Control[]
                {
                EngineButton("SQLite (local)",    "sqlite",    true),
                EngineButton("SQL Server",        "sqlserver", DatabaseConfig.SqlServer.IsConfigured),
                _statusLabel,
                });
                return bar;
            }

            private Button EngineButton(string label, string engine, bool available)
            {
                var btn = Theme.FlatBtn(label, 130, 28);
                btn.Enabled = available;
                if (!available) btn.Text += " (pendiente)";
                btn.Click += (_, _) => { _activeEngine = engine; LoadEngine(engine); };
                return btn;
            }

            // ── Tabs ──────────────────────────────────────────────────

            private TabControl BuildTabs()
            {
                _tabs = new TabControl { Dock = DockStyle.Fill };
                _tabs.TabPages.Add(BuildRecordsTab());
                _tabs.TabPages.Add(BuildSqlTab());
                _tabs.TabPages.Add(BuildChartTab());
                return _tabs;
            }

            private Panel? _chartCanvas;

            private TabPage BuildChartTab()
            {
                var tab = new TabPage { Text = "📊 Gráfica por extensión", BackColor = Theme.BgMain };
                _chartCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMain };
                _chartCanvas.Paint += (_, e) => DrawTypeChart(e.Graphics, _chartCanvas.Size);
                tab.Controls.Add(_chartCanvas);

                // Refrescar cuando el usuario cambie a esta pestaña
                _tabs.SelectedIndexChanged += (_, _) =>
                {
                    if (_tabs.SelectedTab == tab) _chartCanvas?.Invalidate();
                };
                return tab;
            }

            private void DrawTypeChart(Graphics g, Size size)
            {
                g.Clear(Theme.BgMain);
                var counts = CountRecordsByCategory();
                if (counts.Count == 0)
                {
                    using var br = new SolidBrush(Theme.TxtMuted);
                    g.DrawString("Sin registros en la base de datos.", Theme.FontNormal, br, 40, 40);
                    return;
                }
                var sorted = counts.OrderByDescending(x => x.Value).Take(12).ToList();
                int maxVal = sorted.Max(x => x.Value);
                int pad = 70;
                int barW = Math.Max(40, (size.Width - pad * 2) / sorted.Count);
                int chartH = size.Height - 120;

                using var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
                using var titleBr = new SolidBrush(Theme.TxtMain);
                g.DrawString("Archivos en BD por extension (.mp4, .pdf, .jpg, etc.)",
                    titleFont, titleBr, pad, 12);

                var palette = new[]
                {
                Color.FromArgb(0,120,212),   Color.FromArgb(48,201,106),
                Color.FromArgb(255,140,0),   Color.FromArgb(220,50,50),
                Color.FromArgb(150,100,220), Color.FromArgb(0,180,200),
                Color.FromArgb(255,200,0),   Color.FromArgb(100,180,100),
            };
                using var vFont = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var lFont = new Font("Segoe UI", 9f);
                using var vBr = new SolidBrush(Theme.TxtMain);
                using var lBr = new SolidBrush(Theme.TxtMuted);
                for (int i = 0; i < sorted.Count; i++)
                {
                    var kv = sorted[i];
                    int h = maxVal > 0 ? (int)((double)kv.Value / maxVal * chartH) : 4;
                    int x = pad + i * barW;
                    int y = 50 + chartH - h;
                    var col = palette[i % palette.Length];
                    using var fill = new SolidBrush(Color.FromArgb(210, col));
                    using var bord = new Pen(col, 1);
                    g.FillRectangle(fill, x, y, barW - 10, h);
                    g.DrawRectangle(bord, x, y, barW - 10, h);
                    g.DrawString(kv.Value.ToString(), vFont, vBr, x + 4, y - 20);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(kv.Key, lFont, lBr,
                        new RectangleF(x, 50 + chartH + 8, barW - 4, 30), sf);
                }
                using var basePen = new Pen(Color.FromArgb(60, 60, 80), 1);
                g.DrawLine(basePen, pad - 5, 50 + chartH, size.Width - pad + 5, 50 + chartH);
            }

            private Dictionary<string, int> CountRecordsByCategory()
            {
                // Cuenta por extension real del archivo (.mp4, .pdf, .jpg, etc.)
                // Extrae la ruta del campo Notes "Ruta: ... | ..."
                var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var r in _localDb.GetAll())
                    {
                        var ext = ExtractExtensionFromRecord(r);
                        result[ext] = result.TryGetValue(ext, out var v) ? v + 1 : 1;
                    }
                }
                catch { /* fallback silencioso intencional */ }
                return result;
            }

            private static string ExtractExtensionFromRecord(DatabaseRecord r)
            {
                // Intento 1: extension del campo Name (si tiene una)
                var extFromName = Path.GetExtension(r.Name).ToLowerInvariant();
                if (!string.IsNullOrEmpty(extFromName)) return extFromName;

                // Intento 2: parsear la ruta dentro de Notes "Ruta: C:\... | Tamano: ..."
                if (!string.IsNullOrEmpty(r.Notes))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        r.Notes, @"Ruta:\s*([^|]+)");
                    if (match.Success)
                    {
                        var ext = Path.GetExtension(match.Groups[1].Value.Trim()).ToLowerInvariant();
                        if (!string.IsNullOrEmpty(ext)) return ext;
                    }
                }

                // Fallback: usar categoria
                return string.IsNullOrEmpty(r.Category) ? "(sin ext)" : r.Category;
            }

            private TabPage BuildRecordsTab()
            {
                var tab = new TabPage { Text = "📋 Registros", BackColor = Theme.BgMain };
                _grid = Theme.Grid(); _grid.Dock = DockStyle.Fill;

                var toolbar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = Theme.BgPanel,
                    Padding = new Padding(6, 6, 0, 0)
                };

                var restoreBtn = Theme.AccentBtn("♻ Recuperar archivo", 180, 28);
                restoreBtn.Click += (_, _) => RestoreSelectedFile();

                var deleteBtn = Theme.FlatBtn("🗑 Eliminar registro", 170, 28);
                deleteBtn.Click += (_, _) => DeleteSelectedRecord();

                var refreshBtn = Theme.FlatBtn("🔄 Actualizar", 120, 28);
                refreshBtn.Click += (_, _) => LoadEngine(_activeEngine);

                toolbar.Controls.AddRange(new Control[] { restoreBtn, deleteBtn, refreshBtn });
                tab.Controls.Add(_grid);
                tab.Controls.Add(toolbar);
                return tab;
            }

            /// <summary>
            /// Restaura el archivo respaldado en la base de datos al disco.
            /// Lee el campo FileData (base64) y lo escribe en la ruta original o donde elija el usuario.
            /// </summary>
            private void RestoreSelectedFile()
            {
                if (_grid.CurrentRow == null) return;
                if (_grid.CurrentRow.DataBoundItem is not DatabaseRecord record)
                {
                    MessageBox.Show("Selecciona un registro válido.");
                    return;
                }

                if (string.IsNullOrEmpty(record.FileData))
                {
                    MessageBox.Show("Este registro no tiene archivo respaldado (solo metadatos).",
                        "Sin contenido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    var bytes = System.Convert.FromBase64String(record.FileData);
                    var savePath = Forms.FilePicker.Save(defaultName: record.Name);
                    if (savePath == null) return;
                    File.WriteAllBytes(savePath, bytes);
                    MessageBox.Show("✓ Archivo restaurado:" + savePath, "Recuperado",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al restaurar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void DeleteSelectedRecord()
            {
                if (_grid.CurrentRow == null) return;
                if (_grid.CurrentRow.DataBoundItem is not DatabaseRecord record) return;
                if (MessageBox.Show($"¿Eliminar el registro '{record.Name}'?", "Confirmar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                _localDb.Delete(record.Id);
                _grid.DataSource = _localDb.GetAll();
            }

            private TabPage BuildSqlTab()
            {
                var tab = new TabPage { Text = "💻 Consola SQL", BackColor = Theme.BgMain };
                var split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,
                    SplitterDistance = 180,
                    SplitterWidth = 4,
                    BackColor = Theme.Border
                };

                var sqlBar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    BackColor = Theme.BgPanel,
                    Padding = new Padding(6, 4, 0, 0)
                };
                var runBtn = Theme.AccentBtn("Ejecutar", 100, 28);
                runBtn.Click += (_, _) => ExecuteSql();
                sqlBar.Controls.Add(runBtn);

                _sqlBox = Theme.RichBox(false);
                _sqlBox.Dock = DockStyle.Fill;
                _sqlBox.Text = "SELECT TOP 50 id, nombre, ruta, extension, tamano, fecha FROM archivos ORDER BY id;";
                split.Panel1.Controls.AddRange(new Control[] { sqlBar, _sqlBox });

                _sqlOutput = Theme.RichBox(true);
                _sqlOutput.Dock = DockStyle.Fill;
                split.Panel2.Controls.Add(_sqlOutput);

                tab.Controls.Add(split);
                return tab;
            }

            // ── Conexión automática ───────────────────────────────────

            private void AutoConnectAll()
            {
                LoadSqlite();
                if (DatabaseConfig.SqlServer.IsConfigured)
                    Task.Run(() => TestConnection("sqlserver", DatabaseConfig.SqlServer.ConnectionString));
            }

            private void LoadEngine(string engine)
            {
                switch (engine)
                {
                    case "sqlite": LoadSqlite(); break;
                    case "sqlserver": LoadExternal(DatabaseConfig.SqlServer.ConnectionString, "sqlserver"); break;
                }
            }

            private void LoadSqlite()
            {
                try
                {
                    _grid.DataSource = _localDb.GetAll();
                    ShowStatus("✓ SQLite — " + _localDb.GetAll().Count + " registros", success: true);
                }
                catch (Exception ex) { ShowStatus("Error SQLite: " + ex.Message, success: false); }
            }

            private void LoadExternal(string connStr, string engine)
            {
                ShowStatus("Conectando...", success: true);
                Task.Run(() =>
                {
                    try
                    {
                        var table = QueryToDataTable(connStr, engine,
                            "SELECT id, nombre, ruta, extension, tamano, fecha FROM archivos ORDER BY id");
                        BeginInvoke(() =>
                        {
                            _grid.DataSource = table;
                            ShowStatus("✓ Conectado — " + table.Rows.Count + " registro(s) en archivos", success: true);
                        });
                    }
                    catch (Exception ex) { BeginInvoke(() => ShowStatus("Error: " + ex.Message, success: false)); }
                });
            }

            private void TestConnection(string engine, string connStr)
            {
                try { QueryToDataTable(connStr, engine, "SELECT 1"); }
                catch { /* silencioso — el botón ya muestra "pendiente" */ }
            }

            private void ExecuteSql()
            {
                if (_activeEngine == "sqlite")
                {
                    _sqlOutput.Text = _localDb.RunSql(_sqlBox.Text);
                    return;
                }
                var connStr = DatabaseConfig.SqlServer.ConnectionString;

                Task.Run(() =>
                {
                    try
                    {
                        var result = QueryToDataTable(connStr, _activeEngine, _sqlBox.Text);
                        BeginInvoke(() => _sqlOutput.Text = DataTableToText(result));
                    }
                    catch (Exception ex) { BeginInvoke(() => _sqlOutput.Text = "ERROR: " + ex.Message); }
                });
            }

            // ── Helpers de query ─────────────────────────────────────

            private static System.Data.DataTable QueryToDataTable(
                string connStr, string engine, string sql)
            {
                var table = new System.Data.DataTable();
                switch (engine)
                {
                    case "sqlserver":
                        using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                        { conn.Open(); table.Load(cmd.ExecuteReader()); }
                        break;
                }
                return table;
            }

            private static string DataTableToText(System.Data.DataTable table)
            {
                var sb = new System.Text.StringBuilder();
                foreach (System.Data.DataColumn col in table.Columns)
                    sb.Append(col.ColumnName.PadRight(20));
                sb.AppendLine();
                sb.AppendLine(new string('-', table.Columns.Count * 20));
                foreach (System.Data.DataRow row in table.Rows)
                {
                    foreach (var val in row.ItemArray)
                        sb.Append((val?.ToString() ?? "").PadRight(20));
                    sb.AppendLine();
                }
                return sb.ToString();
            }

            private void ShowStatus(string msg, bool success)
            {
                _statusLabel.ForeColor = success ? Theme.Green : Theme.Red;
                _statusLabel.Text = msg;
            }
        }


        /// <summary>Grafica de barras con tipos de archivo en la carpeta actual.</summary>
    }
}
