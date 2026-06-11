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
    public class FilePicker : Form
    {
        public string? SelectedPath { get; private set; }

        private string  _currentPath;
        private readonly string _extensionFilter;
        private readonly bool   _requireExistingFile;

        private ListView _fileList   = null!;
        private Label    _pathLabel  = null!;
        private TextBox  _nameBox    = null!;
        private Label    _statusLabel = null!;

        public static string? Open(string startPath = "", string filter = "*")
        {
            var resolvedPath = ResolveStartPath(startPath);
            using var picker = new FilePicker(resolvedPath, filter, requireExistingFile: true);
            return picker.ShowDialog() == DialogResult.OK ? picker.SelectedPath : null;
        }

        public static string? Save(string startPath = "", string defaultName = "archivo.txt")
        {
            var resolvedPath = ResolveStartPath(startPath);
            using var picker = new FilePicker(resolvedPath, "*", requireExistingFile: false);
            picker._nameBox.Text = defaultName;
            return picker.ShowDialog() == DialogResult.OK ? picker.SelectedPath : null;
        }

        private static string ResolveStartPath(string path) =>
            !string.IsNullOrEmpty(path) && Directory.Exists(path)
                ? path
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        private FilePicker(string startPath, string filter, bool requireExistingFile)
        {
            _currentPath         = startPath;
            _extensionFilter     = filter;
            _requireExistingFile = requireExistingFile;

            Theme.ApplyForm(this);
            Text            = requireExistingFile ? "Seleccionar archivo" : "Guardar archivo";
            Size            = new Size(820, 580);
            MinimumSize     = new Size(600, 450);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            BuildUI();
            LoadDirectory(_currentPath);
        }

        private void BuildUI()
        {
            // Orden: Bottom y Top primero, Fill al final
            Controls.Add(BuildSelectionBar());     // Dock = Bottom
            Controls.Add(BuildNavigationBar());    // Dock = Top
            // Wrapper con padding para que la lista no quede pegada a la nav bar
            var wrapper = new Panel
            { Dock = DockStyle.Fill, BackColor = Theme.BgMain, Padding = new Padding(0, 8, 0, 0) };
            wrapper.Controls.Add(BuildFileListArea());
            Controls.Add(wrapper);
        }

        private FlowLayoutPanel BuildNavigationBar()
        {
            var bar = new FlowLayoutPanel
            { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgPanel,
              Padding = new Padding(8, 7, 0, 0) };

            var upButton = Theme.FlatBtn("↑ Subir", 80, 28);
            upButton.Click += (_, _) => NavigateUp();

            _pathLabel = new Label
            { ForeColor = Theme.TxtSub, Font = Theme.FontSmall,
              AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

            bar.Controls.AddRange(new Control[] { upButton, _pathLabel });
            return bar;
        }

        private Panel BuildFileListArea()
        {
            var container = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMain };
            _fileList = BuildFileListView();
            container.Controls.Add(_fileList);
            return container;
        }

        private ListView BuildFileListView()
        {
            var list = new ListView
            { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
              BackColor = Theme.BgMain, ForeColor = Theme.TxtMain,
              BorderStyle = BorderStyle.None, Font = Theme.FontNormal };
            list.Columns.Add("Nombre",    300);
            list.Columns.Add("Tipo",      100);
            list.Columns.Add("Tamaño",    90);
            list.Columns.Add("Modificado",140);
            list.DoubleClick           += OnEntryDoubleClicked;
            list.SelectedIndexChanged  += OnEntrySelected;
            return list;
        }

        private Panel BuildSelectionBar()
        {
            var bar = new Panel
            { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.BgPanel,
              Padding = new Padding(10, 8, 10, 8) };
            bar.Controls.Add(BuildNameRow());
            bar.Controls.Add(BuildButtonRow());
            return bar;
        }

        private FlowLayoutPanel BuildNameRow()
        {
            var row = new FlowLayoutPanel
            { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
            row.Controls.Add(new Label
            { Text = "Nombre:", ForeColor = Theme.TxtMuted, Font = Theme.FontSmall,
              AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            _nameBox = Theme.TextBox(false, 24);
            _nameBox.Width = 520;
            _nameBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) ConfirmSelection(); };
            row.Controls.Add(_nameBox);
            return row;
        }

        private FlowLayoutPanel BuildButtonRow()
        {
            var row = new FlowLayoutPanel
            { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.Transparent,
              FlowDirection = FlowDirection.RightToLeft };

            var confirmBtn = Theme.AccentBtn("✓ Seleccionar", 130, 24);
            confirmBtn.Click += (_, _) => ConfirmSelection();

            var cancelBtn = Theme.FlatBtn("✗ Cancelar", 100, 24);
            cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;

            _statusLabel = new Label
            { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall,
              AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

            row.Controls.AddRange(new Control[] { cancelBtn, confirmBtn, _statusLabel });
            return row;
        }

        private void LoadDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            _currentPath    = path;
            _pathLabel.Text = path;
            _fileList.Items.Clear();

            try
            {
                AddDirectoryEntries(path);
                AddFileEntries(path);
            }
            catch (Exception ex) { _statusLabel.Text = "Error: " + ex.Message; }
        }

        private void AddDirectoryEntries(string path)
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var info = new DirectoryInfo(dir);
                if (info.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                _fileList.Items.Add(BuildDirectoryRow(info, dir));
            }
        }

        private static ListViewItem BuildDirectoryRow(DirectoryInfo info, string fullPath)
        {
            var item = new ListViewItem($"📁  {info.Name}")
            { Tag = fullPath, ForeColor = Theme.TxtMain, BackColor = Theme.BgMain };
            item.SubItems.Add("Carpeta");
            item.SubItems.Add("");
            item.SubItems.Add(info.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
            return item;
        }

        private void AddFileEntries(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var info = new FileInfo(file);
                if (!FileMatchesFilter(info.Name)) continue;
                _fileList.Items.Add(BuildFileRow(info, file));
            }
        }

        private static ListViewItem BuildFileRow(FileInfo info, string fullPath)
        {
            var kind = FileKindHelper.From(info.Extension);
            var item = new ListViewItem($"{kind.Emoji()}  {info.Name}")
            { Tag = fullPath, ForeColor = Theme.TxtMain, BackColor = Theme.BgMain };
            item.SubItems.Add(kind.Label());
            item.SubItems.Add(FormatFileSize(info.Length));
            item.SubItems.Add(info.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
            return item;
        }

        private bool FileMatchesFilter(string fileName)
        {
            if (_extensionFilter == "*") return true;
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return _extensionFilter.Split(',', ';')
                .Select(f => f.Trim().TrimStart('*').ToLowerInvariant())
                .Any(f => ext == f || f == "*" || f == ".*");
        }

        private void NavigateUp()
        {
            var parent = Directory.GetParent(_currentPath)?.FullName;
            if (parent != null) LoadDirectory(parent);
        }

        private void OnEntryDoubleClicked(object? sender, EventArgs e)
        {
            if (_fileList.SelectedItems.Count == 0) return;
            var path = _fileList.SelectedItems[0].Tag as string;
            if (path == null) return;
            if (Directory.Exists(path)) LoadDirectory(path);
            else ConfirmSelection();
        }

        private void OnEntrySelected(object? sender, EventArgs e)
        {
            if (_fileList.SelectedItems.Count == 0) return;
            var path = _fileList.SelectedItems[0].Tag as string;
            if (path != null && File.Exists(path))
                _nameBox.Text = Path.GetFileName(path);
        }

        private void ConfirmSelection()
        {
            var name = _nameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) { _statusLabel.Text = "Escribe un nombre"; return; }

            var fullPath = Path.IsPathRooted(name) ? name : Path.Combine(_currentPath, name);

            if (_requireExistingFile && !File.Exists(fullPath))
            {
                if (Directory.Exists(fullPath)) { LoadDirectory(fullPath); return; }
                _statusLabel.Text = "Archivo no encontrado";
                return;
            }

            SelectedPath = fullPath;
            DialogResult = DialogResult.OK;
        }

        private static string FormatFileSize(long bytes) => bytes switch
        {
            < 1_024         => $"{bytes} B",
            < 1_048_576     => $"{bytes / 1024.0:F1} KB",
            < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
            _               => $"{bytes / 1_073_741_824.0:F1} GB",
        };
    }

    // ── Base de datos (CRUD + SQL) ────────────────────────────────
}
