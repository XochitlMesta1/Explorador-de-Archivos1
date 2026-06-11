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
public class DocumentConverterForm : Form
{
    private TextBox     _originPath   = null!;
    private ComboBox    _targetFormat = null!;
    private Button      _convertBtn   = null!;
    private ProgressBar _progress     = null!;
    private Label       _statusLabel  = null!;
    private ListBox     _logList      = null!;

    // Formatos soportados según el origen
    private static readonly string[] WordFormats  = { "PDF", "DOCX", "RTF", "TXT", "HTML", "XLSX", "PPTX" };
    private static readonly string[] ExcelFormats = { "PDF", "CSV", "TXT", "DOCX", "PPTX", "XLSX" };
    private static readonly string[] PptFormats   = { "PDF", "DOCX", "PPTX" };
    private static readonly string[] RtfFormats   = { "PDF", "DOCX", "TXT" };
    private static readonly string[] PdfFormats   = { "DOCX", "TXT", "XLSX", "PPTX" };

    public DocumentConverterForm(string? initialPath = null)
    {
        Theme.ApplyForm(this);
        Text        = "Convertidor de Documentos Office";
        Size        = new Size(640, 480);
        MinimumSize = new Size(540, 420);
        BuildUI();
        if (!string.IsNullOrEmpty(initialPath) && File.Exists(initialPath))
            SetOriginFile(initialPath);
    }

    private void BuildUI()
    {
        var layout = BuildLayout();
        layout.Controls.Add(BuildTitle(),       0, 0);
        layout.Controls.Add(BuildOriginRow(),   0, 1);
        layout.Controls.Add(BuildFormatRow(),   0, 2);
        layout.Controls.Add(BuildActionRow(),   0, 3);
        layout.Controls.Add(BuildStatusRow(),   0, 4);
        layout.Controls.Add(BuildLogList(),     0, 5);
        Controls.Add(layout);
    }

    private static TableLayoutPanel BuildLayout()
    {
        var layout = new TableLayoutPanel
        { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
          Padding = new Padding(18, 14, 18, 14), BackColor = Theme.BgMain };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return layout;
    }

    private static Label BuildTitle() =>
        new() { Text = "Convertidor Office — Requiere Microsoft Word/Excel instalado",
                Font = Theme.FontBold, ForeColor = Theme.TxtMain, AutoSize = true };

