using Explorador_de_Archivo;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XComponent.SliderBar;

namespace FileExplorer.Forms;

public record SongInfo(int Num, string Title, string Artist, string Album, string Year, string Duration, string Path, Image? Cover);

public class MusicTableForm : Form
{
    private readonly string _folder;
    private readonly List<SongInfo> _songs = new();
    private DataGridView _grid = null!;
    private Label _status = null!;

    public MusicTableForm(string folder)
    {
        _folder = folder;
        Theme.ApplyForm(this);
        Text = "Biblioteca de Musica";
        Size = new Size(960, 580);
        Build();
        LoadSongs();
    }

    private void Build()
    {
        var bar = new FlowLayoutPanel
        { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgPanel, Padding = new Padding(8, 8, 0, 0) };
        var play   = Theme.AccentBtn("Reproducir", 150, 30);
        var reload = Theme.FlatBtn("Recargar",     100, 30);
        _status = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontMono, AutoSize = true };
        play.Click   += (_, _) => PlaySelected();
        reload.Click += (_, _) => LoadSongs();
        bar.Controls.AddRange(new Control[] { play, reload, _status });

        _grid = Theme.Grid();
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (var (h, n, w) in new[]
        {
            ("#", "Num", 40), ("Titulo", "Title", 260), ("Artista", "Artist", 180),
            ("Album", "Album", 180), ("Anio", "Year", 60), ("Duracion", "Dur", 80), ("Ruta", "Path", 0)
        })
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = h, Name = n, Width = w, Visible = n != "Path" });

        _grid.Dock = DockStyle.Fill;
        _grid.DoubleClick += (_, _) => PlaySelected();
        Controls.AddRange(new Control[] { bar, _grid });
    }

    private void LoadSongs()
    {
        _songs.Clear(); _grid.Rows.Clear(); _status.Text = "Cargando...";
        var exts  = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };
        var files = Directory.Exists(_folder)
            ? Directory.GetFiles(_folder, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f).ToList()
            : new List<string>();
        int n = 1;
        foreach (var f in files)
        {
            var s = ReadTags(f, n++);
            _songs.Add(s);
            _grid.Rows.Add(s.Num, s.Title, s.Artist, s.Album, s.Year, s.Duration, s.Path);
        }
        for (int i = 0; i < _grid.Rows.Count; i++)
            _grid.Rows[i].DefaultCellStyle.BackColor =
                i % 2 == 0 ? Theme.BgMain : Color.FromArgb(28, 28, 28);
        _status.Text = "  " + _songs.Count + " cancion(es)";
    }

    private static SongInfo ReadTags(string path, int num)
    {
        try
        {
            using var r = new AudioFileReader(path);
            return new SongInfo(num, Path.GetFileNameWithoutExtension(path),
                "—", "—", "—", FormatTime(r.TotalTime), path, null);
        }
        catch { return new SongInfo(num, Path.GetFileNameWithoutExtension(path), "—", "—", "—", "—", path, null); }
    }

    private void PlaySelected()
    {
        if (_grid.CurrentRow == null) return;
        var path = _grid.CurrentRow.Cells["Path"].Value?.ToString();
        if (string.IsNullOrEmpty(path)) return;
        new AudioPlayerForm(_songs.Select(s => s.Path).ToList(),
            _songs.FindIndex(s => s.Path == path)).Show();
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
}

