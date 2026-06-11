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
    public class CleanerForm : Form
    {
        private RichTextBox _inputText  = null!, _outputText = null!;
        private CheckBox    _trimOption = null!, _emptyOption    = null!,
                            _dupeOption = null!, _emailOption    = null!,
                            _caseOption = null!;
        private Label       _statsLabel = null!;

        private static readonly Regex NullValuePattern  = new(@":\s*(None|null|NULL)\b", RegexOptions.IgnoreCase);
        private static readonly Regex InvalidAgePattern = new(@"'edad'\s*:\s*(-\d+|999|1000)", RegexOptions.IgnoreCase);
        private static readonly Regex InvalidNamePattern= new(@"'nombre'\s*:\s*('[^a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{0,3}'|'')", RegexOptions.IgnoreCase);
        private static readonly Regex IdPattern         = new(@"'id'\s*:\s*\d+\s*,?\s*");
        private static readonly Regex EmailPattern      = new(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}");
        private static readonly Regex EmailFieldPattern = new(@"'(?:email|correo)'\s*:\s*'([^']*)'", RegexOptions.IgnoreCase);

        public CleanerForm()
        {
            Theme.ApplyForm(this);
            Text = "Limpiar Datos";
            Size = new Size(880, 580);
            BuildUI();
        }

        private void BuildUI()
        {
            // Fill al final para que respete Top
            var editor = BuildEditorArea();
            var options = BuildOptionsPanel();
            var toolbar = BuildToolbar();
            // Wrapper con padding superior
            var wrapper = new Panel
            { Dock = DockStyle.Fill, BackColor = Theme.BgMain, Padding = new Padding(0, 8, 0, 0) };
            wrapper.Controls.Add(editor);
            Controls.Add(wrapper);
            Controls.Add(options);
            Controls.Add(toolbar);
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var bar = Toolbar(DockStyle.Top, 46);
            _statsLabel = new Label { ForeColor = Theme.Green, Font = Theme.FontSmall, AutoSize = true };
            bar.Controls.AddRange(new Control[]
            {
                ActionButton("✨ Limpiar", 120, CleanData, accent: true),
                ActionButton("📂 Cargar",  90, LoadFile),
                ActionButton("💾 Guardar", 100, SaveFile),
                _statsLabel,
            });
            return bar;
        }

        private FlowLayoutPanel BuildOptionsPanel()
        {
            var panel = new FlowLayoutPanel
            { Dock = DockStyle.Top, Height = 34, BackColor = Theme.BgControl,
              Padding = new Padding(10, 7, 0, 0) };
            _trimOption  = CreateOption("Eliminar espacios",    isChecked: true);
            _emptyOption = CreateOption("Quitar líneas vacías", isChecked: true);
            _dupeOption  = CreateOption("Eliminar duplicados",  isChecked: true);
            _emailOption = CreateOption("Validar emails");
            _caseOption  = CreateOption("Corregir mayúsculas");
            panel.Controls.AddRange(new Control[]
            { _trimOption, _emptyOption, _dupeOption, _emailOption, _caseOption });
            return panel;
        }

        private static CheckBox CreateOption(string label, bool isChecked = false)
        {
            var box = Theme.CheckBox(label);
            box.Checked = isChecked;
            return box;
        }

        private SplitContainer BuildEditorArea()
        {
            var split = VerticalSplit(DockStyle.Fill);
            _inputText  = EditorBox(split.Panel1);
            _outputText = EditorBox(split.Panel2, readOnly: true);
            return split;
        }

        private void LoadFile()
        {
            var path = FilePicker.Open(filter: ".txt,.csv,.json,.xml");
            if (path != null) _inputText.Text = File.ReadAllText(path);
        }

        private void SaveFile()
        {
            if (string.IsNullOrWhiteSpace(_outputText.Text)) return;
            var savePath = FilePicker.Save(defaultName: "datos_limpios.txt");
            if (savePath != null)
                File.WriteAllText(savePath, _outputText.Text);
        }

        private void CleanData()
        {
            if (string.IsNullOrWhiteSpace(_inputText.Text)) return;
            var lines = SplitIntoLines(_inputText.Text);
            lines     = ApplyTrimming(lines);
            var (afterEmpty, emptyRemoved)     = RemoveEmptyLines(lines);
            var (afterInvalid, invalidRemoved) = RemoveInvalidEntries(afterEmpty);
            var (afterDupes, dupesRemoved)     = RemoveDuplicates(afterInvalid);
            var (afterEmail, badEmails)        = MarkInvalidEmails(afterDupes);
            var finalLines = ApplyCaseCorrection(afterEmail);
            _outputText.Text  = string.Join(Environment.NewLine, finalLines);
            _statsLabel.Text  = BuildStatsText(invalidRemoved, dupesRemoved, emptyRemoved, badEmails);
        }

        private static List<string> SplitIntoLines(string text) =>
            text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

        private List<string> ApplyTrimming(List<string> lines) =>
            _trimOption.Checked ? lines.Select(l => l.Trim()).ToList() : lines;

        private (List<string> lines, int removed) RemoveEmptyLines(List<string> lines)
        {
            if (!_emptyOption.Checked) return (lines, 0);
            var filtered = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            return (filtered, lines.Count - filtered.Count);
        }

        private static (List<string> lines, int removed) RemoveInvalidEntries(List<string> lines)
        {
            var valid   = lines.Where(IsValidEntry).ToList();
            return (valid, lines.Count - valid.Count);
        }

        private static bool IsValidEntry(string line) =>
            !NullValuePattern.IsMatch(line) &&
            !InvalidAgePattern.IsMatch(line) &&
            !InvalidNamePattern.IsMatch(line);

        private (List<string> lines, int removed) RemoveDuplicates(List<string> lines)
        {
            if (!_dupeOption.Checked) return (lines, 0);
            var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = lines.Where(l => seen.Add(NormalizeForDedup(l))).ToList();
            return (unique, lines.Count - unique.Count);
        }

        private static string NormalizeForDedup(string line) =>
            IdPattern.Replace(line, "").Trim().Trim('{', '}').Trim();

        private (List<string> lines, int badCount) MarkInvalidEmails(List<string> lines)
        {
            if (!_emailOption.Checked) return (lines, 0);
            int badCount = 0;
            var marked   = lines.Select(l => MarkEmailIfInvalid(l, ref badCount)).ToList();
            return (marked, badCount);
        }

        private static string MarkEmailIfInvalid(string line, ref int badCount)
        {
            var match = EmailFieldPattern.Match(line);
            if (!match.Success) return line;
            var emailValue = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(emailValue) || EmailPattern.IsMatch(emailValue)) return line;
            badCount++;
            return line.Replace(match.Value, match.Value + " [EMAIL INVÁLIDO]");
        }

        private List<string> ApplyCaseCorrection(List<string> lines) =>
            _caseOption.Checked
                ? lines.Select(l => l.Length > 0 ? char.ToUpper(l[0]) + l[1..] : l).ToList()
                : lines;

        private static string BuildStatsText(int invalid, int dupes, int empty, int badEmails) =>
            $"✓  Inválidos: {invalid}  |  Duplicados: {dupes}  |  Vacíos: {empty}  |  Emails inv.: {badEmails}";
    }

    // ── Delegación a UiHelpers (compatibilidad) ───────────────────
}
