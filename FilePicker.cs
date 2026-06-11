using Explorador_de_Archivo.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Explorador_de_Archivo
{
    /// <summary>
    /// Renderizado de archivos en vista cuadrícula y vista lista.
    /// Responsabilidad única: representar visualmente una lista de FileItem.
    /// </summary>
    public partial class Form1
    {
        internal void RenderItems(List<FileItem> items)
        {
            if (IsGridView) RenderGrid(items);
            else            RenderList(items);
            SetStatus($"{items.Count} elemento(s)  —  {CurrentPath}");
        }

        // ── Vista cuadrícula ──────────────────────────────────────

        private void RenderGrid(List<FileItem> items)
        {
            FileGrid.SuspendLayout();
            FileGrid.Controls.Clear();
            foreach (var item in items)
                FileGrid.Controls.Add(BuildCard(item));
            FileGrid.ResumeLayout();
        }

        private Panel BuildCard(FileItem item)
        {
            var card = new Panel
            { Width = 110, Height = 115, BackColor = Color.Transparent,
              Cursor = Cursors.Hand, Margin = new Padding(6), Tag = item };
            card.Controls.Add(BuildCardIcon(item));
            card.Controls.Add(BuildCardLabel(item));
            WireCardEvents(card, item);
            return card;
        }

        private static Control BuildCardIcon(FileItem item)
        {
            if (item.Thumbnail != null)
                return new PictureBox
                { Width = 72, Height = 72, SizeMode = PictureBoxSizeMode.Zoom,
                  Image = item.Thumbnail, BackColor = Theme.BgControl, Left = 19, Top = 6 };

            return new Label
            { Text = item.Emoji, Font = new Font("Segoe UI", 28f),
              ForeColor = item.KindColor, AutoSize = false,
              Width = 72, Height = 72, TextAlign = ContentAlignment.MiddleCenter,
              Left = 19, Top = 6, BackColor = Color.Transparent };
        }

        private static Label BuildCardLabel(FileItem item) =>
            new() { Text = item.Name, ForeColor = Theme.TxtMain, Font = Theme.FontSmall,
                    AutoSize = false, Width = 106, Height = 32, Left = 2, Top = 80,
                    TextAlign = ContentAlignment.TopCenter, BackColor = Color.Transparent };

        private void WireCardEvents(Panel card, FileItem item)
        {
            void Wire(Control ctrl)
            {
                ctrl.Click       += (_, _) => SelectItem(item, card);
                ctrl.DoubleClick += (_, _) => OpenItem(item);
                ctrl.MouseUp     += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    { SelectItem(item, card); BuildContextMenu(item).Show(card, e.Location); }
                };
                ctrl.MouseEnter += (_, _) =>
                { if (!item.Equals(SelectedItem)) card.BackColor = Theme.BgHover; };
                ctrl.MouseLeave += (_, _) =>
                { if (!item.Equals(SelectedItem)) card.BackColor = Color.Transparent; };
                foreach (Control child in ctrl.Controls) Wire(child);
            }
            Wire(card);
        }

        // ── Vista lista ───────────────────────────────────────────

        private void RenderList(List<FileItem> items)
        {
            FileList.Items.Clear();
            foreach (var item in items)
                FileList.Items.Add(BuildListRow(item));
        }

        private static ListViewItem BuildListRow(FileItem item)
        {
            var row = new ListViewItem($"{item.Emoji}  {item.Name}")
            { Tag = item, ForeColor = Theme.TxtMain, BackColor = Theme.BgMain };
            row.SubItems.Add(item.KindText);
            row.SubItems.Add(item.SizeText);
            row.SubItems.Add(item.ModifiedText);
            return row;
        }
    }
}