public class AudioPlayerForm : Form
{
    // ── Audio engine ──────────────────────────────────────────
    private WaveOutEvent?    _out;
    private AudioFileReader? _reader;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };

    // ── Metadatos musicales ───────────────────────────────────────────────
    private readonly MusicMetadataService _metadata = new(new System.Net.Http.HttpClient());

    // ── Playlist ──────────────────────────────────────────────
    private List<string> _playlist = new();
    private int          _current;
    private bool         _shuffle, _repeat;
    private readonly Random _rnd = new();

    // ── Controles ─────────────────────────────────────────────
    private PictureBox   _cover      = null!;
    private Label        _title      = null!, _artist = null!, _album = null!,
                         _lyrics     = null!, _time   = null!;
    private MACTrackBar  _seek       = null!, _vol    = null!;
    private Button       _playBtn    = null!, _shuffleBtn = null!, _repeatBtn = null!;
    private Panel        _lyricsPanel = null!;

    // ── Constructores ─────────────────────────────────────────
    public AudioPlayerForm(string? path = null)
        : this(path != null ? new List<string> { path } : new List<string>(), 0) { }

    public AudioPlayerForm(List<string> playlist, int start = 0)
    {
        Theme.ApplyForm(this);
        Text        = "Reproductor de Audio";
        Size        = new Size(650, 760);
        MinimumSize = new Size(650, 760);
        Build();
        _timer.Tick += Tick;
        _playlist = playlist;
        _current  = Math.Max(0, Math.Min(start, playlist.Count - 1));
        if (_playlist.Count > 0) LoadCurrent();
    }

    // ── Construccion de UI ────────────────────────────────────
    private void Build()
    {
        BackColor = Theme.BgMain;
        var tbl = BuildLayout();
        tbl.Controls.Add(BuildCoverPanel(), 0, 0);
        _title  = CenteredLabel("Sin cancion", new Font("Segoe UI", 12f, FontStyle.Bold), Theme.TxtMain);
        _artist = CenteredLabel("—", Theme.FontNormal, Theme.TxtSub);
        _album  = CenteredLabel("—", Theme.FontSmall,  Theme.TxtMuted);
        tbl.Controls.Add(_title,  0, 1);
        tbl.Controls.Add(_artist, 0, 2);
        tbl.Controls.Add(_album,  0, 3);
        tbl.Controls.Add(BuildSeekBar(),      0, 4);
        tbl.Controls.Add(BuildTimeLabel(),    0, 5);
        tbl.Controls.Add(BuildControlRow(),   0, 6);
        tbl.Controls.Add(BuildBottomRow(),    0, 7);
        tbl.Controls.Add(BuildLyricsPanel(),  0, 8);
        Controls.Add(tbl);
    }

    private static TableLayoutPanel BuildLayout()
    {
        var tbl = new TableLayoutPanel
        { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, BackColor = Theme.BgMain };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  28));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  22));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  18));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  26));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  20));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  50));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  36));
        tbl.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
        return tbl;
    }

    private Panel BuildCoverPanel()
    {
        _cover = new PictureBox
        { Size = new Size(170, 170), SizeMode = PictureBoxSizeMode.Zoom,
          BackColor = Theme.BgPanel, Anchor = AnchorStyles.None };
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(_cover);
        panel.Resize += (_, _) => { _cover.Left = (panel.Width - 170) / 2; _cover.Top = 8; };
        SetDefaultCover();
        return panel;
    }

    private MACTrackBar BuildSeekBar()
    {
        _seek = Theme.TrackBar(0, 1000, 0); _seek.Dock = DockStyle.Fill;
        _seek.Scroll += (_, _) =>
        {
            if (_reader != null && _reader.TotalTime.TotalSeconds > 0)
                _reader.CurrentTime = TimeSpan.FromSeconds(
                    _seek.Value / 1000.0 * _reader.TotalTime.TotalSeconds);
        };
        return _seek;
    }

    private Label BuildTimeLabel()
    {
        _time = new Label
        { Text = "0:00 / 0:00", Font = Theme.FontMono, ForeColor = Theme.TxtMuted,
          Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        return _time;
    }

    private FlowLayoutPanel BuildControlRow()
    {
        var row = new FlowLayoutPanel
        { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(10) };

        Button Ctrl(string t, int w, Action a, bool accent = false)
        {
            var b = accent ? Theme.AccentBtn(t, w, 44) : Theme.FlatBtn(t, w, 44);
            b.Font = new Font("Segoe UI", 12f); b.Click += (_, _) => a(); return b;
        }

        _shuffleBtn = Ctrl("⇄", 50, ToggleShuffle);
        _playBtn    = Ctrl("▶", 70, Toggle, true);
        _repeatBtn  = Ctrl("↺", 50, ToggleRepeat);

        row.Controls.Add(_shuffleBtn);
        row.Controls.Add(Ctrl("⏮", 50, PrevTrack));
        row.Controls.Add(Ctrl("⏪", 60, () => Step(-10)));
        row.Controls.Add(_playBtn);
        row.Controls.Add(Ctrl("⏩", 60, () => Step(10)));
        row.Controls.Add(Ctrl("⏭", 50, NextTrack));
        row.Controls.Add(_repeatBtn);
        return row;
    }

    private FlowLayoutPanel BuildBottomRow()
    {
        var row = new FlowLayoutPanel
        { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 2, 4, 0) };

        row.Controls.Add(new Label
        { Text = "🔊", ForeColor = Theme.TxtSub, Font = Theme.FontLarge,
          AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });

        _vol = Theme.TrackBar(0, 100, 80); _vol.Width = 110; _vol.AutoSize = false; _vol.Height = 26;
        _vol.ValueChanged += (_, _) => { if (_out != null) _out.Volume = _vol.Value / 100f; };
        row.Controls.Add(_vol);

        var stopBtn = Theme.FlatBtn("⏹", 44, 28); stopBtn.Click += (_, _) => StopPlayback();
        var lyrBtn  = Theme.FlatBtn("📝 Letra", 120, 28); lyrBtn.Click  += (_, _) => ToggleLyrics();

        row.Controls.AddRange(new Control[] { stopBtn, lyrBtn });
        return row;
    }

    private Panel BuildLyricsPanel()
    {
        _lyrics = new Label
        { Dock = DockStyle.Top, ForeColor = Theme.TxtSub, Font = Theme.FontNormal,
          TextAlign = ContentAlignment.MiddleCenter, AutoSize = true,
          Text = "No hay letra disponible.", Padding = new Padding(12) };
        _lyricsPanel = new Panel
        { Dock = DockStyle.Fill, BackColor = Theme.BgPanel, Visible = false,
          AutoScroll = true };
        _lyricsPanel.Controls.Add(_lyrics);
        return _lyricsPanel;
    }

    private static Label CenteredLabel(string text, Font font, Color color) =>
        new() { Text = text, Font = font, ForeColor = color,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };

    // ── Carga de pista ────────────────────────────────────────

    private void LoadCurrent()
    {
        if (_playlist.Count == 0) return;
        _current = Math.Max(0, Math.Min(_current, _playlist.Count - 1));
        LoadFile(_playlist[_current]);
    }

    private void LoadFile(string path)
    {
        StopPlayback();
        try
        {
            _reader = new AudioFileReader(path);
            _out    = new WaveOutEvent { Volume = _vol.Value / 100f };
            _out.Init(_reader);
            _out.PlaybackStopped += OnStopped;
            _out.Play();
            _timer.Start();
            _playBtn.Text = "⏸";
            _title.Text   = Path.GetFileNameWithoutExtension(path);
            _artist.Text  = "—";
            _album.Text   = "—";
            _lyrics.Text  = "Cargando metadatos...";
            SetDefaultCover();
            FetchTrackMetadata(path);  // Y luego enriquece con iTunes
        }
        catch (Exception ex)
        { _title.Text = Path.GetFileNameWithoutExtension(path); _artist.Text = ex.Message; }
    }



    // ── Metadatos musicales ───────────────────────────────────────────────

    /// <summary>
    /// Quita sufijos comunes de nombres de canciones que confunden la búsqueda:
    /// (Official Video), [HD], ft. Artista, etc.
    /// </summary>
    private static string CleanTrackName(string name)
    {
        var ic = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        name = System.Text.RegularExpressions.Regex.Replace(
            name, @"\s*[\(\[](Official|Video|Audio|Lyric|Lyrics|Music|HD|HQ|MV|Visualizer)[^)\]]*[\)\]]",
            "", ic).Trim();
        name = System.Text.RegularExpressions.Regex.Replace(
            name, @"\s+(ft\.?|feat\.?|featuring)\s+.+$",
            "", ic).Trim();
        return name;
    }

    private void FetchTrackMetadata(string path)
    {
        if (!_metadata.IsAvailable)
        {
            _lyrics.Text = "Servicio de metadatos no disponible.";
            return;
        }
        var name = Path.GetFileNameWithoutExtension(path);
        name = CleanTrackName(name);

        var artist = "";
        if (name.Contains(" - "))
        {
            var parts = name.Split(new[] { " - " }, 2, StringSplitOptions.None);
            artist = parts[0].Trim();
            name   = parts[1].Trim();
        }

        _lyrics.Text = "Buscando \"" + name + "\"...";

        Task.Run(async () =>
        {
            try
            {
                var info = await _metadata.SearchAsync(artist, name, path);
                if (IsDisposed) return;
                if (info == null)
                    BeginInvoke(() => _lyrics.Text = "No se encontró:\n" +
                        (string.IsNullOrEmpty(artist) ? name : artist + " - " + name));
                else
                    BeginInvoke(() => ApplyTrackInfo(info));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => _lyrics.Text = "Error API: " + ex.Message);
            }
        });
    }

    private void ApplyTrackInfo(TrackInfo info)
    {
        _title.Text  = info.Title;
        _artist.Text = info.Artist;
        _album.Text  = info.Album;
        Text         = info.Title + " — " + info.Artist;
        if (info.Cover != null) _cover.Image = info.Cover;
        _lyrics.Text = string.IsNullOrWhiteSpace(info.Lyrics)
            ? "No se encontró letra para esta canción."
            : info.Lyrics;
    }

    // ── Portada por defecto ───────────────────────────────────

    private void SetDefaultCover()
    {
        var bmp = new Bitmap(200, 200);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Theme.BgPanel);
        using var f  = new Font("Segoe UI", 60f);
        var sz = g.MeasureString("🎵", f);
        using var br = new SolidBrush(Theme.Accent);
        g.DrawString("🎵", f, br, (200 - sz.Width) / 2, (200 - sz.Height) / 2);
        _cover.Image = bmp;
    }

    // ── Controles de reproduccion ─────────────────────────────

    private void StopPlayback()
    {
        _timer.Stop();
        _out?.Stop(); _out?.Dispose();
        _reader?.Dispose();
        _out = null; _reader = null;
        if (_playBtn != null) _playBtn.Text = "▶";
    }

    private void Toggle()
    {
        if (_out == null) { OpenFile(); return; }
        if (_out.PlaybackState == PlaybackState.Playing)
        { _out.Pause(); _timer.Stop(); _playBtn.Text = "▶"; }
        else
        { _out.Play(); _timer.Start(); _playBtn.Text = "⏸"; }
    }

    private void Step(int seconds)
    {
        if (_reader == null) return;
        var t = _reader.CurrentTime + TimeSpan.FromSeconds(seconds);
        _reader.CurrentTime = t < TimeSpan.Zero ? TimeSpan.Zero
            : t > _reader.TotalTime ? _reader.TotalTime : t;
    }

    /// <summary>
    /// Si la playlist es de una sola pista, carga todas las del mismo directorio
    /// para que prev/next/shuffle puedan navegar entre canciones reales.
    /// </summary>
    private void EnsurePlaylistFromFolder()
    {
        if (_playlist.Count > 1) return;
        if (_playlist.Count == 0) return;

        var folder = Path.GetDirectoryName(_playlist[0]);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        var audioExts = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };
        var siblings  = Directory.GetFiles(folder)
            .Where(f => audioExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (siblings.Count < 2) return;

        var currentPath = _playlist[_current];
        _playlist = siblings;
        _current  = _playlist.IndexOf(currentPath);
        if (_current < 0) _current = 0;
    }

    private void NextTrack()
    {
        EnsurePlaylistFromFolder();
        if (_playlist.Count == 0) return;
        _current = _shuffle
            ? PickRandomIndex()
            : (_current + 1) % _playlist.Count;
        LoadCurrent();
    }

    private void PrevTrack()
    {
        EnsurePlaylistFromFolder();
        if (_playlist.Count == 0) return;
        if (_reader != null && _reader.CurrentTime.TotalSeconds > 3)
        { _reader.CurrentTime = TimeSpan.Zero; return; }
        _current = (_current - 1 + _playlist.Count) % _playlist.Count;
        LoadCurrent();
    }

    private int PickRandomIndex()
    {
        if (_playlist.Count <= 1) return 0;
        int next;
        do { next = _rnd.Next(_playlist.Count); } while (next == _current);
        return next;
    }

    private void ToggleShuffle()
    {
        _shuffle = !_shuffle;
        _shuffleBtn.BackColor = _shuffle ? Theme.Accent : Theme.BgControl;
        _shuffleBtn.ForeColor = _shuffle ? Color.White : Theme.TxtMain;
        if (_shuffle) EnsurePlaylistFromFolder();
    }

    private void ToggleRepeat()
    {
        _repeat = !_repeat;
        _repeatBtn.BackColor = _repeat ? Theme.Accent : Theme.BgControl;
        _repeatBtn.ForeColor = _repeat ? Color.White : Theme.TxtMain;
    }

    private void ToggleLyrics() => _lyricsPanel.Visible = !_lyricsPanel.Visible;

    private void OnStopped(object? s, StoppedEventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed) return;
            if (_repeat) { if (_reader != null) _reader.CurrentTime = TimeSpan.Zero; _out?.Play(); }
            else if (_playlist.Count > 1) NextTrack();
        }));
    }

    private void Tick(object? s, EventArgs e)
    {
        if (_reader == null) return;
        var cur = _reader.CurrentTime;
        var tot = _reader.TotalTime;
        if (tot.TotalSeconds > 0)
            _seek.Value = (int)(cur.TotalSeconds / tot.TotalSeconds * 1000);
        _time.Text = FormatTime(cur) + " / " + FormatTime(tot);
    }

    private void OpenFile()
    {
        var path = Explorador_de_Archivo.Forms.FilePicker.Open(filter: ".mp3,.wav,.flac,.aac,.ogg,.wma,.m4a");
        if (path == null) return;
        _playlist = new List<string> { path }; _current = 0; LoadCurrent();
    }


    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";

    protected override void OnFormClosed(FormClosedEventArgs e)
    { StopPlayback(); base.OnFormClosed(e); }
}

