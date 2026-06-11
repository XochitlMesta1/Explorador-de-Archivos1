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
    public class CameraForm : Form
    {
        private VideoCaptureDevice? _captureDevice;
        private Bitmap?    _latestFrame;
        private PictureBox _previewBox      = null!;
        private ComboBox   _cameraSelector  = null!;
        private Button     _startButton     = null!, _captureButton = null!, _stopButton = null!;
        private Button     _recordButton    = null!;
        private Label      _statusLabel     = null!;

        // Grabacion de video (AVI sin compresion - no requiere librerias externas)
        private AviVideoWriter? _videoWriter;
        private string?  _videoPath;
        private bool     _isRecording;
        private DateTime _recordStartTime;
        private readonly System.Windows.Forms.Timer _recordTimer = new() { Interval = 1000 };

        public CameraForm()
        {
            Theme.ApplyForm(this);
            Text        = "Cámara";
            Size        = new Size(740, 580);
            MinimumSize = new Size(500, 420);
            BuildUI();
            LoadAvailableCameras();
        }

        private void BuildUI()
        {
            _previewBox = BuildPreviewBox();
            var toolbar = BuildCameraToolbar();
            Controls.AddRange(new Control[] { _previewBox, toolbar });
        }

        private PictureBox BuildPreviewBox()
        {
            var box = new PictureBox
            { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            box.Controls.Add(new Label
            { Name = "placeholder", Text = "📷\n\nSelecciona una cámara\ny presiona Iniciar",
              ForeColor = Theme.TxtMuted, Font = Theme.FontLarge, Dock = DockStyle.Fill,
              TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            return box;
        }

        private FlowLayoutPanel BuildCameraToolbar()
        {
            var bar = Toolbar(DockStyle.Bottom, 60);
            _cameraSelector = Theme.ComboBox();
            _cameraSelector.Width = 220;
            _cameraSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _startButton   = ActionButton("▶ Iniciar",      90, StartCamera, accent: true);
            _captureButton = ActionButton("📸 Foto",       80, CapturePhoto);
            _recordButton  = ActionButton("🎬 Grabar",     90, ToggleVideoRecording);
            _stopButton    = ActionButton("⏹ Detener",     90, StopCamera);
            _statusLabel   = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontSmall, AutoSize = true };
            _captureButton.Enabled = _stopButton.Enabled = _recordButton.Enabled = false;
            _recordTimer.Tick += (_, _) => UpdateRecordingClock();
            bar.Controls.AddRange(new Control[]
            { _cameraSelector, _startButton, _captureButton, _recordButton, _stopButton, _statusLabel });
            return bar;
        }

        private void LoadAvailableCameras()
        {
            var cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo cam in cameras) _cameraSelector.Items.Add(cam.Name);
            if (_cameraSelector.Items.Count > 0) _cameraSelector.SelectedIndex = 0;
            else
            {
                _cameraSelector.Items.Add("Sin cámara");
                _cameraSelector.SelectedIndex = 0;
                _startButton.Enabled = false;
            }
        }

        private void StartCamera()
        {
            var cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _captureDevice = new VideoCaptureDevice(cameras[_cameraSelector.SelectedIndex].MonikerString);
            _captureDevice.NewFrame += OnNewFrame;
            _captureDevice.Start();
            SetCameraRunning(true);
        }

        private void OnNewFrame(object sender, AForge.Video.NewFrameEventArgs e)
        {
            var frame = (Bitmap)e.Frame.Clone();

            // Si esta grabando, escribir el frame al archivo AVI
            if (_isRecording && _videoWriter != null)
            {
                try { _videoWriter.WriteFrame(frame); }
                catch { /* skip frame on error */ }
            }

            _previewBox.BeginInvoke(() =>
            {
                _latestFrame?.Dispose();
                _latestFrame       = frame;
                _previewBox.Image  = frame;
            });
        }

        /// <summary>
        /// Inicia o detiene la grabacion de video desde la camara.
        /// El video se guarda en formato MP4 en la carpeta de Videos del usuario.
        /// </summary>
        private void ToggleVideoRecording()
        {
            if (!_isRecording) StartVideoRecording();
            else               StopVideoRecording();
        }

        private void StartVideoRecording()
        {
            if (_latestFrame == null)
            {
                MessageBox.Show("Inicia la cámara primero.");
                return;
            }
            try
            {
                _videoPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    $"video_{DateTime.Now:yyyyMMdd_HHmmss}.avi");

                _videoWriter = new AviVideoWriter(_videoPath,
                    _latestFrame.Width, _latestFrame.Height, framerate: 15);

                _isRecording      = true;
                _recordStartTime  = DateTime.Now;
                _recordButton.Text = "⏺ Detener";
                _recordButton.BackColor = Theme.Red;
                _recordButton.ForeColor = Color.White;
                _recordTimer.Start();
                _statusLabel.Text = "● Grabando video...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error iniciando grabación:" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _videoWriter?.Dispose();
                _videoWriter = null;
            }
        }

        private void StopVideoRecording()
        {
            try
            {
                _isRecording = false;
                _recordTimer.Stop();
                _videoWriter?.Dispose();
                _videoWriter = null;
                _recordButton.Text = "🎬 Grabar";
                _recordButton.BackColor = Theme.BgControl;
                _recordButton.ForeColor = Theme.TxtMain;
                _statusLabel.Text = "✓ Video guardado: " + Path.GetFileName(_videoPath ?? "");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error: " + ex.Message;
            }
        }

        private void UpdateRecordingClock()
        {
            if (!_isRecording) return;
            var elapsed = DateTime.Now - _recordStartTime;
            _statusLabel.Text = $"● Grabando  {elapsed:mm\\:ss}";
        }

        private void CapturePhoto()
        {
            if (_latestFrame == null) return;
            var savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                $"foto_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            new Bitmap(_latestFrame).Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            _statusLabel.Text = "✓ " + Path.GetFileName(savePath);
        }

        private void StopCamera()
        {
            if (_captureDevice?.IsRunning == true)
            { _captureDevice.SignalToStop(); _captureDevice.WaitForStop(); _captureDevice = null; }
            SetCameraRunning(false);
        }

        private void SetCameraRunning(bool running)
        {
            _startButton.Enabled   = !running;
            _stopButton.Enabled    = running;
            _captureButton.Enabled = running;
            _recordButton.Enabled  = running;
            _statusLabel.Text      = running ? "● Activa" : "Detenida";
            SetPlaceholderVisible(!running);
        }

        private void SetPlaceholderVisible(bool visible)
        {
            foreach (Control c in _previewBox.Controls)
                if (c.Name == "placeholder") c.Visible = visible;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_isRecording) StopVideoRecording();
            _videoWriter?.Dispose();
            StopCamera();
            _latestFrame?.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ── Grabadora de audio ────────────────────────────────────────
}
