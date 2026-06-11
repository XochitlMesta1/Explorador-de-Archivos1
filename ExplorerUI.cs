using Explorador_de_Archivo.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Explorador_de_Archivo
{
    /// <summary>
    /// Navegación por el sistema de archivos: historial, filtros, búsqueda.
    /// Responsabilidad única: determinar qué carpeta mostrar y cuáles archivos.
    /// </summary>
    public partial class Form1
    {
        internal void Navigate(string path)
        {
            if (path == "drives") { ShowDrives(); return; }
            if (!Directory.Exists(path)) { SetStatus("Ruta no encontrada"); return; }
            CurrentPath           = path;
            _pathBox.Text         = path;
            History.Push(path);
            Reload();
        }

        internal void NavigateWithoutFilter(string path)
        { ActiveFilter = null; Navigate(path); }

        internal void Reload()
        {
            var items = ActiveFilter != null
                ? Files.GetItemsFiltered(CurrentPath, ActiveFilter)
                : Files.GetItems(CurrentPath);
            RenderItems(items);
        }

        private void ShowDrives()
        {
            CurrentPath   = "Este equipo";
            _pathBox.Text = CurrentPath;
            RenderItems(GetDriveItems());
        }

        private static List<FileItem> GetDriveItems() =>
            DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new FileItem
                { FullPath = d.RootDirectory.FullName,
                  Name = $"{d.VolumeLabel}  ({d.Name})",
                  IsDirectory = true, Kind = FileKind.Folder })
                .ToList();

        internal void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { Reload(); return; }
            var results = Files.Search(CurrentPath, query);
            RenderItems(results);
            SetStatus($"{results.Count} resultado(s) para '{query}'");
        }

        internal void GoBack()
        {
            if (!History.CanGoBack) return;
            CurrentPath   = History.GoBack();
            _pathBox.Text = CurrentPath;
            Reload();
        }

        internal void GoForward()
        {
            if (!History.CanGoForward) return;
            CurrentPath   = History.GoForward();
            _pathBox.Text = CurrentPath;
            Reload();
        }

        internal void GoUp()
        {
            var parent = Directory.GetParent(CurrentPath)?.FullName;
            if (parent != null) Navigate(parent);
        }

        internal void CreateFolder()
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre:", "Nueva carpeta", "Nueva carpeta");
            if (string.IsNullOrWhiteSpace(name)) return;
            try { Directory.CreateDirectory(Path.Combine(CurrentPath, name)); Reload(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}