public class VideoPlayerForm : Form
{
    private LibVLC?      _vlc;
    private MediaPlayer? _player;
    private VideoView    _view    = null!;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private bool   _seeking, _muted, _repeat, _shuffle;
    private string _loadedPath = "";
    private readonly List<string> _playlist = new();
    private int    _current;
    private readonly Random _rnd = new();

    private Label      _noVideo = null!, _time = null!, _info = null!;
    private MACTrackBar _seek   = null!, _vol  = null!;
    private Button     _playBtn = null!, _muteBtn = null!, _repeatBtn = null!, _shuffleBtn = null!;
    private ComboBox   _speedBox = null!;

    public VideoPlayerForm(string? path = null)
    {
        Theme.ApplyForm(this);
        Text        = "Reproductor de Video";
        Size        = new Size(960, 640);
        MinimumSize = new Size(640, 460);
        Core.Initialize();
        _vlc    = new LibVLC();
        _player = new MediaPlayer(_vlc);
        Build();
        _timer.Tick += UiTick;
        if (!string.IsNullOrEmpty(path)) { _playlist.Add(path); _current = 0; LoadFile(path); }
    }

    private void Build()
    {
        _view = new VideoView { Dock = DockStyle.Fill, BackColor = Color.Black, MediaPlayer = _player };
        _noVideo = new Label
        { Text = "🎬\n\nHaz clic en  📂  para cargar un video\nMP4 · AVI · MKV · MOV · WMV",
          Font = Theme.FontLarge, ForeColor = Theme.TxtMuted, BackColor = Color.Transparent,
          TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        _view.Controls.Add(_noVideo);
        _view.DoubleClick += (_, _) => ToggleFullscreen();

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 140, BackColor = Theme.BgPanel, Padding = new Padding(6) };
        _seek = Theme.TrackBar(0, 1000, 0); _seek.Dock = DockStyle.Top; _seek.Height = 28;
        _seek.MouseDown += (_, _) => _seeking = true;
        _seek.MouseUp   += SeekUp;

        var row1 = BuildVideoControlRow();
        var row2 = BuildVideoSecondRow();

        bar.Controls.AddRange(new Control[] { row2, row1, _seek });
        Controls.AddRange(new Control[] { _view, bar });
    }