    private Panel BuildOriginRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(new Label
        { Text = "Archivo de origen", ForeColor = Theme.TxtMuted,
          Font = Theme.FontSmall, AutoSize = true, Top = 0 });
        _originPath          = Theme.TextBox(false, 28);
        _originPath.Dock     = DockStyle.Bottom;
        _originPath.ReadOnly = true;
        _originPath.TextChanged += (_, _) => RefreshAvailableFormats();
        var browseBtn = Theme.FlatBtn("📂", 32, 28);
        browseBtn.Dock   = DockStyle.Right;
        browseBtn.Click += (_, _) => BrowseOriginFile();
        panel.Controls.Add(_originPath);
        panel.Controls.Add(browseBtn);
        return panel;
    }

    private Panel BuildFormatRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(new Label
        { Text = "Formato de destino", ForeColor = Theme.TxtMuted,
          Font = Theme.FontSmall, AutoSize = true, Top = 0 });
        _targetFormat      = Theme.ComboBox();
        _targetFormat.Dock = DockStyle.Bottom;
        _targetFormat.Width = 220;
        panel.Controls.Add(_targetFormat);
        return panel;
    }

    private Panel BuildActionRow()
    {
        var panel = new FlowLayoutPanel
        { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
        _convertBtn         = Theme.AccentBtn("🔄 Convertir", 140, 32);
        _convertBtn.Click  += (_, _) => StartConversion();
        _convertBtn.Enabled = false;
        _progress = new ProgressBar
        { Width = 200, Height = 22, Style = ProgressBarStyle.Marquee, Visible = false };
        panel.Controls.AddRange(new Control[] { _convertBtn, _progress });
        return panel;
    }

    private Panel BuildStatusRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = Theme.TxtMuted, Font = Theme.FontSmall };
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private ListBox BuildLogList()
    {
        _logList = new ListBox
        { Dock = DockStyle.Fill, BackColor = Theme.BgPanel,
          ForeColor = Theme.TxtSub, Font = Theme.FontMono,
          BorderStyle = BorderStyle.FixedSingle };
        return _logList;
    }

    private void BrowseOriginFile()
    {
        var path = FilePicker.Open(filter: ".docx,.doc,.xlsx,.xls,.pptx,.ppt,.pdf,.rtf");
        if (path != null) SetOriginFile(path);
    }

    private void SetOriginFile(string filePath)
    {
        _originPath.Text = filePath;
        RefreshAvailableFormats();
    }

    private void RefreshAvailableFormats()
    {
        _targetFormat.Items.Clear();
        var ext       = Path.GetExtension(_originPath.Text).ToLowerInvariant();
        var available = ext switch
        {
            ".docx" or ".doc"   => WordFormats,
            ".xlsx" or ".xls"   => ExcelFormats,
            ".pptx" or ".ppt"   => PptFormats,
            ".rtf"              => RtfFormats,
            ".pdf"              => PdfFormats,
            _                   => Array.Empty<string>()
        };
        _targetFormat.Items.AddRange(available);
        if (_targetFormat.Items.Count > 0) _targetFormat.SelectedIndex = 0;
        _convertBtn.Enabled = _targetFormat.Items.Count > 0 && File.Exists(_originPath.Text);
    }

    private void StartConversion()
    {
        var target = $"{_targetFormat.SelectedItem}".ToLower();
        var savePath = FilePicker.Save(
            defaultName: Path.GetFileNameWithoutExtension(_originPath.Text) + "." + target);
        if (savePath == null) return;

        SetConvertingState(converting: true);
        Log("Convirtiendo " + Path.GetFileName(_originPath.Text) + " → ." + target);

        var origin      = _originPath.Text;
        var destination = savePath;
        Task.Run(() => RunConversion(origin, destination, target));
    }

    private void RunConversion(string origin, string destination, string targetExt)
    {
        try
        {
            OfficeConverter.Convert(origin, destination, targetExt);
            BeginInvoke(() => OnConversionSuccess(destination));
        }
        catch (Exception ex) { BeginInvoke(() => OnConversionFailed(ex.Message)); }
    }

    private void OnConversionSuccess(string destination)
    {
        SetConvertingState(converting: false);
        _statusLabel.ForeColor = Theme.Green;
        _statusLabel.Text      = "✓ Listo";
        Log("✓ " + Path.GetFileName(destination));
        if (MessageBox.Show("¿Abrir archivo convertido?", "Listo",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = destination, UseShellExecute = true });
    }

    private void OnConversionFailed(string error)
    {
        SetConvertingState(converting: false);
        _statusLabel.ForeColor = Theme.Red;
        _statusLabel.Text      = "Error: " + error;
        Log("✗ " + error);
    }

    private void SetConvertingState(bool converting)
    {
        _convertBtn.Enabled = !converting;
        _progress.Visible   = converting;
    }

    private void Log(string message) =>
        _logList.Items.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
}

// ── Servicio de conversión Office via COM Interop ─────────────────
internal static class OfficeConverter
{
    public static void Convert(string origin, string destination, string targetExt)
    {
        var sourceExt = Path.GetExtension(origin).ToLowerInvariant();

        if (IsWordSource(sourceExt))      { ConvertWithWord(origin, destination, targetExt);       return; }
        if (IsExcelSource(sourceExt))     { ConvertWithExcel(origin, destination, targetExt);      return; }
        if (IsPowerPointSource(sourceExt)){ ConvertWithPowerPoint(origin, destination, targetExt); return; }
        if (sourceExt == ".pdf")          { ConvertFromPdf(origin, destination, targetExt);        return; }

        throw new NotSupportedException("Formato no soportado: " + sourceExt);
    }

    private static bool IsWordSource(string ext)       => ext is ".docx" or ".doc" or ".rtf";
    private static bool IsExcelSource(string ext)      => ext is ".xlsx" or ".xls";
    private static bool IsPowerPointSource(string ext) => ext is ".pptx" or ".ppt";

