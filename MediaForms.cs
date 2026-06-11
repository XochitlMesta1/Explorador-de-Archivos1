using Explorador_de_Archivo.Models;
using Explorador_de_Archivo.Services;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;

namespace Explorador_de_Archivo.Forms
{
    // ── Puente JavaScript → C# ────────────────────────────────────
    [System.Runtime.InteropServices.ComVisible(true)]
    public class MapBridge
    {
        private readonly GeoImageForm _form;
        public MapBridge(GeoImageForm form) => _form = form;

        public void OnClick(string lat, string lon)
        {
            if (double.TryParse(lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var dLat) &&
                double.TryParse(lon, NumberStyles.Any, CultureInfo.InvariantCulture, out var dLon))
                _form.BeginInvoke(() => _form.MarcarUbicacion(dLat, dLon));
        }
    }

    public class GeoImageForm : Form
    {
        // ── Estado ────────────────────────────────────────────────
        private string   _ruta       = "";
        private Bitmap?  _bitmap;
        private GeoInfo? _geo;
        private double   _latSel     = double.NaN;
        private double   _lonSel     = double.NaN;

        // ── Controles ─────────────────────────────────────────────
        private WebBrowser _mapa      = null!;
        private Label _lblLat         = null!, _lblLon   = null!,
                      _lblFecha       = null!, _lblCam   = null!,
                      _lblSize        = null!, _lblStatus = null!,
                      _lblInfo        = null!;
        private Button _btnGuardar    = null!;

        // ── Constructor ───────────────────────────────────────────
        public GeoImageForm(string path)
        {
            ForzarIE11();
            Theme.ApplyForm(this);
            Text = "Geolocalización GPS";
            Size = new Size(900, 660);
            BuildUI();
            CargarImagen(path);
        }

        // ── IE11 (necesario para que Leaflet funcione) ────────────
        private static void ForzarIE11()
        {
            try
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                key?.SetValue(exe, 11001, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { /* fallback silencioso intencional */ }
        }

        // ── UI ────────────────────────────────────────────────────
        private void BuildUI()
        {
            // Barra superior
            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 46,
                BackColor = Theme.BgPanel, Padding = new Padding(8, 8, 0, 0)
            };
            _lblStatus = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true };
            topBar.Controls.AddRange(new Control[]
            {
                MakeBtn("📂 Abrir imagen",     150, AbrirImagen,     accent: true),
                MakeBtn("💾 Exportar GeoJSON", 160, ExportarGeoJson, accent: false),
                _lblStatus
            });

            // Panel inferior
            var botPanel = new Panel
            {
                Dock = DockStyle.Bottom, Height = 100,
                BackColor = Theme.BgPanel, Padding = new Padding(10, 6, 10, 6)
            };

            // Fila de tarjetas de metadatos
            var cards = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 50,
                ColumnCount = 5, RowCount = 2, BackColor = Color.Transparent
            };
            for (int i = 0; i < 5; i++)
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            _lblLat   = AddCard(cards, "Latitud",    0);
            _lblLon   = AddCard(cards, "Longitud",   1);
            _lblFecha = AddCard(cards, "Fecha foto", 2);
            _lblCam   = AddCard(cards, "Cámara",     3);
            _lblSize  = AddCard(cards, "Resolución", 4);