    private FlowLayoutPanel BuildVideoControlRow()
    {
        var row = new FlowLayoutPanel
        { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent, Padding = new Padding(0, 2, 0, 0) };

        Button B(string t, int w, Action a, bool accent = false)
        { var b = accent ? Theme.AccentBtn(t, w, 32) : Theme.FlatBtn(t, w, 32); b.Click += (_, _) => a(); return b; }

        _playBtn = B("▶", 52, TogglePlay, true);
        row.Controls.Add(B("📂", 44, OpenFile));
        row.Controls.Add(B("⏮", 34, () => SeekTo(0)));
        row.Controls.Add(B("⏪", 52, () => Step(-10)));
        row.Controls.Add(_playBtn);
        row.Controls.Add(B("⏩", 52, () => Step(10)));
        row.Controls.Add(B("⏭", 34, NextVideo));
        return row;
    }

    private FlowLayoutPanel BuildVideoSecondRow()
    {
        var row = new FlowLayoutPanel
        { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent, Padding = new Padding(0, 2, 0, 0) };

        Button B(string t, int w, Action a)
        { var b = Theme.FlatBtn(t, w, 32); b.Click += (_, _) => a(); return b; }

        _muteBtn    = B("🔊", 36, ToggleMute);
        _shuffleBtn = B("⇄", 36, ToggleShuffle);
        _repeatBtn  = B("↺", 36, ToggleRepeat);

        _vol = Theme.TrackBar(0, 100, 80); _vol.Width = 110; _vol.AutoSize = false; _vol.Height = 28;
        _vol.ValueChanged += (_, _) => ApplyVol();

        _speedBox = Theme.ComboBox(); _speedBox.Width = 72;
        _speedBox.Items.AddRange(new object[] { "0.25×", "0.5×", "1×", "1.5×", "2×", "3×" });
        _speedBox.SelectedIndex = 2;
        _speedBox.SelectionChangeCommitted += SpeedChanged;

        _time = new Label { Text = "0:00 / 0:00", ForeColor = Theme.TxtMuted, Font = Theme.FontMono, AutoSize = true };
        _info = new Label { ForeColor = Theme.TxtMuted, Font = new Font("Segoe UI", 9f), AutoSize = true };

        row.Controls.AddRange(new Control[]
        { _muteBtn, _vol, _shuffleBtn, _repeatBtn,
          B("⛶", 36, ToggleFullscreen), B("📸", 36, Capture),
          _speedBox, _time, _info });
        return row;
    }

