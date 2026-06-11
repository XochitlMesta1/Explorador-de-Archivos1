using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorador_de_Archivo
{
    internal class UIFactory
    {
        public static Button FlatButton(string text, int width = 110, int height = 30)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.BgTertiary,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Regular,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = AppColors.Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = AppColors.BgHover;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 50);
            return btn;
        }

        public static Button AccentButton(string text, int width = 110, int height = 30)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Accent,
                ForeColor = Color.White,
                Font = AppFonts.Bold,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AppColors.AccentHover;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 170);
            return btn;
        }

        public static TextBox DarkTextBox(bool multiline = false, int height = 28)
        {
            var tb = new TextBox
            {
                BackColor = AppColors.BgTertiary,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Regular,
                BorderStyle = BorderStyle.FixedSingle,
                Height = height,
                Multiline = multiline,
            };
            if (multiline) tb.ScrollBars = ScrollBars.Both;
            return tb;
        }

        public static RichTextBox DarkRichBox()
        {
            return new RichTextBox
            {
                BackColor = AppColors.BgTertiary,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Mono,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
            };
        }

        public static Label Label(string text, bool muted = false)
        {
            return new Label
            {
                Text = text,
                ForeColor = muted ? AppColors.TextMuted : AppColors.TextSecondary,
                Font = AppFonts.Small,
                AutoSize = true,
            };
        }

        public static ComboBox DarkCombo()
        {
            return new ComboBox
            {
                BackColor = AppColors.BgTertiary,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Regular,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
        }

        public static Panel Separator(bool vertical = false)
        {
            return new Panel
            {
                BackColor = AppColors.Border,
                Width = vertical ? 1 : 0,
                Height = vertical ? 0 : 1,
                Dock = vertical ? DockStyle.Left : DockStyle.Top,
            };
        }

        public static TrackBar DarkTrackBar(int min, int max, int value)
        {
            return new TrackBar
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                BackColor = AppColors.BgSecondary,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Height = 24,
            };
        }

        public static DataGridView DarkGrid()
        {
            var grid = new DataGridView
            {
                BackgroundColor = AppColors.BgPrimary,
                GridColor = AppColors.Border,
                ForeColor = AppColors.TextPrimary,
                DefaultCellStyle = { BackColor = AppColors.BgPrimary, ForeColor = AppColors.TextPrimary, SelectionBackColor = AppColors.Accent, SelectionForeColor = Color.White, Font = AppFonts.Regular },
                ColumnHeadersDefaultCellStyle = { BackColor = AppColors.BgSecondary, ForeColor = AppColors.TextSecondary, Font = AppFonts.Bold },
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                MultiSelect = false,
                RowTemplate = { Height = 26 },
            };
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppColors.BgSecondary;
            return grid;
        }

        public static CheckBox DarkCheck(string text)
        {
            return new CheckBox
            {
                Text = text,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Regular,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
        }

        public static ProgressBar DarkProgress()
        {
            return new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                BackColor = AppColors.BgTertiary,
                ForeColor = AppColors.Accent,
                Height = 6,
            };
        }

        public static void ApplyDarkForm(Form form)
        {
            form.BackColor = AppColors.BgPrimary;
            form.ForeColor = AppColors.TextPrimary;
            form.Font = AppFonts.Regular;
            form.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