            // Fila de acciones
            var actRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 38,
                BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0)
            };
            _lblInfo = new Label
            {
                ForeColor = Color.OrangeRed, Font = Theme.FontSmall,
                AutoSize = true, TextAlign = ContentAlignment.MiddleLeft
            };
            _btnGuardar = Theme.AccentBtn("📍 Guardar ubicación en imagen", 230, 30);
            _btnGuardar.Enabled = false;
            _btnGuardar.Click  += (_, _) => GuardarGPS();

            var btnGMaps = Theme.FlatBtn("🌐 Ver en Google Maps", 180, 30);
            btnGMaps.Click += (_, _) => AbrirGoogleMaps();

            actRow.Controls.AddRange(new Control[] { _lblInfo, _btnGuardar, btnGMaps });
            botPanel.Controls.AddRange(new Control[] { cards, actRow });

            // Mapa WebBrowser
            _mapa = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                ObjectForScripting = new MapBridge(this)
            };

            Controls.AddRange(new Control[] { topBar, _mapa, botPanel });
        }

        private static Label AddCard(TableLayoutPanel t, string titulo, int col)
        {
            t.Controls.Add(new Label
            {
                Text = titulo, ForeColor = Theme.TxtMuted,
                Font = Theme.FontSmall, AutoSize = true,
                Padding = new Padding(4, 0, 0, 0)
            }, col, 0);

            var val = new Label
            {
                Text = "—", ForeColor = Theme.TxtMain,
                Font = Theme.FontMono, AutoSize = false,
                Width = 140, Padding = new Padding(4, 0, 0, 0)
            };
            t.Controls.Add(val, col, 1);
            return val;
        }

        private static Button MakeBtn(string texto, int w, Action a, bool accent)
        {
            var b = accent ? Theme.AccentBtn(texto, w, 30) : Theme.FlatBtn(texto, w, 30);
            b.Click += (_, _) => a();
            return b;
        }

        // ── HTML con Leaflet embebido ─────────────────────────────
        private static string BuildHtml(double? lat = null, double? lon = null)
        {
            double cLat  = lat ?? 23.6345;
            double cLon  = lon ?? -102.5528;
            int    zoom  = lat.HasValue ? 14 : 5;
            string sLat  = cLat.ToString("F6", CultureInfo.InvariantCulture);
            string sLon  = cLon.ToString("F6", CultureInfo.InvariantCulture);

            // Marcador de ubicación ya guardada (📷)
            string existente = lat.HasValue
                ? $@"L.marker([{sLat},{sLon}],{{
                        icon:L.divIcon({{html:'📷',className:'',iconSize:[28,28],iconAnchor:[14,28]}})
                    }}).addTo(map)
                     .bindPopup('<b>📷 Ubicación guardada</b><br>Lat:{sLat}<br>Lon:{sLon}')
                     .openPopup();"
                : "";

            return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'>
<meta http-equiv='X-UA-Compatible' content='IE=edge'>
<link  rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
  html,body,#map{{margin:0;padding:0;width:100%;height:100%;overflow:hidden;}}
  #hint{{position:absolute;bottom:12px;left:50%;transform:translateX(-50%);
         background:rgba(0,0,0,.75);color:#fff;padding:7px 18px;border-radius:20px;
         font:13px Segoe UI,sans-serif;z-index:9999;pointer-events:none;white-space:nowrap;}}
</style>
</head><body>
<div id='map'></div>
<div id='hint'>🖱️ Haz clic en el mapa para colocar el marcador GPS</div>
<script>
  var map = L.map('map').setView([{sLat},{sLon}],{zoom});
  L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',
    {{maxZoom:19,attribution:'© OpenStreetMap contributors'}}).addTo(map);
  {existente}
  var pin = null;
  map.on('click',function(e){{
    var la=e.latlng.lat.toFixed(6), lo=e.latlng.lng.toFixed(6);
    if(pin) map.removeLayer(pin);
    pin=L.marker([la,lo]).addTo(map)
         .bindPopup('<b>📍 Nueva ubicación</b><br>'+la+', '+lo).openPopup();
    document.getElementById('hint').textContent=
      '📍 '+la+', '+lo+' — pulsa «Guardar ubicación» para confirmar';
    try{{window.external.OnClick(la,lo);}}catch(x){{}}
  }});
