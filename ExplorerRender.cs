using Explorador_de_Archivo.Forms;
using Explorador_de_Archivo.Models;
using FileExplorer.Forms;
using Explorador_de_Archivo.Services;
using System;
using System.IO;
using System.Windows.Forms;
using Explorador_de_Archivo;

namespace Explorador_de_Archivo
{
    /// <summary>
    /// Operaciones CRUD sobre archivos y carpetas: abrir, copiar, mover, eliminar, renombrar.
    /// Responsabilidad única: mutación del sistema de archivos.
    /// </summary>
    public partial class Form1
    {
        // ── Selección ─────────────────────────────────────────────

        internal void SelectItem(FileItem item, Panel? card = null)
        {
            ClearCardHighlights();
            if (card != null) card.BackColor = System.Drawing.Color.FromArgb(30, 0, 120, 212);
            SelectedItem = item;
            ShowDetailPlaceholder(false);
            UpdateDetailFields(item);
            RebuildActionButtons();
        }

        internal void ClearSelection()
        {
            SelectedItem = null;
            ClearCardHighlights();
            ShowDetailPlaceholder(true);
        }

        private void ClearCardHighlights()
        {
            foreach (Control c in FileGrid.Controls)
                c.BackColor = System.Drawing.Color.Transparent;
        }

        // ── Apertura de archivos ──────────────────────────────────

        internal void OpenItem(FileItem item)
        {
            if (item.IsDirectory) { NavigateWithoutFilter(item.FullPath); return; }

            switch (item.Kind)
            {
                case FileKind.Image:       new ImageEditorForm(item.FullPath).Show();  break;
                case FileKind.Audio:       new AudioPlayerForm(item.FullPath).Show();  break;
                case FileKind.Video:       new VideoPlayerForm(item.FullPath).Show();  break;
                case FileKind.Spreadsheet: new ChartForm(item.FullPath).Show();         break;
                case FileKind.Document: Explorador_de_Archivo.DocumentViewer.Open(item.FullPath);                break;
                case FileKind.Code: Explorador_de_Archivo.DocumentViewer.OpenText(item.FullPath);            break;
                default: Explorador_de_Archivo.DocumentViewer.OpenText(item.FullPath);            break;
            }
        }

        // ── Portapapeles ──────────────────────────────────────────

        internal void PasteHere()
        {
            if (!Clipboard.HasContent) { SetStatus("Portapapeles vacío"); return; }
            foreach (var source in Clipboard.Paths)
                TryTransferFile(source);
            Clipboard.ClearIfCut();
            Reload();
        }

        private void TryTransferFile(string source)
        {
            try
            {
                var dest = ResolveUniqueDestination(source);
                if (Clipboard.IsCut) MoveFileSystemEntry(source, dest);
                else                 CopyFileSystemEntry(source, dest);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al pegar"); }
        }

        private string ResolveUniqueDestination(string source)
        {
            var dest = Path.Combine(CurrentPath, Path.GetFileName(source));
            if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
            return Path.Combine(CurrentPath,
                Path.GetFileNameWithoutExtension(source) + "_copia" + Path.GetExtension(source));
        }

        // ── Eliminar ──────────────────────────────────────────────

        internal void DeleteItem(FileItem item)
        {
            if (!UserConfirmedDelete(item.Name)) return;
            try
            {
                if (item.IsDirectory) Directory.Delete(item.FullPath, recursive: true);
                else File.Delete(item.FullPath);
                AfterMutation();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private static bool UserConfirmedDelete(string name) =>
            MessageBox.Show($"¿Eliminar '{name}'?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        // ── Mover ─────────────────────────────────────────────────

        internal void MoveItem(FileItem item)
        {
            using var dialog = new FolderBrowserDialog
            { Description = "Selecciona la carpeta de destino", UseDescriptionForTitle = true };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                MoveFileSystemEntry(item.FullPath, Path.Combine(dialog.SelectedPath, item.Name));
                AfterMutation();
                SetStatus("✓ Movido a " + dialog.SelectedPath);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al mover"); }
        }

        // ── Renombrar ─────────────────────────────────────────────

        internal void RenameItem(FileItem item)
        {
            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Nuevo nombre:", "Renombrar", item.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                MoveFileSystemEntry(item.FullPath,
                    Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName));
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        // ── Helpers de sistema de archivos ────────────────────────

        private static void MoveFileSystemEntry(string source, string destination)
        {
            if (Directory.Exists(source)) Directory.Move(source, destination);
            else File.Move(source, destination);
        }

        private static void CopyFileSystemEntry(string source, string destination)
        {
            if (Directory.Exists(source)) CopyDirectory(source, destination);
            else File.Copy(source, destination);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }

        private void AfterMutation()
        {
            SelectedItem = null;
            ShowDetailPlaceholder(true);
            Reload();
        }

    }
}
