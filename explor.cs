using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Metadatos de una canción extraídos desde una fuente musical pública.
    /// </summary>
    public record TrackInfo(
        string Path, string Title, string Artist, string Album,
        TimeSpan Duration, Image? Cover,
        int Popularity = 0, string ReleaseDate = "", string TrackUrl = "",
        string Lyrics = "");

    /// <summary>
    /// Servicio de búsqueda de metadatos musicales.
    /// Usa iTunes Search API (pública, sin autenticación) para título/artista/álbum/portada.
    /// Usa lyrics.ovh (pública, sin autenticación) para letras.
    /// </summary>
    internal class MusicMetadataService
    {
        private readonly HttpClient _http;

        public bool   IsAvailable   => true;
        public string LastError     { get; private set; } = "";
        public string LastSearchUrl { get; private set; } = "";

        public MusicMetadataService(HttpClient http) => _http = http;

        public async Task<TrackInfo?> SearchAsync(string artist, string title, string localPath)
        {
            try
            {
                var query = string.IsNullOrEmpty(artist) ? title : artist + " " + title;
                LastSearchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=song&limit=1";

                var resp = await _http.GetAsync(LastSearchUrl);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    LastError = $"iTunes HTTP {(int)resp.StatusCode}";
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var results = doc.RootElement.GetProperty("results");
                if (results.GetArrayLength() == 0)
                {
                    LastError = "Sin resultados";
                    return null;
                }

                LastError = "";
                return await ParseTrackAsync(results[0], localPath);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        private async Task<TrackInfo> ParseTrackAsync(JsonElement item, string path)
        {
            string title    = GetString(item, "trackName");
            string artist   = GetString(item, "artistName");
            string album    = GetString(item, "collectionName");
            string release  = GetString(item, "releaseDate");
            string trackUrl = GetString(item, "trackViewUrl");
            var duration    = item.TryGetProperty("trackTimeMillis", out var d)
                ? TimeSpan.FromMilliseconds(d.GetInt32()) : TimeSpan.Zero;

            var cover  = await DownloadAlbumCoverAsync(item);
            var lyrics = await DownloadLyricsAsync(artist, title);

            return new TrackInfo(path, title, artist, album, duration,
                cover, 0, release, trackUrl, lyrics);
        }

        private async Task<Image?> DownloadAlbumCoverAsync(JsonElement item)
        {
            try
            {
                if (!item.TryGetProperty("artworkUrl100", out var aw)) return null;
                var lowResUrl = aw.GetString();
                if (string.IsNullOrEmpty(lowResUrl)) return null;
                var highResUrl = lowResUrl.Replace("100x100", "600x600");
                var bytes = await _http.GetByteArrayAsync(highResUrl);
                return Image.FromStream(new MemoryStream(bytes));
            }
            catch { return null; }
        }

        private async Task<string> DownloadLyricsAsync(string artist, string title)
        {
            if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title)) return "";
            try
            {
                var primaryArtist = artist.Split(',')[0].Trim();
                var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(primaryArtist)}/{Uri.EscapeDataString(title)}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return "";
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                return doc.RootElement.TryGetProperty("lyrics", out var lyrics)
                    ? lyrics.GetString() ?? ""
                    : "";
            }
            catch { return ""; }
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";
    }
}