    private static void ConvertWithWord(string origin, string destination, string targetExt)
    {
        // Formatos directos vía Word COM
        const int PdfFormat  = 17, DocxFormat = 16, RtfFormat = 6, TxtFormat = 2, HtmlFormat = 8;

        if (targetExt is "pdf" or "docx" or "rtf" or "txt" or "html")
        {
            int format = targetExt switch
            {
                "pdf"  => PdfFormat,  "docx" => DocxFormat,
                "rtf"  => RtfFormat,  "txt"  => TxtFormat,
                "html" => HtmlFormat, _      => PdfFormat,
            };
            RunWithComObject("Word.Application", word =>
            {
                dynamic doc = word.Documents.Open(origin, ReadOnly: true);
                doc.SaveAs2(destination, FileFormat: format);
                doc.Close(false);
            });
            return;
        }

        // Word → XLSX: extraer texto y poner en una columna
        if (targetExt == "xlsx")
        {
            string text = "";
            RunWithComObject("Word.Application", word =>
            {
                dynamic doc = word.Documents.Open(origin, ReadOnly: true);
                text = doc.Content.Text;
                doc.Close(false);
            });
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Contenido");
            foreach (var line in text.Split('\n', '\r'))
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine("\"" + line.Trim().Replace("\"", "\"\"") + "\"");
            var tempCsv = Path.ChangeExtension(destination, ".csv.tmp");
            File.WriteAllText(tempCsv, sb.ToString());
            try
            {
                RunWithComObject("Excel.Application", excel =>
                {
                    dynamic wb = excel.Workbooks.Open(tempCsv);
                    wb.SaveAs(destination, FileFormat: 51);
                    wb.Close(false);
                });
            }
            finally { try { File.Delete(tempCsv); } catch { } }
            return;
        }

        // Word → PPTX: cada párrafo (limitado) se convierte en una diapositiva
        if (targetExt == "pptx")
        {
            var tempPdf = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(origin) + ".pdf");
            try
            {
                RunWithComObject("Word.Application", word =>
                {
                    dynamic doc = word.Documents.Open(origin, ReadOnly: true);
                    doc.SaveAs2(tempPdf, FileFormat: PdfFormat);
                    doc.Close(false);
                });
                ConvertPdfToPptx(tempPdf, destination);
            }
            finally { try { File.Delete(tempPdf); } catch { } }
            return;
        }

