using AForge.Video.DirectShow;
using Explorador_de_Archivo;
using Explorador_de_Archivo.Models;
using Explorador_de_Archivo.Services;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Explorador_de_Archivo.Services.ConverterService;
using FileExplorer.Forms;
using static Explorador_de_Archivo.Forms.UiHelpers;

namespace Explorador_de_Archivo.Forms
{
    public class RecorderForm : Form
    {
        // Captura de audio
        private WaveInEvent?    _audioInput;
        private WaveFileWriter? _audioFile;

        // Reproducción inline de grabaciones (NO reproduce canciones, solo grabaciones)
        private NAudio.Wave.WaveOutEvent?    _playback;
        private NAudio.Wave.AudioFileReader? _playbackReader;
        private Label                        _playbackStatus = null!;
        private Button                       _stopPlaybackBtn = null!;
        private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
        private int     _secondsElapsed;
        private string? _outputFilePath;
        private bool    _isPaused;

        private Label   _clockDisplay  = null!;
        private Button  _recordButton  = null!, _pauseButton = null!, _stopButton = null!;
        private ListBox _recordingList = null!;
        private Label   _statusLabel   = null!;

        public RecorderForm()
        {
            Theme.ApplyForm(this);
            Text = "Grabar Audio";
            Size = new Size(460, 430);
            BuildUI();
            _elapsedTimer.Tick += (_, _) => UpdateClock();
        }

        private void BuildUI()
        {
            var layout = BuildLayout();
            layout.Controls.Add(_clockDisplay   = BuildClockDisplay(),   0, 0);
            layout.Controls.Add(BuildControlButtons(),                    0, 1);
            layout.Controls.Add(BuildRecordingsLabel(),                   0, 2);
            layout.Controls.Add(BuildRecordingsList(),                    0, 3);
            layout.Controls.Add(BuildPlaybackBar(),                       0, 4);
            layout.Controls.Add(BuildCloseButton(),                       0, 5);
            _statusLabel = BuildStatusLabel();
            Controls.Add(layout);
        }