    private void LoadFile(string path)
    {
        if (_player == null || _vlc == null) return;
        _loadedPath = path; _noVideo.Visible = false;
        var m = new Media(_vlc, path, FromType.FromPath);
        _player.Media = m; m.Dispose();
        _player.EndReached += (_, _) => BeginInvoke(() =>
        { if (_repeat) { _player.Stop(); _player.Play(); } else NextVideo(); });
        _player.Play(); _playBtn.Text = "⏸"; _timer.Start(); ApplyVol();
        _info.Text = "  " + Path.GetFileName(path);
    }

    private void TogglePlay()
    {
        if (_player == null) return;
        if (string.IsNullOrEmpty(_loadedPath)) { OpenFile(); return; }
        if (_player.IsPlaying) { _player.Pause(); _playBtn.Text = "▶"; _timer.Stop(); }
        else { _player.Play(); _playBtn.Text = "⏸"; _timer.Start(); }
    }

    private void ToggleMute()   { _muted = !_muted; _muteBtn.Text = _muted ? "🔇" : "🔊"; if (_player != null) _player.Mute = _muted; }
    private void ToggleShuffle(){ _shuffle = !_shuffle; _shuffleBtn.BackColor = _shuffle ? Theme.Accent : Theme.BgControl; _shuffleBtn.ForeColor = _shuffle ? Color.White : Theme.TxtMain; }
    private void ToggleRepeat() { _repeat  = !_repeat;  _repeatBtn.BackColor  = _repeat  ? Theme.Accent : Theme.BgControl; _repeatBtn.ForeColor  = _repeat  ? Color.White : Theme.TxtMain; }
    private void ApplyVol()     { if (_player != null) _player.Volume = _vol.Value; }
    private void SeekTo(double s){ if (_player == null) return; _player.Time = Math.Max(0, Math.Min((long)(s * 1000), _player.Length)); }
    private void Step(int s)     { if (_player == null) return; SeekTo(_player.Time / 1000.0 + s); }
    private void NextVideo()     { if (_playlist.Count < 2) return; _current = _shuffle ? _rnd.Next(_playlist.Count) : (_current + 1) % _playlist.Count; LoadFile(_playlist[_current]); }

