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
    // ── Convertidor de datos (JSON / CSV / XML / TXT) ─────────────
    public class ConverterForm : Form
    {
        private readonly ConverterService _converter = new();

        private RichTextBox _inputBox    = null!, _outputBox = null!;
        private ComboBox    _fromFormat  = null!, _toFormat  = null!;
        private Label       _statusLabel = null!;

        private static readonly string[] InputFormats  = { "JSON", "CSV", "XML", "TXT" };
        private static readonly string[] OutputFormats = { "JSON", "CSV", "XML", "TXT" };

        public ConverterForm(string? initialFilePath = null)
        {
            Theme.ApplyForm(this);
            Text = "Convertir Datos";
            Size = new Size(840, 560);
            BuildUI();
            TryPreloadFile(initialFilePath);
        }

        private void BuildUI()
        {
            // 1) Status bar abajo (Dock = Bottom debe agregarse antes que Fill)
            _statusLabel = new Label
            { Dock = DockStyle.Bottom, Height = 22, ForeColor = Theme.TxtMuted,
              Font = Theme.FontSmall, TextAlign = ContentAlignment.MiddleLeft,
              Padding = new Padding(8, 0, 0, 0) };
            Controls.Add(_statusLabel);

            // 2) Toolbar arriba
            var toolbar = Toolbar(DockStyle.Top, 48);
            _fromFormat = FormatSelector(toolbar, InputFormats);
            toolbar.Controls.Add(ArrowLabel());
            _toFormat = FormatSelector(toolbar, OutputFormats);
            toolbar.Controls.AddRange(new Control[]
            {
                ActionButton("🔄 Convertir", 120, RunConversion),
                ActionButton("📂 Cargar",     90, LoadFromFile),
                ActionButton("💾 Guardar",    130, SaveToFile),
            });
            Controls.Add(toolbar);

            // 3) Contenido al centro con padding superior
            var wrapper = new Panel
            { Dock = DockStyle.Fill, BackColor = Theme.BgMain, Padding = new Padding(8, 12, 8, 8) };
            var split = VerticalSplit(DockStyle.Fill);
            _inputBox  = EditorBox(split.Panel1);
            _outputBox = EditorBox(split.Panel2, readOnly: true);
            _inputBox.ScrollBars  = RichTextBoxScrollBars.Both;
            _outputBox.ScrollBars = RichTextBoxScrollBars.Both;
            _inputBox.WordWrap    = false;
            _outputBox.WordWrap   = false;
            wrapper.Controls.Add(split);
            Controls.Add(wrapper);
        }

        private void TryPreloadFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            _inputBox.Text = File.ReadAllText(filePath);
            var ext = Path.GetExtension(filePath).TrimStart('.').ToUpper();
            if (_fromFormat.Items.Contains(ext)) _fromFormat.SelectedItem = ext;
        }

        private void RunConversion()
        {
            try
            {
                _outputBox.Text = _converter.Convert(_inputBox.Text, SelectedFrom, SelectedTo);
                ShowSuccess(_statusLabel, $"✓ {_outputBox.Text.Length} caracteres");
            }
            catch (Exception ex) { ShowError(_statusLabel, ex.Message); }
        }

        private void LoadFromFile()
        {
            var path = FilePicker.Open(filter: ".json,.csv,.xml,.txt");
            if (path == null) return;
            _inputBox.Text = File.ReadAllText(path);
            _statusLabel.Text = "Cargado: " + path;
        }

        private void SaveToFile()
        {
            if (string.IsNullOrWhiteSpace(_outputBox.Text)) return;
            var ext = SelectedTo.ToLower();
            var savePath = FilePicker.Save(defaultName: "datos." + ext);
            if (savePath == null) return;
            File.WriteAllText(savePath, _outputBox.Text);
            _statusLabel.Text = "Guardado: " + savePath;
        }

        private string SelectedFrom => $"{_fromFormat.SelectedItem}";
        private string SelectedTo   => $"{_toFormat.SelectedItem}";
    }

    // ── Selector de archivos interno ──────────────────────────────
}