        private static TableLayoutPanel BuildLayout()
        {
            var layout = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
              BackColor = Theme.BgMain, Padding = new Padding(14, 10, 14, 10) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // 0 clock
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));   // 1 controls
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));   // 2 label
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 3 recordings list
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // 4 playback bar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // 5 close
            return layout;
        }

        private static Label BuildClockDisplay() =>
            new() { Text = "00:00", Font = new Font("Cascadia Code", 34f, FontStyle.Bold),
                    ForeColor = Theme.Red, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter };

        private FlowLayoutPanel BuildControlButtons()
        {
            var panel = new FlowLayoutPanel
            { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };

            _recordButton = Theme.FlatBtn("⏺ Grabar",  120, 44);
            _recordButton.BackColor = Theme.Red;
            _recordButton.ForeColor = Color.White;
            _pauseButton  = Theme.FlatBtn("⏸ Pausar",  120, 44); _pauseButton.Enabled = false;
            _stopButton   = Theme.FlatBtn("⏹ Detener", 120, 44); _stopButton.Enabled  = false;

            _recordButton.Click += (_, _) => StartRecording();
            _pauseButton.Click  += (_, _) => TogglePause();
            _stopButton.Click   += (_, _) => StopRecording();

            panel.Controls.AddRange(new Control[] { _recordButton, _pauseButton, _stopButton });
            return panel;
        }

        private static Label BuildRecordingsLabel() =>
            new() { Text = "🎤 Grabaciones (doble clic para escuchar)",
                    ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, Dock = DockStyle.Fill };

        private ListBox BuildRecordingsList()
        {
            _recordingList = new ListBox
            { BackColor = Theme.BgPanel, ForeColor = Theme.TxtSub,
              Font = Theme.FontMono, BorderStyle = BorderStyle.FixedSingle,
              Dock = DockStyle.Fill };
            _recordingList.DoubleClick += (_, _) =>
            {
                if (_recordingList.SelectedItem is string path && File.Exists(path))
                    PlayRecordingInline(path);
            };
            return _recordingList;
        }

        private static Label BuildStatusLabel() =>
            new() { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, Dock = DockStyle.Fill };

        private Button BuildCloseButton()
        {
            var btn = Theme.FlatBtn("✖ Cerrar", 0, 34);
            btn.Dock = DockStyle.Fill; btn.ForeColor = Theme.Red;
            btn.Click += (_, _) => { CleanupRecording(); Close(); };
            return btn;
        }

        private void StartRecording()
        {
            if (_audioInput != null) return;
            _secondsElapsed = 0;
            _isPaused       = false;
            _outputFilePath = BuildRecordingFilePath();
            InitializeAudioCapture();
            _audioInput!.StartRecording();
            _elapsedTimer.Start();
            SetRecordingState(active: true);
        }

        private static string BuildRecordingFilePath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                $"grabacion_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        private void InitializeAudioCapture()
        {
            _audioInput = new WaveInEvent { WaveFormat = new WaveFormat(44100, 1) };
            _audioInput.DataAvailable    += OnAudioDataAvailable;
            _audioInput.RecordingStopped += OnRecordingStopped;
            _audioFile = new WaveFileWriter(_outputFilePath, _audioInput.WaveFormat);
        }

        private void TogglePause()
        {
            if (_audioInput == null) return;
            if (!_isPaused) PauseRecording();
            else ResumeRecording();
        }

        private void PauseRecording()
        {
            _audioInput!.StopRecording();
            _elapsedTimer.Stop();
            _isPaused          = true;
            _pauseButton.Text  = "▶ Continuar";
            _statusLabel.Text  = "⏸ Pausado";
        }

        private void ResumeRecording()
        {
            InitializeAudioCapture();
            _audioInput!.StartRecording();
            _elapsedTimer.Start();
            _isPaused          = false;
            _pauseButton.Text  = "⏸ Pausar";
            _statusLabel.Text  = "● Grabando...";
        }

        private void StopRecording()
        {
            if (_audioInput == null) return;
            _stopButton.Enabled  = false;
            _pauseButton.Enabled = false;
            _elapsedTimer.Stop();
            _audioInput.StopRecording();
        }

        private void SetRecordingState(bool active)
        {
            _recordButton.Enabled  = !active;
            _stopButton.Enabled    = active;
            _pauseButton.Enabled   = active;
            _statusLabel.Text      = active ? "● Grabando..." : "";
        }

        private void UpdateClock()
        {
            _secondsElapsed++;
            _clockDisplay.Text = $"{_secondsElapsed / 60:D2}:{_secondsElapsed % 60:D2}";
        }

        private void OnAudioDataAvailable(object? s, WaveInEventArgs e) =>
            _audioFile?.Write(e.Buffer, 0, e.BytesRecorded);

        private void OnRecordingStopped(object? s, StoppedEventArgs e)
        {
            _audioFile?.Flush(); _audioFile?.Dispose(); _audioFile = null;
            _audioInput?.Dispose(); _audioInput = null;
            if (!_isPaused) BeginInvoke(OnRecordingFinished);
        }

        private void OnRecordingFinished()
        {
            SetRecordingState(active: false);
            _pauseButton.Text = "⏸ Pausar";
            _recordingList.Items.Add(_outputFilePath ?? "");
            ShowSuccess(_statusLabel, "✓ " + Path.GetFileName(_outputFilePath));
        }

        /// <summary>
        /// Reproduce una grabación de audio dentro del propio formulario,
        /// sin abrir el reproductor de música.
        /// </summary>
        private void PlayRecordingInline(string path)
        {
            StopInlinePlayback();
            try
            {
                _playbackReader = new NAudio.Wave.AudioFileReader(path);
                _playback       = new NAudio.Wave.WaveOutEvent();
                _playback.Init(_playbackReader);
                _playback.PlaybackStopped += (_, _) => BeginInvoke(() =>
                {
                    StopInlinePlayback();
                    _playbackStatus.Text = "✓ Terminó: " + Path.GetFileName(path);
                });
                _playback.Play();
                _playbackStatus.Text  = "▶ Reproduciendo: " + Path.GetFileName(path);
                _stopPlaybackBtn.Enabled = true;
            }
            catch (Exception ex) { _playbackStatus.Text = "Error: " + ex.Message; }
        }

        private void StopInlinePlayback()
        {
            _playback?.Stop();
            _playback?.Dispose();
            _playbackReader?.Dispose();
            _playback       = null;
            _playbackReader = null;
            if (_stopPlaybackBtn != null) _stopPlaybackBtn.Enabled = false;
        }

        private FlowLayoutPanel BuildPlaybackBar()
        {
            var bar = new FlowLayoutPanel
            { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _stopPlaybackBtn = Theme.FlatBtn("⏹ Detener", 110, 26);
            _stopPlaybackBtn.Enabled = false;
            _stopPlaybackBtn.Click += (_, _) =>
            {
                StopInlinePlayback();
                _playbackStatus.Text = "Detenido";
            };
            _playbackStatus = new Label
            { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true,
              Padding = new Padding(8, 4, 0, 0) };
            bar.Controls.AddRange(new Control[] { _stopPlaybackBtn, _playbackStatus });
            return bar;
        }

        private void CleanupRecording()
        {
            _elapsedTimer.Stop();
            _audioInput?.StopRecording(); _audioInput?.Dispose();
            _audioFile?.Flush(); _audioFile?.Dispose();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CleanupRecording();
            StopInlinePlayback();
            base.OnFormClosed(e);
        }
    }

    // ── Limpiador de datos ────────────────────────────────────────
}