    private void SpeedChanged(object? s, EventArgs e)
    {
        float[] rates = { 0.25f, 0.5f, 1f, 1.5f, 2f, 3f };
        if (_player != null && _speedBox.SelectedIndex >= 0)
            _player.SetRate(rates[_speedBox.SelectedIndex]);
    }

    private void ToggleFullscreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        { FormBorderStyle = FormBorderStyle.Sizable; WindowState = FormWindowState.Normal; }
        else
        { FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; }
    }

    private void Capture()
    {
        var bmp = new Bitmap(_view.Width, _view.Height);
        _view.DrawToBitmap(bmp, _view.ClientRectangle);
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "frame_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg");
        bmp.Save(p, System.Drawing.Imaging.ImageFormat.Jpeg);
        MessageBox.Show("Guardado:\n" + p);
    }

    private void SeekUp(object? s, MouseEventArgs e)
    {
        _seeking = false;
        if (_player != null && _player.Length > 0)
            _player.Time = (long)(_seek.Value / 1000.0 * _player.Length);
    }

    private void UiTick(object? s, EventArgs e)
    {
        if (_player == null || _seeking) return;
        try
        {
            long cur = _player.Time, tot = _player.Length;
            if (tot > 0)
            {
                _seek.Value = (int)(cur * 1000.0 / tot);
                _time.Text  = FormatTime(TimeSpan.FromMilliseconds(cur)) + " / " +
                              FormatTime(TimeSpan.FromMilliseconds(tot));
            }
        }
        catch { /* fallback silencioso intencional */ }
    }

    private void OpenFile()
    {
        var path = Explorador_de_Archivo.Forms.FilePicker.Open(
            filter: ".mp4,.avi,.mkv,.mov,.wmv,.webm,.flv,.m4v");
        if (path == null) return;
        _playlist.Clear(); _playlist.Add(path); _current = 0; LoadFile(path);
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";

    protected override void OnFormClosed(FormClosedEventArgs e)
    { _timer.Stop(); _player?.Stop(); _player?.Dispose(); _vlc?.Dispose(); base.OnFormClosed(e); }
}
