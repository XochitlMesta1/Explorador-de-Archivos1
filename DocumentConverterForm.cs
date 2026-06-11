using AForge.Video.DirectShow;
using Explorador_de_Archivo;
using Explorador_de_Archivo.Models;
using Explorador_de_Archivo.Services;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Explorador_de_Archivo.Services.ConverterService;
using FileExplorer.Forms;
using static Explorador_de_Archivo.Forms.UiHelpers;

namespace Explorador_de_Archivo.Forms
{
    public class DatabaseForm : Form
    {
        private readonly DatabaseService _db;

        private DataGridView _recordGrid  = null!;
        private TextBox      _searchBox   = null!;
        private Label        _countLabel  = null!;
        private RichTextBox  _sqlEditor   = null!, _sqlOutput = null!;

        public DatabaseForm(DatabaseService db)
        {
            _db = db;
            Theme.ApplyForm(this);
            Text        = "Base de Datos";
            Size        = new Size(1300, 800);
            MinimumSize = new Size(1000, 700);
            BuildUI();
            LoadAllRecords();
        }

        private void BuildUI()
        {
            var toolbar = BuildToolbar();
            var tabs    = BuildTabs();
            Controls.AddRange(new Control[] { tabs, toolbar });
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var bar = Toolbar(DockStyle.Top, 46);
            _searchBox = Theme.TextBox(false, 30);
            _searchBox.Width = 190;
            _searchBox.PlaceholderText = "Buscar...";
            _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SearchRecords(); };
            _countLabel = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true };
            bar.Controls.AddRange(new Control[]
            {
                ActionButton("➕ Nuevo",      100, CreateRecord),
                ActionButton("✏ Editar",       90, EditSelectedRecord),
                ActionButton("🗑 Eliminar",    100, DeleteSelectedRecord),
                ActionButton("🔄 Actualizar",  110, LoadAllRecords),
                ActionButton("📤 JSON",         80, ExportToJson),
                ActionButton("📤 CSV",          80, ExportToCsv),
                _searchBox,
                ActionButton("🔍", 34, SearchRecords),
                _countLabel,
            });
            return bar;
        }

        private TabControl BuildTabs()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildRecordsTab());
            tabs.TabPages.Add(BuildSqlTab());
            return tabs;
        }

        private TabPage BuildRecordsTab()
        {
            var tab = new TabPage { Text = "📋 Registros", BackColor = Theme.BgMain };
            _recordGrid = Theme.Grid(); _recordGrid.Dock = DockStyle.Fill;
            AddGridColumns();
            _recordGrid.DoubleClick += (_, _) => EditSelectedRecord();
            tab.Controls.Add(_recordGrid);
            return tab;
        }

        private void AddGridColumns()
        {
            var columns = new[]
            {
                ("ID",        "Id",        50),
                ("Nombre",    "Name",     200),
                ("Email",     "Email",    200),
                ("Teléfono",  "Phone",    130),
                ("Categoría", "Category", 120),
                ("Notas",     "Notes",    160),
                ("Creado",    "CreatedAt",130),
            };
            foreach (var (header, property, width) in columns)
                _recordGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = header, DataPropertyName = property, Width = width });
        }

        private TabPage BuildSqlTab()
        {
            var tab   = new TabPage { Text = "💻 SQL Console", BackColor = Theme.BgMain };
            var split = HorizontalSplit(DockStyle.Fill);
            split.SplitterDistance = 250;

            var sqlBar = Toolbar(DockStyle.Top, 38);
            _sqlEditor = EditorBox(split.Panel1);
            _sqlEditor.Text = "SELECT TOP 20 id, nombre, ruta, extension, tamano, fecha FROM archivos ORDER BY id";
            sqlBar.Controls.AddRange(new Control[]
            {
                ActionButton("▶ Ejecutar", 110, ExecuteSql, accent: true),
                ActionButton("INSERT",      90, InsertTemplate),
                ActionButton("COUNT",       80, CountTemplate),
            });
            split.Panel1.Controls.Add(sqlBar);
            _sqlOutput = EditorBox(split.Panel2, readOnly: true);
            tab.Controls.Add(split);
            return tab;
        }

        private void LoadAllRecords()     => BindRecords(_db.GetAll());
        private void SearchRecords()      => BindRecords(_db.Search(_searchBox.Text));

        private void BindRecords(List<DatabaseRecord> records)
        {
            _recordGrid.DataSource = records;
            _countLabel.Text       = $"  {records.Count} registro(s)";
        }

        private void CreateRecord()
        {
            var dialog = new RecordDialog(null);
            if (dialog.ShowDialog() == DialogResult.OK) { _db.Insert(dialog.Record); LoadAllRecords(); }
        }

        private void EditSelectedRecord()
        {
            if (_recordGrid.CurrentRow?.DataBoundItem is not DatabaseRecord record) return;
            var dialog = new RecordDialog(record);
            if (dialog.ShowDialog() == DialogResult.OK) { _db.Update(dialog.Record); LoadAllRecords(); }
        }

        private void DeleteSelectedRecord()
        {
            if (_recordGrid.CurrentRow?.DataBoundItem is not DatabaseRecord record) return;
            if (MessageBox.Show($"¿Eliminar '{record.Name}'?", "Confirmar",
                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _db.Delete(record.Id);
            LoadAllRecords();
        }

        private void ExportToJson()
        {
            var savePath = FilePicker.Save(defaultName: "registros.json");
            if (savePath == null) return;
            File.WriteAllText(savePath,
                JsonConvert.SerializeObject(_db.GetAll(), Formatting.Indented));
            MessageBox.Show("Exportado.");
        }

        private void ExportToCsv()
        {
            var savePath = FilePicker.Save(defaultName: "registros.csv");
            if (savePath == null) return;
            var lines = new List<string> { "Id,Nombre,Email,Telefono,Categoria,Notas,Creado" };
            lines.AddRange(_db.GetAll().Select(r =>
                $"{r.Id},{r.Name},{r.Email},{r.Phone},{r.Category},{r.Notes},{r.CreatedAt}"));
            File.WriteAllLines(savePath, lines);
            MessageBox.Show("Exportado.");
        }

        private void ExecuteSql()     => _sqlOutput.Text = _db.RunSql(_sqlEditor.Text);
        private void InsertTemplate() => _sqlEditor.Text =
            "INSERT INTO archivos (nombre, ruta, extension, tamano, fecha) VALUES (N'NombreArchivo.ext', N'C:\\ruta\\NombreArchivo.ext', N'.ext', 0, GETDATE())";
        private void CountTemplate()  => _sqlEditor.Text = "SELECT COUNT(*) FROM archivos";
    }

    // ── Diálogo de registro ───────────────────────────────────────
    public class RecordDialog : Form
    {
        public DatabaseRecord Record { get; }

        private TextBox     _nameField     = null!, _emailField    = null!,
                            _phoneField    = null!, _categoryField = null!;
        private RichTextBox _notesField    = null!;

        public RecordDialog(DatabaseRecord? existing)
        {
            Record = existing ?? new DatabaseRecord();
            Theme.ApplyForm(this);
            Text            = existing == null ? "Nuevo registro" : "Editar registro";
            Size            = new Size(420, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            BuildUI();
        }

        private void BuildUI()
        {
            var layout = BuildLayout();
            int row    = 0;

            AddTextField(layout, "Nombre *",  ref _nameField!,     Record.Name,     ref row);
            AddTextField(layout, "Email",      ref _emailField!,    Record.Email,    ref row);
            AddTextField(layout, "Teléfono",   ref _phoneField!,    Record.Phone,    ref row);
            AddTextField(layout, "Categoría",  ref _categoryField!, Record.Category, ref row);
            AddNotesField(layout, ref row);
            AddButtonRow(layout, row);

            Controls.Add(layout);
        }

        private static TableLayoutPanel BuildLayout()
        {
            var layout = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12,
              Padding = new Padding(16, 12, 16, 12), BackColor = Theme.BgMain };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 10; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i % 2 == 0 ? 18 : 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            return layout;
        }

        private static void AddTextField(TableLayoutPanel layout, string label,
            ref TextBox field, string value, ref int row)
        {
            layout.Controls.Add(Theme.Label(label), 0, row++);
            field = Theme.TextBox(false, 28);
            field.Text = value;
            field.Dock = DockStyle.Fill;
            layout.Controls.Add(field, 0, row++);
        }

        private void AddNotesField(TableLayoutPanel layout, ref int row)
        {
            layout.Controls.Add(Theme.Label("Notas"), 0, row++);
            _notesField = new RichTextBox
            { BackColor = Theme.BgControl, ForeColor = Theme.TxtMain,
              BorderStyle = BorderStyle.FixedSingle, Font = Theme.FontNormal,
              Dock = DockStyle.Fill, Text = Record.Notes };
            layout.Controls.Add(_notesField, 0, row++);
        }

        private void AddButtonRow(TableLayoutPanel layout, int row)
        {
            var buttonRow = new FlowLayoutPanel
            { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
              BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
            buttonRow.Controls.AddRange(new Control[]
            {
                ActionButton("Guardar",   100, SaveRecord, accent: true),
                ActionButton("Cancelar",  100, () => DialogResult = DialogResult.Cancel),
            });
            layout.Controls.Add(buttonRow, 0, row);
        }

        private void SaveRecord()
        {
            if (string.IsNullOrWhiteSpace(_nameField.Text))
            { MessageBox.Show("El nombre es obligatorio."); return; }
            Record.Name     = _nameField.Text.Trim();
            Record.Email    = _emailField.Text.Trim();
            Record.Phone    = _phoneField.Text.Trim();
            Record.Category = _categoryField.Text.Trim();
            Record.Notes    = _notesField.Text.Trim();
            DialogResult    = DialogResult.OK;
        }
    }

    // ── Correo electrónico ────────────────────────────────────────
}