</script></body></html>";
        }

        // ── Carga de imagen + EXIF ────────────────────────────────
        private void CargarImagen(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return;
            _ruta    = ruta;
            _latSel  = _lonSel = double.NaN;
            _btnGuardar.Enabled = false;
            _lblStatus.Text = "Leyendo EXIF...";

            try
            {
                _bitmap?.Dispose();
                _bitmap = CopiarConExif(ruta);
                _geo    = ExifGpsReader.Leer(ruta);

                if (_geo != null)
                {
                    _lblLat.Text   = _geo.Lat.ToString("F6");
                    _lblLon.Text   = _geo.Lon.ToString("F6");
                    _lblFecha.Text = _geo.DateTaken ?? "—";
                    _lblCam.Text   = _geo.Camera   ?? "—";
                    _lblSize.Text  = $"{_geo.Width}×{_geo.Height}";
                    _lblInfo.ForeColor = Color.FromArgb(40, 200, 80);
                    _lblInfo.Text      = "✔ GPS encontrado — clic en el mapa para cambiar ubicación";
                    _lblStatus.Text    = "✓ GPS encontrado";
                    _mapa.DocumentText = BuildHtml(_geo.Lat, _geo.Lon);
                }
                else
                {
                    using var tmp  = Image.FromFile(ruta);
                    _lblSize.Text      = $"{tmp.Width}×{tmp.Height}";
                    _lblLat.Text = _lblLon.Text = _lblFecha.Text = _lblCam.Text = "—";
                    _lblInfo.ForeColor = Color.OrangeRed;
                    _lblInfo.Text      = "✘ Sin GPS — haz clic en el mapa para asignar ubicación";
                    _lblStatus.Text    = "Sin GPS";
                    _mapa.DocumentText = BuildHtml();
                }
            }
            catch (Exception ex) { _lblStatus.Text = "✗ " + ex.Message; }
        }

        private static Bitmap CopiarConExif(string ruta)
        {
            using var src = Image.FromFile(ruta);
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(src, 0, 0);
            foreach (var p in src.PropertyItems)
                try { bmp.SetPropertyItem(p); } catch { /* fallback silencioso intencional */ }
            return bmp;
        }

        // ── Llamado por JavaScript ────────────────────────────────
        public void MarcarUbicacion(double lat, double lon)
        {
            _latSel = lat;
            _lonSel = lon;
            _lblLat.Text        = lat.ToString("F6");
            _lblLon.Text        = lon.ToString("F6");
            _btnGuardar.Enabled = true;
            _lblInfo.ForeColor  = Color.FromArgb(0, 120, 212);
            _lblInfo.Text       = $"📍 Seleccionado: {lat:F6}, {lon:F6}  — pulsa Guardar para confirmar";
        }

        // ── Guardar GPS en EXIF ───────────────────────────────────
        private void GuardarGPS()
        {
            if (_bitmap == null || string.IsNullOrEmpty(_ruta)) return;
            if (double.IsNaN(_latSel))
            { MessageBox.Show("Haz clic en el mapa primero.", "Sin ubicación"); return; }

            ExifGpsWriter.Escribir(_bitmap, _latSel, _lonSel);

            try
            {
                _bitmap.Save(_ruta, ImageFormat.Jpeg);

                // Actualizar estado
                _geo ??= new GeoInfo { Width = _bitmap.Width, Height = _bitmap.Height };
                _geo.Lat = _latSel;
                _geo.Lon = _lonSel;
                _lblLat.Text        = _latSel.ToString("F6");
                _lblLon.Text        = _lonSel.ToString("F6");
                _btnGuardar.Enabled = false;
                _lblInfo.ForeColor  = Color.FromArgb(40, 200, 80);
                _lblInfo.Text       = $"✔ GPS guardado: {_latSel:F6}, {_lonSel:F6}";

                // Recargar mapa con marcador 📷 en la nueva posición
                _mapa.DocumentText = BuildHtml(_latSel, _lonSel);

                MessageBox.Show("✓ GPS guardado en la imagen.", "Listo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al guardar"); }
        }

        private void AbrirImagen()
        {
            var path = Forms.FilePicker.Open(filter: ".jpg,.jpeg,.tiff,.tif");
            if (path != null) CargarImagen(path);
        }

        private void ExportarGeoJson()
        {
            if (_geo == null) { MessageBox.Show("Sin datos GPS para exportar."); return; }
            var savePath = Forms.FilePicker.Save(defaultName: "ubicacion.geojson"); if (savePath == null) return;
            string json = $"{{\n  \"type\":\"Feature\",\n  \"geometry\":{{\n    \"type\":\"Point\",\n    \"coordinates\":[{_geo.Lon},{_geo.Lat}]\n  }}\n}}";
            File.WriteAllText(savePath, json);
            MessageBox.Show("Exportado correctamente.");
        }

        private void AbrirGoogleMaps()
        {
            double lat = double.IsNaN(_latSel) ? (_geo?.Lat ?? 0) : _latSel;
            double lon = double.IsNaN(_lonSel) ? (_geo?.Lon ?? 0) : _lonSel;
            if (lat == 0 && lon == 0) { MessageBox.Show("Sin coordenadas disponibles."); return; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"https://www.google.com/maps?q={lat.ToString("F6", CultureInfo.InvariantCulture)},{lon.ToString("F6", CultureInfo.InvariantCulture)}",
                UseShellExecute = true
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _bitmap?.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ── Lector EXIF GPS (sin librerías externas) ──────────────────
    internal static class ExifGpsReader
    {
        private const int LatRef = 0x0001, Lat = 0x0002;
        private const int LonRef = 0x0003, Lon = 0x0004;
        private const int Date   = 0x9003, Cam = 0x0110;

        public static GeoInfo? Leer(string ruta)
        {
            try
            {
                using var img = Image.FromFile(ruta);
                double? lat = LeerGrado(img, Lat);
                double? lon = LeerGrado(img, Lon);
                if (lat == null || lon == null) return null;

                if (LeerTexto(img, LatRef) == "S") lat = -lat;
                if (LeerTexto(img, LonRef) == "W") lon = -lon;

                return new GeoInfo
                {
                    Lat       = lat.Value,
                    Lon       = lon.Value,
                    DateTaken = LeerTexto(img, Date),
                    Camera    = LeerTexto(img, Cam),
                    Width     = img.Width,
                    Height    = img.Height
                };
            }
            catch { return null; }
        }

        private static double? LeerGrado(Image img, int id)
        {
            try
            {
                var p = img.GetPropertyItem(id);
                if (p?.Value == null || p.Value.Length < 24) return null;
                double deg = Fraccion(p.Value, 0);
                double min = Fraccion(p.Value, 8);
                double sec = Fraccion(p.Value, 16);
                return deg + min / 60.0 + sec / 3600.0;
            }
            catch { return null; }
        }

        private static double Fraccion(byte[] b, int offset)
        {
            uint n = BitConverter.ToUInt32(b, offset);
            uint d = BitConverter.ToUInt32(b, offset + 4);
            return d == 0 ? 0 : (double)n / d;
        }

        private static string? LeerTexto(Image img, int id)
        {
            try
            {
                var p = img.GetPropertyItem(id);
                return p?.Value == null ? null : Encoding.ASCII.GetString(p.Value).TrimEnd('\0').Trim();
            }
            catch { return null; }
        }
    }

    // ── Escritor EXIF GPS (sin librerías externas) ────────────────
    internal static class ExifGpsWriter
    {
        public static void Escribir(Bitmap img, double lat, double lon)
        {
            img.SetPropertyItem(Crear(0x0001, 2,  2, Texto(lat >= 0 ? "N" : "S")));
            img.SetPropertyItem(Crear(0x0002, 5, 24, Racional(lat)));
            img.SetPropertyItem(Crear(0x0003, 2,  2, Texto(lon >= 0 ? "E" : "W")));
            img.SetPropertyItem(Crear(0x0004, 5, 24, Racional(lon)));
        }

        private static byte[] Racional(double valor)
        {
            valor     = Math.Abs(valor);
            uint deg  = (uint)valor;
            uint minN = (uint)((valor - deg) * 60 * 10000);
            var buf   = new byte[24];
            void W(uint v, int p) { Array.Copy(BitConverter.GetBytes(v), 0, buf, p, 4); }
            W(deg,   0); W(1,     4);
            W(minN,  8); W(10000, 12);
            W(0,    16); W(1,     20);
            return buf;
        }

        private static byte[] Texto(string s) => Encoding.ASCII.GetBytes(s + "\0");

        private static PropertyItem Crear(int id, short tipo, int len, byte[] datos)
        {
            var pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
            pi.Id = id; pi.Type = tipo; pi.Len = len; pi.Value = datos;
            return pi;
        }
    }
}
