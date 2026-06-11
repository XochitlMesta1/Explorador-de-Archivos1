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
    /// Exporta un archivo a la base de datos seleccionada (SQLite local o SQL Server).
    /// Incluye respaldo binario del archivo para poder restaurarlo después.
    /// </summary>
    public class DatabaseExportForm : Form
    {
        // ── Constantes ─────────────────────────────────────────────
        private const long MaxBackupBytes      = 100L * 1024 * 1024;    // 100 MB
        private const int  EngineSqlite        = 0;
        private const int  EngineSqlServer     = 1;
        private const string SqlServerPort     = "1433";

        // ── Dependencias ───────────────────────────────────────────
        private readonly FileItem        _item;
        private readonly DatabaseService _localDb;
        private readonly Action?         _onInserted;

        // ── Controles UI ───────────────────────────────────────────
        private ComboBox _engine = null!;
        private TextBox  _host   = null!, _port = null!, _user = null!,
                         _dbBox  = null!, _pass = null!;
        private Label    _status = null!;

        public DatabaseExportForm(FileItem item, DatabaseService db, Action? onInserted = null)
        {
            _item       = item;
            _localDb    = db;
            _onInserted = onInserted;

            Theme.ApplyForm(this);
            Text            = "Exportar a Base de Datos";
            Size            = new Size(460, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            BuildUI();
        }

        // ── Construcción de UI ─────────────────────────────────────

        private void BuildUI()
        {
            var layout = BuildLayout();
            AddEngineRow(layout);
            AddField(layout, "Servidor",      ref _host!,  "localhost");
            AddField(layout, "Puerto",        ref _port!,  SqlServerPort);
            AddField(layout, "Base de datos", ref _dbBox!, "ExploradorDB");
            AddField(layout, "Usuario",       ref _user!,  "sa");
            AddPasswordField(layout);
            AddActionRow(layout);
            Controls.Add(layout);
        }

        private static TableLayoutPanel BuildLayout() =>
            new()
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(18, 14, 18, 14),
                BackColor   = Theme.BgMain,
                ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) },
            };

        private void AddEngineRow(TableLayoutPanel layout)
        {
            layout.Controls.Add(BuildFieldLabel("Motor"));
            _engine = Theme.ComboBox();
            _engine.Dock = DockStyle.Fill;
            _engine.Items.AddRange(new object[] { "SQLite (local)", "SQL Server" });
            _engine.SelectedIndex = 0;
            layout.Controls.Add(_engine);
        }

        private static void AddField(TableLayoutPanel layout, string label,
            ref TextBox field, string defaultValue)
        {
            layout.Controls.Add(BuildFieldLabel(label));
            field = Theme.TextBox(false, 28);
            field.Text = defaultValue;
            field.Dock = DockStyle.Fill;
            layout.Controls.Add(field);
        }

        private void AddPasswordField(TableLayoutPanel layout)
        {
            layout.Controls.Add(BuildFieldLabel("Contraseña"));
            _pass = Theme.TextBox(false, 28);
            _pass.PasswordChar = '●';
            _pass.Dock = DockStyle.Fill;
            layout.Controls.Add(_pass);
        }

        private void AddActionRow(TableLayoutPanel layout)
        {
            _status = new Label
            { ForeColor = Theme.Green, Font = Theme.FontSmall, AutoSize = true };

            var row = new FlowLayoutPanel { AutoSize = true, BackColor = Color.Transparent };
            var exportBtn = Theme.AccentBtn("Exportar", 120, 32);
            exportBtn.Click += (_, _) => Export();

            row.Controls.AddRange(new Control[] { exportBtn, _status });
            layout.Controls.Add(row);
        }

        private static Label BuildFieldLabel(string text) =>
            new() { Text = text, ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true };

        // ── Lógica de exportación ──────────────────────────────────

        private void Export()
        {
            if (_engine.SelectedIndex == EngineSqlite)
                ExportToSqlite();
            else
                ExportToSqlServer();
        }

        private void ExportToSqlite()
        {
            var fileData = TryReadFileAsBase64();
            try
            {
                _localDb.Insert(BuildRecord(fileData));
                ShowSqliteSuccess(fileData.Length);
                _onInserted?.Invoke();
            }
            catch (Exception ex)
            {
                ShowError("SQLite: " + ex.Message);
            }
        }

        private DatabaseRecord BuildRecord(string fileData) =>
            new()
            {
                Name     = _item.Name,
                Category = _item.KindText,
                Notes    = "Ruta: " + _item.FullPath + " | Tamano: " + _item.SizeText,
                FileData = fileData,
            };

        private string TryReadFileAsBase64()
        {
            if (!File.Exists(_item.FullPath)) return "";
            try
            {
                var bytes = File.ReadAllBytes(_item.FullPath);
                if (bytes.Length > MaxBackupBytes) return "";
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                // Archivo en uso o sin permisos: guardamos solo metadatos
                return "";
            }
        }

        private byte[] TryReadFileBytes()
        {
            if (!File.Exists(_item.FullPath)) return Array.Empty<byte>();
            try
            {
                var bytes = File.ReadAllBytes(_item.FullPath);
                return bytes.Length > MaxBackupBytes ? Array.Empty<byte>() : bytes;
            }
            catch { return Array.Empty<byte>(); }
        }

        private void ExportToSqlServer()
        {
            var connStr = BuildSqlServerConnectionString();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                ShowError("Completa todos los campos.");
                return;
            }

            var fileBytes = TryReadFileBytes();
            try
            {
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                conn.Open();
                EnsureSqlServerTable(conn);
                InsertSqlServerRecord(conn, fileBytes);
                ShowSqlServerSuccess(fileBytes.Length);
                _onInserted?.Invoke();
            }
            catch (Exception ex)
            {
                ShowError("SQL Server: " + ex.Message);
            }
        }

        private string BuildSqlServerConnectionString() =>
            "Server=" + _host.Text + "," + _port.Text +
            ";Database=" + _dbBox.Text +
            ";User Id=" + _user.Text +
            ";Password=" + _pass.Text +
            ";TrustServerCertificate=true;";

        private static void EnsureSqlServerTable(Microsoft.Data.SqlClient.SqlConnection conn)
        {
            const string createSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='archivos')
                CREATE TABLE archivos (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    nombre NVARCHAR(255),
                    ruta NVARCHAR(500),
                    extension NVARCHAR(20),
                    tamano BIGINT,
                    fecha DATETIME,
                    contenido VARBINARY(MAX)
                )";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(createSql, conn);
            cmd.ExecuteNonQuery();
        }

        private void InsertSqlServerRecord(
            Microsoft.Data.SqlClient.SqlConnection conn, byte[] fileBytes)
        {
            const string insertSql = @"
                INSERT INTO archivos (nombre, ruta, extension, tamano, fecha, contenido)
                VALUES (@nombre, @ruta, @extension, @tamano, @fecha, @contenido)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@nombre",    _item.Name);
            cmd.Parameters.AddWithValue("@ruta",      _item.FullPath);
            cmd.Parameters.AddWithValue("@extension", _item.Extension ?? "");
            cmd.Parameters.AddWithValue("@tamano",    _item.SizeBytes);
            cmd.Parameters.AddWithValue("@fecha",     DateTime.Now);
            cmd.Parameters.AddWithValue("@contenido", fileBytes);
            cmd.ExecuteNonQuery();
        }

        // ── Helpers de status ──────────────────────────────────────

        private void ShowSqliteSuccess(int base64Length)
        {
            _status.ForeColor = Theme.Green;
            if (base64Length == 0)
            {
                _status.Text = "✓ Guardado (solo metadatos)";
                return;
            }
            double mb = base64Length * 0.75 / (1024.0 * 1024.0);
            _status.Text = $"✓ Guardado con respaldo ({mb:F1} MB)";
        }

        private void ShowSqlServerSuccess(int byteLength)
        {
            _status.ForeColor = Theme.Green;
            if (byteLength == 0)
            {
                _status.Text = "✓ Guardado en SQL Server (solo metadatos)";
                return;
            }
            double mb = byteLength / (1024.0 * 1024.0);
            _status.Text = $"✓ Guardado en SQL Server ({mb:F1} MB)";
        }

        private void ShowError(string message)
        {
            _status.ForeColor = Theme.Red;
            _status.Text = "✗ " + message;
        }
    }
}
