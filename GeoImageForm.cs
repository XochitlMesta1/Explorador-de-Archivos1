using Explorador_de_Archivo.Forms;
using Explorador_de_Archivo.Models;
using Explorador_de_Archivo.Services;
using FileExplorer.Forms;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Explorador_de_Archivo
{
    /// <summary>
    /// Construcción de toda la interfaz de usuario del explorador.
    /// Responsabilidad única: crear y configurar controles visuales.
    /// </summary>
    public partial class Form1
    {
        // ── Controles del layout principal ────────────────────────
        private FlowLayoutPanel  _fileGrid    = null!;
        private ListView         _fileList    = null!;
        private Panel            _detailPane  = null!;
        private Label            _statusBar   = null!;
        private ToolStripTextBox _pathBox     = null!, _searchBox = null!;

        // ── Controles del panel de detalles ───────────────────────
        private PictureBox      _previewImage = null!;
        private Label           _detailName   = null!, _detailKind = null!,
                                _detailSize   = null!, _detailDate = null!,
                                _detailExt    = null!;
        private FlowLayoutPanel _actionPanel  = null!;

        internal void BuildUI()
        {
            // Orden CRITICO en WinForms:
            // El Dock = Fill debe agregarse PRIMERO para que los Dock = Top/Bottom
            // queden por encima y ocupen su espacio, no lo tapen.
            BuildMainLayout();  // Dock = Fill
            BuildStatusBar();   // Dock = Bottom
            BuildToolbar();     // Dock = Top
        }

        // ── Toolbar ───────────────────────────────────────────────

        private void BuildToolbar()
        {
            var toolbar = CreateToolstrip();
            AddNavigationButtons(toolbar);
            AddPathBox(toolbar);
            AddSearchBox(toolbar);
            AddToolButtons(toolbar);
            Controls.Add(toolbar);
        }

        private static ToolStrip CreateToolstrip() => new()
        {
            BackColor  = Theme.BgPanel, ForeColor = Theme.TxtMain,
            RenderMode = ToolStripRenderMode.System,
            Dock       = DockStyle.Top, Height = 44,
            GripStyle  = ToolStripGripStyle.Hidden,
            Padding    = new Padding(6, 5, 6, 5),
        };

        private void AddNavigationButtons(ToolStrip bar)
        {
            bar.Items.Add(ToolbarBtn("◀", "Atrás",     (_, _) => GoBack()));
            bar.Items.Add(ToolbarBtn("▶", "Adelante",   (_, _) => GoForward()));
            bar.Items.Add(ToolbarBtn("↑", "Subir",      (_, _) => GoUp()));
            bar.Items.Add(ToolbarBtn("⟳", "Actualizar", (_, _) => Reload()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolbarBtn("📁 Nueva", null, (_, _) => CreateFolder()));
            bar.Items.Add(new ToolStripSeparator());
        }

        private void AddPathBox(ToolStrip bar)
        {
            _pathBox = new ToolStripTextBox
            { Width = 280, BackColor = Theme.BgControl,
              ForeColor = Theme.TxtMain, BorderStyle = BorderStyle.FixedSingle };
            _pathBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Navigate(_pathBox.Text); };
            bar.Items.Add(_pathBox);
            bar.Items.Add(new ToolStripSeparator());
        }

        private void AddSearchBox(ToolStrip bar)
        {
            _searchBox = new ToolStripTextBox
            { Width = 160, BackColor = Theme.BgControl,
              ForeColor = Theme.TxtMain, BorderStyle = BorderStyle.FixedSingle };
            _searchBox.TextBox.PlaceholderText = "🔍 Buscar...";
            _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Search(_searchBox.Text); };
            bar.Items.Add(_searchBox);
            bar.Items.Add(new ToolStripSeparator());
        }

        private void AddToolButtons(ToolStrip bar)
        {
            bar.Items.Add(ToolbarBtn("⊞ Vista",          null, (_, _) => ToggleView()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolbarBtn("📑 Convertir", null, (_, _) => new DocumentConverterForm(SelectedItem?.FullPath).Show()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolbarBtn("🗄 BD",  null, (_, _) => new Explorador_de_Archivo.DatabaseViewerForm(Db).Show()));
        }

        private static ToolStripButton ToolbarBtn(string text, string? tooltip, EventHandler handler)
        {
            var btn = new ToolStripButton(text)
            { ToolTipText = tooltip ?? text, DisplayStyle = ToolStripItemDisplayStyle.Text,
              ForeColor = Theme.TxtMain, BackColor = Color.Transparent, AutoSize = true };
            btn.Click += handler;
            return btn;
        }

        // ── Status bar ────────────────────────────────────────────

        private void BuildStatusBar()
        {
            var bar = new Panel
            { Dock = DockStyle.Bottom, Height = 24,
              BackColor = Color.FromArgb(18, 18, 18), Padding = new Padding(10, 0, 0, 0) };
            _statusBar = new Label
            { AutoSize = true, ForeColor = Theme.TxtMuted, Font = Theme.FontMono,
              Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft, Top = 4 };
            bar.Controls.Add(_statusBar);
            Controls.Add(bar);
        }

        internal void SetStatus(string message) => _statusBar.Text = message;

        // ── Layout de tres paneles ────────────────────────────────

        private void BuildMainLayout()
        {
            var root  = CreateRootSplit();
            var right = CreateRightSplit();
            BuildSidebar(root.Panel1);
            BuildFileArea(right.Panel1);
            BuildDetailPanel(right.Panel2);
            root.Panel2.Controls.Add(right);
            Controls.Add(root);
        }

        private static SplitContainer CreateRootSplit()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 1,
                BackColor = Theme.Border,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
            };
            split.HandleCreated += (_, _) =>
            {
                try { if (split.Width > 300) split.SplitterDistance = 170; } catch { /* fallback silencioso intencional */ }
            };
            return split;
        }

        private static SplitContainer CreateRightSplit()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 1,
                BackColor = Theme.Border,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
            };
            split.HandleCreated += (_, _) =>
            {
                try { if (split.Width > 600) split.SplitterDistance = split.Width - 320; } catch { }
            };
            split.Resize += (_, _) =>
            {
                try { if (split.Width > 600) split.SplitterDistance = split.Width - 320; } catch { }
            };
            return split;
        }
        // ── Sidebar ───────────────────────────────────────────────

        private void BuildSidebar(SplitterPanel host)
        {
            var panel = new Panel
            { Dock = DockStyle.Fill, BackColor = Theme.BgPanel, Padding = new Padding(6, 10, 6, 10) };
            var flow = new FlowLayoutPanel
            { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
              WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };

            AddLocationShortcuts(flow);
            flow.Controls.Add(SectionHeader("HERRAMIENTAS"));
            AddToolShortcuts(flow);

            panel.Controls.Add(flow);
            host.Controls.Add(panel);
        }

        private void AddLocationShortcuts(FlowLayoutPanel flow)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var shortcuts = new[]
            {
                ("🏠  Inicio",     Folder(Environment.SpecialFolder.MyDocuments), (FileKind[]?)null),
                ("🖥  Escritorio", Folder(Environment.SpecialFolder.Desktop),     null),
                ("📄  Documentos", Folder(Environment.SpecialFolder.MyDocuments), new[]{ FileKind.Document, FileKind.Spreadsheet, FileKind.Code }),
                ("🖼  Imágenes",   Folder(Environment.SpecialFolder.MyPictures),  new[]{ FileKind.Image }),
                ("🎵  Música",     Folder(Environment.SpecialFolder.MyMusic),     new[]{ FileKind.Audio }),
                ("🎬  Videos",     Folder(Environment.SpecialFolder.MyVideos),    new[]{ FileKind.Video }),
                ("⬇  Descargas",  Path.Combine(home, "Downloads"),              null),
                ("💻  Este equipo","drives",                                     null),
            };
            foreach (var (label, path, filter) in shortcuts)
                flow.Controls.Add(SidebarButton(label, path, filter));
        }

        private void AddToolShortcuts(FlowLayoutPanel flow)
        {
            var tools = new (string label, Action action)[]
            {
                ("📧  Correo",       () => new EmailForm().Show()),
                ("📷  Cámara",       () => new CameraForm().Show()),
                ("🎙  Grabar audio", () => new RecorderForm().Show()),
                ("✨  Limpiar datos",() => new CleanerForm().Show()),
                ("📊  Graficador",   () => new ChartForm(SelectedItem?.FullPath).Show()),
                ("🔄  Convertir",    () => new ConverterForm(SelectedItem?.FullPath).Show()),
            };
            foreach (var (label, action) in tools)
                flow.Controls.Add(ToolButton(label, action));
        }

        private static string Folder(Environment.SpecialFolder f) =>
            Environment.GetFolderPath(f);

        // ── Área de archivos ──────────────────────────────────────

        private void BuildFileArea(SplitterPanel host)
        {
            var container = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMain };
            _fileGrid = BuildGridPanel();
            _fileList = BuildListPanel();
            container.Controls.Add(_fileList);
            container.Controls.Add(_fileGrid);
            host.Controls.Add(container);
        }

        private FlowLayoutPanel BuildGridPanel()
        {
            var grid = new FlowLayoutPanel
            { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgMain,
              Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight,
              WrapContents = true };
            grid.Click += (_, _) => ClearSelection();
            return grid;
        }

        private ListView BuildListPanel()
        {
            var list = new ListView
            { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
              MultiSelect = true, BackColor = Theme.BgMain, ForeColor = Theme.TxtMain,
              BorderStyle = BorderStyle.None, Font = Theme.FontNormal, Visible = false };
            list.Columns.Add("Nombre",    300);
            list.Columns.Add("Tipo",      100);
            list.Columns.Add("Tamaño",    90);
            list.Columns.Add("Modificado",140);
            list.SelectedIndexChanged += (_, _) => OnListSelectionChanged();
            list.DoubleClick          += (_, _) => OnListDoubleClick();
            list.MouseUp              += OnListRightClick;
            return list;
        }

        // ── Panel de detalles ─────────────────────────────────────

        private void BuildDetailPanel(SplitterPanel host)
        {
            _detailPane = new Panel
            { Dock = DockStyle.Fill, BackColor = Theme.BgPanel, Padding = new Padding(14) };

            var scroll = new Panel
            { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
            var inner  = new FlowLayoutPanel
            { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown,
              WrapContents = false, AutoSize = true, BackColor = Color.Transparent };

            _previewImage = new PictureBox
            { Width = 240, Height = 170, SizeMode = PictureBoxSizeMode.Zoom,
              BackColor = Theme.BgControl };
            _detailName = new Label
            { AutoSize = false, Width = 240, Height = 38, Font = Theme.FontBold,
              ForeColor = Theme.TxtMain, TextAlign = ContentAlignment.MiddleLeft };
            _detailKind = MetaLabel("Tipo");
            _detailSize = MetaLabel("Tamaño");
            _detailDate = MetaLabel("Modificado");
            _detailExt  = MetaLabel("Extensión");

            var actionsHeader = new Label
            { Text = "ACCIONES", ForeColor = Theme.TxtMuted,
              Font = new Font("Segoe UI", 8f, FontStyle.Bold),
              AutoSize = true, Padding = new Padding(0, 10, 0, 4) };
            _actionPanel = new FlowLayoutPanel
            { FlowDirection = FlowDirection.TopDown, WrapContents = false,
              AutoSize = true, BackColor = Color.Transparent };

            inner.Controls.AddRange(new Control[]
            { _previewImage, _detailName, _detailKind, _detailSize,
              _detailDate, _detailExt, actionsHeader, _actionPanel });
            scroll.Controls.Add(inner);
            _detailPane.Controls.Add(scroll);
            _detailPane.Controls.Add(BuildEmptyDetailLabel());
            ShowDetailPlaceholder(true);
            host.Controls.Add(_detailPane);
        }

        private static Label MetaLabel(string name) =>
            new() { Text = name + ":  —", AutoSize = false, Width = 240, Height = 18,
                    ForeColor = Theme.TxtSub, Font = Theme.FontSmall, Name = name };

        private static Label BuildEmptyDetailLabel() =>
            new() { Text = "👆\n\nSelecciona un archivo\npara ver sus propiedades",
                    ForeColor = Theme.TxtMuted, Font = Theme.FontNormal,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Name = "ph" };

        internal void ShowDetailPlaceholder(bool visible)
        {
            foreach (Control c in _detailPane.Controls)
                c.Visible = c.Name == "ph" ? visible : !visible;
        }

        internal void UpdateDetailFields(FileItem item)
        {
            _previewImage.Image = item.Thumbnail ?? RenderEmojiIcon(item);
            _detailName.Text    = item.Name;
            _detailKind.Text    = "Tipo:        " + item.KindText;
            _detailSize.Text    = "Tamaño:      " + item.SizeText;
            _detailDate.Text    = "Modificado:  " + item.ModifiedText;
            _detailExt.Text     = "Extensión:   " + (string.IsNullOrEmpty(item.Extension) ? "—" : item.Extension);
        }

        private static Bitmap RenderEmojiIcon(FileItem item)
        {
            const int Size = 120;
            var bmp = new Bitmap(Size, Size);
            using var g = Graphics.FromImage(bmp);
            using var f = new Font("Segoe UI", 44f);
            g.Clear(Theme.BgControl);
            var sz = g.MeasureString(item.Emoji, f);
            g.DrawString(item.Emoji, f, new SolidBrush(item.KindColor),
                (Size - sz.Width) / 2, (Size - sz.Height) / 2);
            return bmp;
        }

        // ── Botones de acciones ───────────────────────────────────

        internal void RebuildActionButtons()
        {
            _actionPanel.Controls.Clear();
            if (SelectedItem == null) return;
            AddCommonActions();
            AddTypeSpecificActions();
        }

        private void AddCommonActions()
        {
            AddAction("▶  Abrir",      () => OpenItem(SelectedItem!), accent: true);
            AddAction("✏  Renombrar",  () => RenameItem(SelectedItem!));
            AddAction("🗑  Eliminar",   () => DeleteItem(SelectedItem!));
            AddAction("📁  Mover a...", () => MoveItem(SelectedItem!));
            AddAction("🗄  → DB",       () => new DatabaseExportForm(SelectedItem!, Db).Show());
            AddAction("📧  Enviar por correo", () => OpenEmailWithAttachment(SelectedItem!.FullPath));
            var ext = System.IO.Path.GetExtension(SelectedItem!.FullPath).ToLowerInvariant();
            if (IsDocumentExtension(ext))
                AddAction("📄  Ver documento", () => Explorador_de_Archivo.DocumentViewer.Open(SelectedItem!.FullPath));
        }

        private static bool IsDocumentExtension(string ext) =>
            ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".rtf";

        private void AddTypeSpecificActions()
        {
            if (SelectedItem!.Kind == FileKind.Image)
            {
                AddAction("🖼  Editar imagen", () => new ImageEditorForm(SelectedItem.FullPath).Show());
                AddAction("📍  GPS",           () => new GeoImageForm(SelectedItem.FullPath).Show());
            }
            if (SelectedItem.Kind == FileKind.Audio)
                AddAction("🎵  Reproducir", () => new AudioPlayerForm(SelectedItem.FullPath).Show());
            if (SelectedItem.Kind == FileKind.Video)
                AddAction("🎬  Reproducir", () => new VideoPlayerForm(SelectedItem.FullPath).Show());
        }

        private void AddAction(string label, Action onClick, bool accent = false)
        {
            var btn = accent ? Theme.AccentBtn(label, 240, 30) : Theme.FlatBtn(label, 240, 30);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding   = new Padding(8, 0, 0, 0);
            btn.Margin    = new Padding(0, 2, 0, 2);
            btn.Click    += (_, _) => onClick();
            _actionPanel.Controls.Add(btn);
        }

        /// <summary>
        /// Abre el formulario de correo con el archivo seleccionado pre-adjuntado.
        /// </summary>
        private static void OpenEmailWithAttachment(string filePath)
        {
            var emailForm = new EmailForm();
            emailForm.PreAttachFile(filePath);
            emailForm.Show();
        }

        // ── Menú contextual ───────────────────────────────────────

        private ContextMenuStrip BuildContextMenu(FileItem item)
        {
            var menu = new ContextMenuStrip { BackColor = Theme.BgPanel, ForeColor = Theme.TxtMain };
            void Add(string text, Action action, bool sep = false)
            {
                if (sep) menu.Items.Add(new ToolStripSeparator());
                var mi = new ToolStripMenuItem(text)
                { BackColor = Theme.BgPanel, ForeColor = Theme.TxtMain };
                mi.Click += (_, _) => action();
                menu.Items.Add(mi);
            }
            Add("▶  Abrir",       () => OpenItem(item));
            Add("✏  Renombrar",   () => RenameItem(item),  true);
            Add("📋  Copiar",     () => Clipboard.Copy(item.FullPath));
            Add("✂  Cortar",      () => Clipboard.Cut(item.FullPath));
            Add("📄  Pegar aquí", PasteHere);
            Add("🗑  Eliminar",   () => DeleteItem(item),  true);
            Add("📁  Mover a...", () => MoveItem(item),    true);
            Add("🗄  → DB",       () => new DatabaseExportForm(item, Db).Show());
            AddTypeContextItems(menu, item, Add);
            return menu;
        }

        private static void AddTypeContextItems(ContextMenuStrip menu, FileItem item,
            Action<string, Action, bool> add)
        {
            if (item.Kind == FileKind.Image)
            {
                add("📍  Ver GPS",          () => new GeoImageForm(item.FullPath).Show(),    true);
                add("🖼  Editar imagen",     () => new ImageEditorForm(item.FullPath).Show(), false);
                add("📧  Enviar por correo", () => new EmailForm().Show(),                    false);
            }
            if (item.Kind == FileKind.Audio)
                add("🎵  Reproducir", () => new AudioPlayerForm(item.FullPath).Show(), true);
            if (item.Kind == FileKind.Video)
                add("🎬  Reproducir", () => new VideoPlayerForm(item.FullPath).Show(), true);
        }

        // ── Helpers de UI ─────────────────────────────────────────

        internal void ToggleView()
        {
            IsGridView        = !IsGridView;
            _fileGrid.Visible = IsGridView;
            _fileList.Visible = !IsGridView;
            Reload();
        }

        private Button SidebarButton(string label, string path, FileKind[]? filter = null)
        {
            var btn = new Button
            { Text = label, Width = 162, Height = 30, FlatStyle = FlatStyle.Flat,
              BackColor = Color.Transparent, ForeColor = Theme.TxtSub, Font = Theme.FontNormal,
              TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0),
              Cursor = Cursors.Hand, Tag = path };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.BgHover;
            btn.Click += (_, _) => { ActiveFilter = filter; Navigate((string)btn.Tag!); };
            return btn;
        }

        private Button ToolButton(string label, Action action)
        {
            var btn = SidebarButton(label, "");
            btn.Click += (_, _) => action();
            return btn;
        }

        private static Label SectionHeader(string title) =>
            new() { Text = title, ForeColor = Theme.TxtMuted,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    AutoSize = false, Width = 162, Height = 20,
                    Padding = new Padding(4, 6, 0, 0), BackColor = Color.Transparent };

        internal void OnListSelectionChanged()
        {
            if (_fileList.SelectedItems.Count > 0 &&
                _fileList.SelectedItems[0].Tag is FileItem item)
                SelectItem(item);
        }

        internal void OnListDoubleClick()
        {
            if (_fileList.SelectedItems.Count > 0 &&
                _fileList.SelectedItems[0].Tag is FileItem item)
                OpenItem(item);
        }

        internal void OnListRightClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _fileList.HitTest(e.Location);
            if (hit.Item?.Tag is not FileItem item) return;
            SelectItem(item);
            BuildContextMenu(item).Show(_fileList, e.Location);
        }

        internal ListView  FileList  => _fileList;
        internal FlowLayoutPanel FileGrid => _fileGrid;
    }
}