        throw new NotSupportedException("Word → " + targetExt + " no soportado");
    }

    private static void ConvertWithExcel(string origin, string destination, string targetExt)
    {
        // Conversiones directas vía Excel COM
        if (targetExt == "pdf" || targetExt == "csv" || targetExt == "txt" || targetExt == "xlsx")
        {
            RunWithComObject("Excel.Application", excel =>
            {
                dynamic wb = excel.Workbooks.Open(origin, ReadOnly: true);
                if (targetExt == "pdf")  wb.ExportAsFixedFormat(0, destination);
                else if (targetExt == "csv") wb.SaveAs(destination, FileFormat: 6);
                else if (targetExt == "txt") wb.SaveAs(destination, FileFormat: 20);
                else wb.SaveAs(destination, FileFormat: 51);
                wb.Close(false);
            });
            return;
        }

        // Excel → DOCX/PPTX vía PDF intermedio (mejor aproximación posible)
        if (targetExt == "docx" || targetExt == "pptx")
        {
            var tempPdf = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(origin) + ".pdf");
            try
            {
                RunWithComObject("Excel.Application", excel =>
                {
                    dynamic wb = excel.Workbooks.Open(origin, ReadOnly: true);
                    wb.ExportAsFixedFormat(0, tempPdf);
                    wb.Close(false);
                });
                if (targetExt == "docx") ConvertWithWord(tempPdf, destination, "docx");
                else                     ConvertPdfToPptx(tempPdf, destination);
            }
            finally { try { File.Delete(tempPdf); } catch { } }
            return;
        }

        throw new NotSupportedException("Excel → " + targetExt + " no soportado");
    }

    private static void ConvertWithPowerPoint(string origin, string destination, string targetExt)
    {
        // PPT → PDF/PPTX directo
        if (targetExt == "pdf" || targetExt == "pptx")
        {
            RunWithComObject("PowerPoint.Application", ppt =>
            {
                dynamic pres = ppt.Presentations.Open(origin, ReadOnly: true, WithWindow: false);
                if (targetExt == "pdf") pres.SaveAs(destination, 32);  // ppSaveAsPDF
                else                    pres.SaveAs(destination, 24);  // ppSaveAsOpenXMLPresentation
                pres.Close();
            });
            return;
        }

        // PPT → DOCX vía PDF intermedio
        if (targetExt == "docx")
        {
            var tempPdf = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(origin) + ".pdf");
            try
            {
                RunWithComObject("PowerPoint.Application", ppt =>
                {
                    dynamic pres = ppt.Presentations.Open(origin, ReadOnly: true, WithWindow: false);
                    pres.SaveAs(tempPdf, 32);
                    pres.Close();
                });
                ConvertWithWord(tempPdf, destination, "docx");
            }
            finally { try { File.Delete(tempPdf); } catch { } }
            return;
        }

        throw new NotSupportedException("PowerPoint → " + targetExt + " no soportado");
    }

    private static void ConvertFromPdf(string origin, string destination, string targetExt)
    {
        if (targetExt == "txt")
        {
            File.WriteAllText(destination, ExtractPdfText(origin), System.Text.Encoding.UTF8);
            return;
        }
        if (targetExt == "docx")
        {
            ConvertWithWord(origin, destination, "docx");
            return;
        }
        if (targetExt == "xlsx")
        {
            // PDF → DOCX → CSV mejor que nada; o pegar texto en una sola columna
            var text = ExtractPdfText(origin);
            var lines = text.Split('\n');
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Contenido");
            foreach (var line in lines) sb.AppendLine("\"" + line.Replace("\"", "\"\"") + "\"");
            var tempCsv = Path.ChangeExtension(destination, ".csv.tmp");
            File.WriteAllText(tempCsv, sb.ToString());
            try
            {
                RunWithComObject("Excel.Application", excel =>
                {
                    dynamic wb = excel.Workbooks.Open(tempCsv);
                    wb.SaveAs(destination, FileFormat: 51);
                    wb.Close(false);
                });
            }
            finally { try { File.Delete(tempCsv); } catch { } }
            return;
        }
        if (targetExt == "pptx")
        {
            ConvertPdfToPptx(origin, destination);
            return;
        }
        throw new NotSupportedException("PDF → " + targetExt + " no soportado");
    }

    /// <summary>
    /// PDF → PPTX: cada página del PDF se convierte en una diapositiva con el texto extraído.
    /// </summary>
    private static void ConvertPdfToPptx(string origin, string destination)
    {
        var text = ExtractPdfText(origin);
        var pages = text.Split(new[] { "\f", "\n\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (pages.Length == 0) pages = new[] { text };

        RunWithComObject("PowerPoint.Application", ppt =>
        {
            dynamic pres = ppt.Presentations.Add(0);  // -1 = msoFalse (sin ventana)
            foreach (var pageContent in pages)
            {
                dynamic slide = pres.Slides.Add(pres.Slides.Count + 1, 2);  // ppLayoutText
                if (slide.Shapes.Count > 0)
                    try { slide.Shapes[2].TextFrame.TextRange.Text = pageContent.Trim(); }
                    catch { }
            }
            pres.SaveAs(destination, 24);  // ppSaveAsOpenXMLPresentation
            pres.Close();
        });
    }

    private static void RunWithComObject(string progId, Action<dynamic> action)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new InvalidOperationException(
                progId + " no encontrado. Instala Microsoft Office.");
        dynamic app = Activator.CreateInstance(type)!;
        app.Visible = false; app.DisplayAlerts = 0;
        try   { action(app); }
        finally
        {
            try { app.Quit(false); } catch { }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
        }
    }

    private static string ExtractPdfText(string path)
    {
        var raw    = File.ReadAllText(path, System.Text.Encoding.Latin1);
        var result = new System.Text.StringBuilder();
        foreach (System.Text.RegularExpressions.Match m in
            new System.Text.RegularExpressions.Regex(@"\(([^)]{2,})\)").Matches(raw))
        {
            var text = m.Groups[1].Value;
            if (!text.Contains("\\n") && !text.StartsWith("/"))
                result.AppendLine(text);
        }
        return result.Length > 0 ? result.ToString() : "(No se pudo extraer texto)";
    }
}
}
