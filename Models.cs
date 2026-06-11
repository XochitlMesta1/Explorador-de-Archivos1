using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XComponent.SliderBar;

namespace Explorador_de_Archivo.Forms
{
    public class ImageEditorForm : Form
    {
        private Bitmap? _orig, _current;
        private readonly Stack<Bitmap> _undo = new();

        private PictureBox  _canvas     = null!;
        private MACTrackBar _slBright   = null!;
        private MACTrackBar _slContrast = null!;
        private MACTrackBar _slSat      = null!;
        private Label       _info       = null!;

        public ImageEditorForm(string? path = null)
        {
            Theme.ApplyForm(this);
            Text = "Editor de Imagen";
            Size = new Size(980, 700);
            Build();
            if (!string.IsNullOrEmpty(path)) LoadImage(path);
        }

        private void Build()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgPanel, Padding = new Padding(8, 7, 0, 0) };
            Button Btn(string t, Action a) { var b = Theme.FlatBtn(t, 0, 30); b.AutoSize = true; b.Padding = new Padding(8, 0, 8, 0); b.Click += (_, _) => a(); return b; }
            bar.Controls.AddRange(new Control[] {
                Btn("📂 Abrir",    OpenFile),        Btn("💾 Guardar",   SaveFile),
                Btn("↩ Deshacer",  Undo),            Btn("↺ Rot -90",    () => Rotate(-90)),
                Btn("↻ Rot +90",   () => Rotate(90)), Btn("↔ H",         () => Flip(true)),
                Btn("↕ V",         () => Flip(false)), Btn("B&N",         Grayscale),
                Btn("Sepia",       Sepia),            Btn("Invertir",     Invert),
                Btn("Original",    Reset),
            });
            _info = new Label { ForeColor = Theme.TxtMuted, Font = Theme.FontMono, AutoSize = true };
            bar.Controls.Add(_info);

            _canvas = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };

            var adj = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, BackColor = Theme.BgPanel, ColumnCount = 3, RowCount = 2, Padding = new Padding(12, 8, 12, 8) };
            for (int i = 0; i < 3; i++) adj.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            _slBright   = Slider(adj, "Brillo",     0, -100, 100);
            _slContrast = Slider(adj, "Contraste",  1, -100, 100);
            _slSat      = Slider(adj, "Saturación", 2, -100, 100);
            _slBright.ValueChanged   += (_, _) => ApplyAdj();
            _slContrast.ValueChanged += (_, _) => ApplyAdj();
            _slSat.ValueChanged      += (_, _) => ApplyAdj();
            Controls.AddRange(new Control[] { bar, adj, _canvas });
        }

        private static MACTrackBar Slider(TableLayoutPanel p, string lbl, int col, int min, int max, int val = 0)
        {
            p.Controls.Add(new Label { Text = lbl, ForeColor = Theme.TxtSub, Font = Theme.FontSmall, AutoSize = true }, col, 0);
            var b = Theme.TrackBar(min, max, val); b.Dock = DockStyle.Fill; p.Controls.Add(b, col, 1); return b;
        }

        private void LoadImage(string path)
        {
            _undo.Clear(); _orig = new Bitmap(path); _current = new Bitmap(_orig); _canvas.Image = _current;
            _info.Text = $"  {_orig.Width} × {_orig.Height} px";
            _slBright.Value = _slContrast.Value = _slSat.Value = 0;
        }

        private void ApplyAdj()
        {
            if (_orig == null) return;
            var result = new Bitmap(_orig.Width, _orig.Height);
            float b = _slBright.Value/100f, c = _slContrast.Value/100f, s = _slSat.Value/100f;
            for (int y = 0; y < _orig.Height; y++)
                for (int x = 0; x < _orig.Width; x++)
                {
                    var p = _orig.GetPixel(x, y);
                    float r = p.R/255f, g = p.G/255f, bl = p.B/255f;
                    r+=b; g+=b; bl+=b;
                    r=(r-.5f)*(1+c)+.5f; g=(g-.5f)*(1+c)+.5f; bl=(bl-.5f)*(1+c)+.5f;
                    float gr=.299f*r+.587f*g+.114f*bl;
                    r=gr+(r-gr)*(1+s); g=gr+(g-gr)*(1+s); bl=gr+(bl-gr)*(1+s);
                    result.SetPixel(x, y, Color.FromArgb(p.A, Clamp(r), Clamp(g), Clamp(bl)));
                }
            _current = result; _canvas.Image = result;
        }

        private void Rotate(int deg)  { if (_current==null) return; PushUndo(); _current.RotateFlip(deg<0?RotateFlipType.Rotate270FlipNone:RotateFlipType.Rotate90FlipNone); _canvas.Image=_current; }
        private void Flip(bool h)     { if (_current==null) return; PushUndo(); _current.RotateFlip(h?RotateFlipType.RotateNoneFlipX:RotateFlipType.RotateNoneFlipY); _canvas.Image=_current; }
        private void Grayscale()      { if (_current==null) return; PushUndo(); PixelOp(p=>{int g=(int)(p.R*.299+p.G*.587+p.B*.114);return Color.FromArgb(p.A,g,g,g);}); }
        private void Sepia()          { if (_current==null) return; PushUndo(); PixelOp(p=>Color.FromArgb(p.A,Cap(p.R*.393+p.G*.769+p.B*.189),Cap(p.R*.349+p.G*.686+p.B*.168),Cap(p.R*.272+p.G*.534+p.B*.131))); }
        private void Invert()         { if (_current==null) return; PushUndo(); PixelOp(p=>Color.FromArgb(p.A,255-p.R,255-p.G,255-p.B)); }
        private void Reset()          { if (_orig==null) return; _current=new Bitmap(_orig); _canvas.Image=_current; _slBright.Value=_slContrast.Value=_slSat.Value=0; }
        private void Undo()           { if (_undo.TryPop(out var p)){_current=p;_canvas.Image=_current;} }
        private void PushUndo()       { if (_current!=null) _undo.Push(new Bitmap(_current)); }
        private void PixelOp(Func<Color,Color> fn) { for(int y=0;y<_current!.Height;y++) for(int x=0;x<_current.Width;x++) _current.SetPixel(x,y,fn(_current.GetPixel(x,y))); _canvas.Image=_current; }
        private void OpenFile()
        {
            var path = Forms.FilePicker.Open(filter: ".jpg,.jpeg,.png,.bmp,.gif,.webp,.tiff");
            if (path != null) LoadImage(path);
        }

        private void SaveFile()
        {
            if (_canvas.Image == null) return;
            var savePath = Forms.FilePicker.Save(defaultName: "imagen.png");
            if (savePath == null) return;
            var ext = System.IO.Path.GetExtension(savePath).ToLowerInvariant();
            var fmt = ext == ".jpg" || ext == ".jpeg"
                ? System.Drawing.Imaging.ImageFormat.Jpeg
                : System.Drawing.Imaging.ImageFormat.Png;
            _canvas.Image.Save(savePath, fmt);
        }
        private static int Clamp(float v) => Math.Max(0,Math.Min(255,(int)(v*255)));
        private static int Cap(double v)  => (int)Math.Min(255,Math.Max(0,v));
    }
}
