using CsvHelper;
using Explorador_de_Archivo.Models;
using FileExplorer.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Formats.Asn1;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static Explorador_de_Archivo.Models.FileItem;
using static ShellDll.ShellAPI;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Explorador_de_Archivo.Services
{
    internal class FileService
    {
        public List<FileItem> GetItemsFiltered(string path, params FileKind[] kinds)
        {
            var list = new List<FileItem>();

            if (kinds == null || kinds.Length == 0)
            {
                foreach (var d in SafeDirs(path)) list.Add(BuildDir(d));
            }

            foreach (var f in SafeFiles(path))
            {
                var item = BuildFile(f);

                if (kinds == null || kinds.Length == 0 || kinds.Contains(item.Kind))
                {
                    list.Add(item);
                }
            }
            return list;
        }
        public List<FileItem> GetItems(string path)
        {
            var list = new List<FileItem>();
            foreach (var d in SafeDirs(path)) list.Add(BuildDir(d));
            foreach (var f in SafeFiles(path)) list.Add(BuildFile(f));
            return list;
        }
        private static string[] SafeDirs(string p)
        {
            try
            {
                return Directory.GetDirectories(p)
                    .Where(d =>
                    {
                        var info = new DirectoryInfo(d);
                        return !info.Attributes.HasFlag(FileAttributes.Hidden)
                            && !info.Attributes.HasFlag(FileAttributes.System)
                            && !info.Name.StartsWith('.');
                    })
                    .ToArray();
            }
            catch { return []; }
        }

        private static string[] SafeFiles(string p)
        {
            try
            {
                return Directory.GetFiles(p)
                    .Where(f =>
                    {
                        var info = new FileInfo(f);
                        return !info.Attributes.HasFlag(FileAttributes.Hidden)
                            && !info.Attributes.HasFlag(FileAttributes.System)
                            && !info.Name.StartsWith('.')
                            && !info.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                            && !info.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
                            && !info.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
                            && !info.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                            && !info.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToArray();
            }
            catch { return []; }
        }
        public List<FileItem> Search(string path, string query) =>
            GetItems(path)
                .Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        public void OpenDefault(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });
            }
            catch { /* fallback silencioso intencional */ }
        }

        private static FileItem BuildDir(string path)
        {
            var d = new DirectoryInfo(path);
            return new FileItem { FullPath = path, Name = d.Name, Modified = d.LastWriteTime, IsDirectory = true, Kind = FileKind.Folder };
        }

        private static FileItem BuildFile(string path)
        {
            var f = new FileInfo(path);
            var kind = FileKindHelper.From(f.Extension);
            return new FileItem
            {
                FullPath = path,
                Name = f.Name,
                Extension = f.Extension,
                SizeBytes = f.Length,
                Modified = f.LastWriteTime,
                Kind = kind,
                Thumbnail = kind == FileKind.Image ? LoadThumb(path) : null,
            };
        }

        private static Image? LoadThumb(string path)
        {
            try { using var bmp = Image.FromFile(path); return bmp.GetThumbnailImage(96, 96, null, IntPtr.Zero); }
            catch { return null; }
        }

       
    }

    public class DatabaseService
    {
        private readonly string _conn;

        public DatabaseService(string path)
        {
            _conn = $"Data Source={path}";
            using var c = Open();
            Exec(c, """
            CREATE TABLE IF NOT EXISTS Records (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL, Email TEXT, Phone TEXT,
                Category TEXT, Notes TEXT,
                CreatedAt TEXT DEFAULT (datetime('now','localtime')),
                FileData TEXT
            )
        """);
            // Migración: agrega FileData a bases de datos antiguas
            try { Exec(c, "ALTER TABLE Records ADD COLUMN FileData TEXT"); } catch { /* fallback silencioso intencional */ }
        }

        public List<DatabaseRecord> GetAll() => Query("SELECT * FROM Records ORDER BY Id DESC");
        public List<DatabaseRecord> Search(string query) => Query(
            "SELECT * FROM Records WHERE Name LIKE $q OR Email LIKE $q OR Category LIKE $q ORDER BY Id DESC",
            ("$q", $"%{query}%"));

        public void Insert(DatabaseRecord r) { using var c = Open(); Run(c, "INSERT INTO Records (Name,Email,Phone,Category,Notes,FileData) VALUES ($n,$e,$p,$c,$no,$fd)", Params(r)); }
        public void Update(DatabaseRecord r) { using var c = Open(); Run(c, "UPDATE Records SET Name=$n,Email=$e,Phone=$p,Category=$c,Notes=$no,FileData=$fd WHERE Id=$id", [.. Params(r), ("$id", r.Id)]); }
        public void Delete(int id) { using var c = Open(); Run(c, "DELETE FROM Records WHERE Id=$id", [("$id", id)]); }

        public string RunSql(string sql)
        {
            try
            {
                using var c = Open();
                var cmd = c.CreateCommand(); cmd.CommandText = sql;
                if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    using var rd = cmd.ExecuteReader();
                    var sb = new StringBuilder();
                    for (int i = 0; i < rd.FieldCount; i++) sb.Append(rd.GetName(i).PadRight(20));
                    sb.AppendLine(); sb.AppendLine(new string('-', rd.FieldCount * 20));
                    while (rd.Read()) { for (int i = 0; i < rd.FieldCount; i++) sb.Append((rd.GetValue(i)?.ToString() ?? "").PadRight(20)); sb.AppendLine(); }
                    return sb.ToString();
                }
                return $"OK — {cmd.ExecuteNonQuery()} fila(s) afectada(s)";
            }
            catch (Exception ex) { return $"ERROR: {ex.Message}"; }
        }

        private List<DatabaseRecord> Query(string sql, params (string k, object v)[] p)
        {
            using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = sql;
            foreach (var (k, v) in p) cmd.Parameters.AddWithValue(k, v);
            using var rd = cmd.ExecuteReader();
            var list = new List<DatabaseRecord>();
            while (rd.Read()) list.Add(new DatabaseRecord
            {
                Id = rd.GetInt32(0),
                Name = rd.GetString(1),
                Email = rd.IsDBNull(2) ? "" : rd.GetString(2),
                Phone = rd.IsDBNull(3) ? "" : rd.GetString(3),
                Category = rd.IsDBNull(4) ? "" : rd.GetString(4),
                Notes = rd.IsDBNull(5) ? "" : rd.GetString(5),
                CreatedAt = rd.IsDBNull(6) ? "" : rd.GetString(6),
                FileData = rd.FieldCount > 7 && !rd.IsDBNull(7) ? rd.GetString(7) : "",
            });
            return list;
        }

        private static (string, object)[] Params(DatabaseRecord r) =>
            [("$n", r.Name), ("$e", r.Email), ("$p", r.Phone), ("$c", r.Category), ("$no", r.Notes), ("$fd", r.FileData ?? "")];

        private SqliteConnection Open() { var c = new SqliteConnection(_conn); c.Open(); return c; }
        private static void Exec(SqliteConnection c, string sql) { var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        private static void Run(SqliteConnection c, string sql, (string k, object v)[] p) { var cmd = c.CreateCommand(); cmd.CommandText = sql; foreach (var (k, v) in p) cmd.Parameters.AddWithValue(k, v); cmd.ExecuteNonQuery(); }
    }

    public class ConverterService
    {
        public string Convert(string content, string from, string to)
        {
            try
            {
                return (from.ToUpper(), to.ToUpper()) switch
                {
                    ("JSON", "CSV") => JsonToCsv(content),
                    ("CSV", "JSON") => CsvToJson(content),
                    ("JSON", "XML") => JsonToXml(content),
                    ("XML", "JSON") => XmlToJson(content),
                    ("CSV", "XML") => CsvToXml(content),
                    (_, "TXT") => StripToText(content),
                    _ => content
                };
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private static string JsonToCsv(string json)
        {
            var arr = JArray.Parse(json);

            if (arr.Count == 0)
                return "";

            var headers = arr
                .OfType<JObject>()
                .SelectMany(o => o.Properties())
                .Select(p => p.Name)
                .Distinct()
                .ToList();

            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",", headers));

            foreach (JObject row in arr)
            {
                sb.AppendLine(string.Join(",",
                    headers.Select(h =>
                        row[h]?.ToString()?.Replace(",", " ") ?? "")));
            }

            return sb.ToString();
        }

        private static string CsvToJson(string csv)
        {
            using var reader = new StringReader(csv);

            var config = new CsvHelper.Configuration.CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var csvReader = new CsvReader(reader, config);

            var records = csvReader.GetRecords<dynamic>().ToList();

            return JsonConvert.SerializeObject(
                records,
                Newtonsoft.Json.Formatting.Indented);
        }

        private static string JsonToXml(string json)
        {
            var arr = JArray.Parse(json);

            var root = new XElement("Records",
                arr.OfType<JObject>().Select(obj =>
                    new XElement("Record",
                        obj.Properties().Select(p =>
                            new XElement(
                                p.Name,
                                p.Value?.ToString() ?? "")))));

            return root.ToString();
        }

        private static string XmlToJson(string xml)
        {
            var doc = XDocument.Parse(xml);

            var records = doc.Root!
                .Elements("Record")
                .Select(r =>
                    r.Elements()
                     .ToDictionary(
                        e => e.Name.LocalName,
                        e => (object)e.Value))
                .ToList();

            return JsonConvert.SerializeObject(
                records,
                Newtonsoft.Json.Formatting.Indented);
        }

        private static string CsvToXml(string csv)
        {
            using var reader = new StringReader(csv);

            var config = new CsvHelper.Configuration.CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var csvReader = new CsvReader(reader, config);

            var records = csvReader.GetRecords<dynamic>();

            var root = new XElement("Records");

            foreach (IDictionary<string, object> row in records)
            {
                var record = new XElement("Record");

                foreach (var col in row)
                {
                    record.Add(
                        new XElement(
                            col.Key,
                            col.Value?.ToString() ?? ""));
                }

                root.Add(record);
            }

            return root.ToString();
        }

        private static string StripToText(string content)
        {
            return Regex.Replace(
                Regex.Replace(content, "<[^>]+>", ""),
                @"[{}\[\]"":,]",
                " ");
        }
        public class GeoService
        {
            public GeoInfo? ReadFromImage(string path)
            {
                return null;
            }

            public string MapUrl(double lat, double lon)
            {
                return $"https://www.openstreetmap.org/export/embed.html?bbox={lon - 0.01}%2C{lat - 0.01}%2C{lon + 0.01}%2C{lat + 0.01}&layer=mapnik&marker={lat}%2C{lon}";
            }

            public string ToGeoJson(GeoInfo geo)
            {
                return $"{{\n  \"type\": \"Feature\",\n  \"geometry\": {{\n    \"type\": \"Point\",\n    \"coordinates\": [{geo.Lon}, {geo.Lat}]\n  }}\n}}";
            }
        }
        public class DataCleanerService
        {
            public CleanResult Clean(string input, CleanOptions opts)
            {
                var lines = input.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
                var processed = lines.Select(l => opts.TrimSpaces ? l.Trim() : l)
                                     .Where(l => !opts.RemoveEmpty || !string.IsNullOrEmpty(l));

                if (opts.RemoveDuplicates) processed = processed.Distinct();

                var resultList = processed.ToList();

                int emptyRemoved = opts.RemoveEmpty ? lines.Count(l => string.IsNullOrEmpty(opts.TrimSpaces ? l.Trim() : l)) : 0;
                int dupesRemoved = opts.RemoveDuplicates ? (lines.Length - emptyRemoved - resultList.Count) : 0;

                return new CleanResult(string.Join(Environment.NewLine, resultList), dupesRemoved, emptyRemoved, 0);
            }
        }
        public record SmtpConfig(string Host, int Port, string From, string Password);
       public record EmailMsg(string To,string Subject,string Body,string? AttachmentPath = null);

        public class EmailService
        {
            private readonly SmtpConfig _config;

            public EmailService(SmtpConfig config) => _config = config;

            public async Task SendAsync(EmailMsg msg)
            {
                await Task.Delay(1500);
            }

            public static string Template(string key) => key switch
            {
                "formal" => "Estimado [Nombre],\n\nPor medio de la presente...",
                "followup" => "Hola [Nombre],\n\nEspero que estés bien. Quería dar seguimiento a...",
                "invoice" => "Adjunto a este correo encontrará la factura correspondiente a...",
                _ => ""
            };
        }
    }
}
