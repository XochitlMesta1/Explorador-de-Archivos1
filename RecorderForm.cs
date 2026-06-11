using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorador_de_Archivo.Models
{

    public class FileItem
    {

        public string FullPath { get; init; } = "";
        public string Name { get; init; } = "";
        public string Extension { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime Modified { get; init; }
        public bool IsDirectory { get; init; }
        public FileKind Kind { get; init; }
        public Image? Thumbnail { get; set; }
        public string SizeText => IsDirectory ? "" : SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1_048_576 => $"{SizeBytes / 1024.0:F1} KB",
            < 1_073_741_824 => $"{SizeBytes / 1_048_576.0:F1} MB",
            _ => $"{SizeBytes / 1_073_741_824.0:F1} GB"
        };
        public string KindText => IsDirectory ? "Carpeta" : Kind.Label();
        public string ModifiedText => Modified.ToString("dd/MM/yyyy  HH:mm");
        public string Emoji => IsDirectory ? "📁" : Kind.Emoji();
        public Color KindColor => IsDirectory ? Theme.Amber : Kind.Color();
    }

    public enum FileKind
    {
        Folder, Image, Audio, Video,
        Document, Spreadsheet, Code, Archive, Unknown
    }

    public static class FileKindHelper
    {
        private static readonly Dictionary<string, FileKind> Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = FileKind.Image,
                [".jpeg"] = FileKind.Image,
                [".png"] = FileKind.Image,
                [".bmp"] = FileKind.Image,
                [".gif"] = FileKind.Image,
                [".webp"] = FileKind.Image,
                [".tiff"] = FileKind.Image,

                [".mp3"] = FileKind.Audio,
                [".wav"] = FileKind.Audio,
                [".ogg"] = FileKind.Audio,
                [".flac"] = FileKind.Audio,
                [".aac"] = FileKind.Audio,
                [".wma"] = FileKind.Audio,

                [".mp4"] = FileKind.Video,
                [".avi"] = FileKind.Video,
                [".mkv"] = FileKind.Video,
                [".mov"] = FileKind.Video,
                [".wmv"] = FileKind.Video,
                [".webm"] = FileKind.Video,

                [".pdf"] = FileKind.Document,
                [".docx"] = FileKind.Document,
                [".doc"] = FileKind.Document,
                [".txt"] = FileKind.Document,
                [".xml"] = FileKind.Document,
                [".json"] = FileKind.Document,
                [".rtf"] = FileKind.Document,

                [".csv"] = FileKind.Spreadsheet,
                [".xlsx"] = FileKind.Spreadsheet,
                [".xls"] = FileKind.Spreadsheet,

                [".cs"] = FileKind.Code,
                [".py"] = FileKind.Code,
                [".js"] = FileKind.Code,
                [".ts"] = FileKind.Code,
                [".html"] = FileKind.Code,
                [".css"] = FileKind.Code,
                [".cpp"] = FileKind.Code,
                [".java"] = FileKind.Code,

                [".zip"] = FileKind.Archive,
                [".rar"] = FileKind.Archive,
                [".7z"] = FileKind.Archive,
                [".tar"] = FileKind.Archive,
            };

        public static FileKind From(string ext) =>
            Map.TryGetValue(ext, out var k) ? k : FileKind.Unknown;

        public static string Label(this FileKind k) => k switch
        {
            FileKind.Image => "Imagen",
            FileKind.Audio => "Audio",
            FileKind.Video => "Video",
            FileKind.Document => "Documento",
            FileKind.Spreadsheet => "Hoja de cálculo",
            FileKind.Code => "Código",
            FileKind.Archive => "Comprimido",
            _ => "Archivo"
        };

        public static string Emoji(this FileKind k) => k switch
        {
            FileKind.Image => "🖼",
            FileKind.Audio => "🎵",
            FileKind.Video => "🎬",
            FileKind.Document => "📄",
            FileKind.Spreadsheet => "📊",
            FileKind.Code => "💻",
            FileKind.Archive => "📦",
            _ => "📎"
        };

        public static Color Color(this FileKind k) => k switch
        {
            FileKind.Image => System.Drawing.Color.FromArgb(74, 168, 255),
            FileKind.Audio => System.Drawing.Color.FromArgb(76, 175, 80),
            FileKind.Video => System.Drawing.Color.FromArgb(244, 67, 54),
            FileKind.Document => System.Drawing.Color.FromArgb(255, 193, 7),
            FileKind.Spreadsheet => System.Drawing.Color.FromArgb(38, 201, 96),
            FileKind.Code => System.Drawing.Color.FromArgb(156, 39, 176),
            FileKind.Archive => System.Drawing.Color.FromArgb(255, 152, 0),
            _ => System.Drawing.Color.FromArgb(130, 130, 130),
        };
    }

    public static partial class Theme
    {
        public static readonly Color Amber = Color.FromArgb(255, 193, 7);
    }

    public class DatabaseRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Category { get; set; } = "";
        public string Notes { get; set; } = "";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        /// <summary>
        /// Contenido del archivo original codificado en base64.
        /// Permite recuperar (restaurar) el archivo si fue eliminado del disco.
        /// </summary>
        public string FileData { get; set; } = "";
    }
    public class GeoInfo
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string? DateTaken { get; set; }
        public string? Camera { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    public record CleanOptions(
        bool TrimSpaces,
        bool RemoveEmpty,
        bool RemoveDuplicates,
        bool ValidateEmails,
        bool FixCasing
    );

    public record CleanResult(
        string Content,
        int Dupes,
        int Empty,
        int BadEmails
    );

}