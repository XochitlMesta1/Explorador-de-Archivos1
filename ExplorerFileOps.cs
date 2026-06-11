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
    public class EmailForm : Form
    {
        private TextBox     _fromAddress = null!, _toAddress  = null!,
                            _subject     = null!, _smtpHost   = null!,
                            _smtpPort    = null!, _appPassword = null!;
        private RichTextBox _messageBody  = null!;
        private Label       _statusLabel  = null!;
        private string?     _attachmentPath;

        public EmailForm()
        {
            Theme.ApplyForm(this);
            Text        = "Correo Electrónico";
            Size        = new Size(700, 600);
            MinimumSize = new Size(600, 500);
            BuildUI();
        }

        /// <summary>
        /// Pre-adjunta un archivo al correo (usado al hacer clic en "Enviar por correo"
        /// desde el panel de detalles del explorador).
        /// </summary>
        public void PreAttachFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            _attachmentPath = filePath;
            if (_statusLabel != null)
                ShowSuccess(_statusLabel, "📎 Adjuntado: " + Path.GetFileName(filePath));
        }

        private void BuildUI()
        {
            var layout = BuildLayout();
            layout.Controls.Add(BuildTitle(), 0, 0);
            layout.Controls.Add(BuildAddressField("De (Gmail)", ref _fromAddress!, "tu@gmail.com"), 0, 1);
            layout.Controls.Add(BuildAddressField("Para",       ref _toAddress!,   ""),             0, 2);
            layout.Controls.Add(BuildAddressField("Asunto",     ref _subject!,     ""),             0, 3);
            layout.Controls.Add(BuildMessageArea(),    0, 4);
            layout.Controls.Add(BuildSmtpConfigRow(),  0, 5);
            layout.Controls.Add(BuildActionRow(),      0, 6);
            _statusLabel = new Label
            { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall,
              AutoSize = false, Dock = DockStyle.Fill };
            layout.Controls.Add(_statusLabel, 0, 7);
            Controls.Add(layout);
        }

        private static TableLayoutPanel BuildLayout()
        {
            var layout = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
              Padding = new Padding(16, 12, 16, 12), BackColor = Theme.BgMain };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var height in new[] { 26, 50, 50, 50, 0, 46, 38, 24 })
                layout.RowStyles.Add(height == 0
                    ? new RowStyle(SizeType.Percent, 100)
                    : new RowStyle(SizeType.Absolute, height));
            return layout;
        }

        private static Label BuildTitle() =>
            new() { Text = "Nuevo correo", Font = Theme.FontTitle,
                    ForeColor = Theme.TxtMain, AutoSize = true };

        private static Panel BuildAddressField(string label, ref TextBox field, string defaultValue)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            panel.Controls.Add(new Label
            { Text = label, ForeColor = Theme.TxtMuted, Font = Theme.FontSmall,
              AutoSize = true, Top = 0 });
            field = Theme.TextBox(false, 28);
            field.Text = defaultValue;
            field.Dock = DockStyle.Bottom;
            panel.Controls.Add(field);
            return panel;
        }

        private Panel BuildMessageArea()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            panel.Controls.Add(new Label
            { Text = "Mensaje", ForeColor = Theme.TxtMuted, Font = Theme.FontSmall,
              AutoSize = true, Top = 0 });
            _messageBody = new RichTextBox
            { BackColor = Theme.BgControl, ForeColor = Theme.TxtMain,
              BorderStyle = BorderStyle.FixedSingle, Font = Theme.FontNormal,
              Dock = DockStyle.Fill };
            panel.Controls.Add(_messageBody);
            return panel;
        }

        private Panel BuildSmtpConfigRow()
        {
            var panel = new FlowLayoutPanel
            { Dock = DockStyle.Fill, BackColor = Theme.BgPanel,
              Padding = new Padding(8, 4, 8, 4) };

            panel.Controls.Add(SmtpLabel("Servidor:"));
            _smtpHost = SmtpField("smtp.gmail.com", 160);
            panel.Controls.Add(_smtpHost);

            panel.Controls.Add(SmtpLabel("  Puerto:"));
            _smtpPort = SmtpField("587", 50);
            panel.Controls.Add(_smtpPort);

            panel.Controls.Add(SmtpLabel("  Contraseña app:"));
            _appPassword = SmtpField("", 160);
            _appPassword.PasswordChar = '●';
            panel.Controls.Add(_appPassword);

            var helpButton = Theme.FlatBtn("?", 24, 26);
            helpButton.ForeColor = Color.FromArgb(0, 120, 212);
            helpButton.Click += (_, _) => ShowGmailHelp();
            panel.Controls.Add(helpButton);
            return panel;
        }

        private static Label SmtpLabel(string text) =>
            new() { Text = text, ForeColor = Theme.TxtMuted,
                    Font = Theme.FontSmall, AutoSize = true };

        private static TextBox SmtpField(string defaultValue, int width)
        {
            var field = Theme.TextBox(false, 26);
            field.Text  = defaultValue;
            field.Width = width;
            return field;
        }

        private Panel BuildActionRow()
        {
            var row = new FlowLayoutPanel
            { Dock = DockStyle.Fill, BackColor = Color.Transparent,
              Padding = new Padding(0, 4, 0, 0) };
            row.Controls.AddRange(new Control[]
            {
                ActionButton("📤 Enviar",   120, SendEmail, accent: true),
                ActionButton("📎 Adjuntar", 110, AttachFile),
                ActionButton("🗑 Limpiar",  100, ClearForm),
            });
            return row;
        }

        private void SendEmail()
        {
            if (!AreRequiredFieldsFilled()) return;
            _statusLabel.Text = "Enviando...";
            try
            {
                using var message = BuildMailMessage();
                using var client  = BuildSmtpClient();
                Cursor = Cursors.WaitCursor;
                client.Send(message);
                Cursor = Cursors.Default;
                HandleEmailSent();
            }
            catch (Exception ex)
            { Cursor = Cursors.Default; ShowError(_statusLabel, ex.Message); }
        }

        private bool AreRequiredFieldsFilled()
        {
            if (string.IsNullOrWhiteSpace(_toAddress.Text) || string.IsNullOrWhiteSpace(_subject.Text))
            { MessageBox.Show("Completa Para y Asunto."); return false; }
            if (string.IsNullOrEmpty(_fromAddress.Text) || string.IsNullOrEmpty(_appPassword.Text))
            { MessageBox.Show("Ingresa tu correo y contraseña de aplicación."); return false; }
            return true;
        }

        private System.Net.Mail.MailMessage BuildMailMessage()
        {
            var mail    = new System.Net.Mail.MailMessage();
            mail.From   = new System.Net.Mail.MailAddress(_fromAddress.Text.Trim());
            mail.To.Add(_toAddress.Text.Trim());
            mail.Subject = _subject.Text.Trim();
            mail.Body    = _messageBody.Text.Trim();
            if (!string.IsNullOrEmpty(_attachmentPath) && File.Exists(_attachmentPath))
                mail.Attachments.Add(new System.Net.Mail.Attachment(_attachmentPath));
            return mail;
        }

        private System.Net.Mail.SmtpClient BuildSmtpClient() =>
            new(_smtpHost.Text.Trim())
            {
                Port           = int.TryParse(_smtpPort.Text, out var p) ? p : 587,
                Credentials    = new System.Net.NetworkCredential(
                                    _fromAddress.Text.Trim(), _appPassword.Text),
                EnableSsl      = true,
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
            };

        private void HandleEmailSent()
        {
            ShowSuccess(_statusLabel, "✓ Enviado");
            _attachmentPath = null;
            MessageBox.Show("¡Correo enviado!", "Éxito",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AttachFile()
        {
            var path = FilePicker.Open();
            if (path == null) return;
            _attachmentPath = path;
            ShowSuccess(_statusLabel, "📎 " + Path.GetFileName(path));
        }

        private void ClearForm()
        {
            _toAddress.Text = _subject.Text = _messageBody.Text = _statusLabel.Text = "";
            _attachmentPath = null;
        }

        private static void ShowGmailHelp() =>
            MessageBox.Show(
                "Para Gmail necesitas una Contraseña de Aplicación:" + Environment.NewLine + Environment.NewLine +
                "1. Ve a myaccount.google.com" + Environment.NewLine +
                "2. Seguridad → Verificación en 2 pasos → Activar" + Environment.NewLine +
                "3. Busca 'Contraseñas de aplicaciones'" + Environment.NewLine +
                "4. Crea una para Correo / Windows" + Environment.NewLine +
                "5. Copia los 16 caracteres aquí" + Environment.NewLine + Environment.NewLine +
                "NO uses tu contraseña normal de Gmail.",
                "Configuración Gmail", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Cámara ────────────────────────────────────────────────────
}
