using System;
using System.Drawing;
using System.Windows.Forms;
using XComponent.SliderBar;

namespace Explorador_de_Archivo
{
    public class Theme
    {
        public static readonly Color BgMain = Color.FromArgb(24, 24, 24);
        public static readonly Color BgPanel = Color.FromArgb(32, 32, 32);
        public static readonly Color BgControl = Color.FromArgb(44, 44, 44);
        public static readonly Color BgHover = Color.FromArgb(55, 55, 55);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentHover = Color.FromArgb(0, 100, 180);
        public static readonly Color Border = Color.FromArgb(60, 60, 60);
        public static readonly Color TxtMain = Color.FromArgb(230, 230, 230);
        public static readonly Color TxtSub = Color.FromArgb(160, 160, 160);
        public static readonly Color TxtMuted = Color.FromArgb(100, 100, 100);
        public static readonly Color Green = Color.FromArgb(76, 175, 80);
        public static readonly Color Red = Color.FromArgb(220, 50, 50);

        public static readonly Font FontNormal = new("Segoe UI", 9f);
        public static readonly Font FontBold = new("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontSmall = new("Segoe UI", 8f);
        public static readonly Font FontLarge = new("Segoe UI", 11f);
        public static readonly Font FontMono = new("Cascadia Code", 9f);
        public static readonly Font FontMonoLg = new("Cascadia Code", 12f);
        public static readonly Font FontTitle = new("Segoe UI", 14f, FontStyle.Bold);

        public static Button FlatBtn(string text, int w = 110, int h = 30)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = BgControl,
                ForeColor = TxtMain,
                Font = FontNormal,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = BgHover;
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 50);
            return b;
        }

        public static Button AccentBtn(string text, int w = 120, int h = 30)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = Accent,
                ForeColor = Color.White,
                Font = FontBold,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 80, 160);
            return b;
        }

        public static TextBox TextBox(bool multiline = false, int height = 28)
        {
            return new TextBox
            {
                BackColor = BgControl,
                ForeColor = TxtMain,
                Font = FontNormal,
                BorderStyle = BorderStyle.FixedSingle,
                Height = height,
                Multiline = multiline,
                ScrollBars = multiline ? ScrollBars.Both : ScrollBars.None,
            };
        }

        public static RichTextBox RichBox(bool readOnly = false)
        {
            return new RichTextBox
            {
                BackColor = readOnly ? Color.FromArgb(18, 18, 18) : BgControl,
                ForeColor = TxtMain,
                Font = FontMono,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                ReadOnly = readOnly,
            };
        }

        public static Label Label(string text, bool muted = true)
        {
            return new Label
            {
                Text = text,
                ForeColor = muted ? TxtMuted : TxtSub,
                Font = FontSmall,
                AutoSize = true,
            };
        }

        public static ComboBox ComboBox()
        {
            return new ComboBox
            {
                BackColor = BgControl,
                ForeColor = TxtMain,
                Font = FontNormal,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
        }

        public static MACTrackBar TrackBar(int min, int max, int val = 0)
        {
            return new MACTrackBar
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, val)),
                TickStyle = TickStyle.None,
                BackColor = Color.Transparent,
                TrackerColor = Accent,
            };
        }

        public static DataGridView Grid()
        {
            var g = new DataGridView
            {
                BackgroundColor = BgMain,
                GridColor = Border,
                ForeColor = TxtMain,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                MultiSelect = false,
                RowTemplate = { Height = 26 },
            };
            g.DefaultCellStyle.BackColor = BgMain;
            g.DefaultCellStyle.ForeColor = TxtMain;
            g.DefaultCellStyle.SelectionBackColor = Accent;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Font = FontNormal;
            g.ColumnHeadersDefaultCellStyle.BackColor = BgPanel;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TxtSub;
            g.ColumnHeadersDefaultCellStyle.Font = FontBold;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = BgPanel;
            return g;
        }

        public static CheckBox CheckBox(string text)
        {
            return new CheckBox
            {
                Text = text,
                ForeColor = TxtSub,
                Font = FontNormal,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
        }

        public static void ApplyForm(Form f)
        {
            f.BackColor = BgMain;
            f.ForeColor = TxtMain;
            f.Font = FontNormal;
            f.StartPosition = FormStartPosition.CenterScreen;
        }
    }

    public static class Ext
    {
        public static T Do<T>(this T ctrl, Action<T> a) where T : Control
        {
            a(ctrl);
            return ctrl;
        }
    }
}