using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Explorador_de_Archivo.Forms
{
    /// <summary>
    /// Escritor de video AVI con compresion MJPEG (Motion JPEG) en .NET puro.
    /// Cada frame se guarda como JPEG dentro del contenedor AVI.
    /// Compatible con VLC, Windows Media Player, navegadores, etc.
    /// No requiere FFmpeg, codecs ni librerias externas.
    /// </summary>
    public class AviVideoWriter : IDisposable
    {
        private readonly BinaryWriter _w;
        private readonly int _width, _height, _framerate;
        private readonly long _jpegQuality;
        private readonly List<(int offset, int size)> _frameIndex = new();
        private int _frameCount;
        private int _maxFrameSize;

        // Offsets para parchear al cerrar
        private long _riffSizePos, _hdrlSizePos, _aviHeaderPos, _strlSizePos, _strhPos;
        private long _moviStartPos, _moviSizePos;

        private static readonly EncoderParameters JpegParams;
        private static readonly ImageCodecInfo? JpegCodec;

        static AviVideoWriter()
        {
            JpegCodec = Array.Find(ImageCodecInfo.GetImageEncoders(),
                e => e.MimeType == "image/jpeg");
            JpegParams = new EncoderParameters(1);
            JpegParams.Param[0] = new EncoderParameter(Encoder.Quality, 80L);
        }

        public AviVideoWriter(string path, int width, int height,
            int framerate = 15, long jpegQuality = 80)
        {
            _width       = width;
            _height      = height;
            _framerate   = framerate;
            _jpegQuality = jpegQuality;

            _w = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));
            WriteHeaders();
        }

        public void WriteFrame(Bitmap frame)
        {
            Bitmap toEncode = frame;
            Bitmap? resized = null;
            if (frame.Width != _width || frame.Height != _height)
            {
                resized = new Bitmap(frame, _width, _height);
                toEncode = resized;
            }

            // Convertir el frame a JPEG en memoria
            using var ms = new MemoryStream();
            using (var p = new EncoderParameters(1))
            {
                p.Param[0] = new EncoderParameter(Encoder.Quality, _jpegQuality);
                if (JpegCodec != null) toEncode.Save(ms, JpegCodec, p);
                else                   toEncode.Save(ms, ImageFormat.Jpeg);
            }
            var jpegBytes = ms.ToArray();

            // Padding a multiplos de 2 (requerido por AVI)
            int size = jpegBytes.Length;
            int padded = (size + 1) & ~1;

            int offset = (int)(_w.BaseStream.Position - _moviStartPos - 4);

            // "00dc" chunk = compressed video frame
            _w.Write(BitConverter.GetBytes(0x63643030));
            _w.Write(size);
            _w.Write(jpegBytes);
            if (padded != size) _w.Write((byte)0);

            _frameIndex.Add((offset, size));
            if (size > _maxFrameSize) _maxFrameSize = size;
            _frameCount++;

            resized?.Dispose();
        }

        private void WriteHeaders()
        {
            // RIFF header
            _w.Write(BitConverter.GetBytes(0x46464952));  // "RIFF"
            _riffSizePos = _w.BaseStream.Position;
            _w.Write(0);
            _w.Write(BitConverter.GetBytes(0x20495641));  // "AVI "

            // LIST hdrl
            _w.Write(BitConverter.GetBytes(0x5453494C));  // "LIST"
            _hdrlSizePos = _w.BaseStream.Position;
            _w.Write(0);
            _w.Write(BitConverter.GetBytes(0x6C726468));  // "hdrl"

            // avih
            _w.Write(BitConverter.GetBytes(0x68697661));  // "avih"
            _w.Write(56);
            _aviHeaderPos = _w.BaseStream.Position;
            _w.Write(1000000 / _framerate);
            _w.Write(0);                                  // max bytes/s (se parche)
            _w.Write(0);                                  // padding
            _w.Write(0x10);                               // AVIF_HASINDEX
            _w.Write(0);                                  // total frames (se parche)
            _w.Write(0);
            _w.Write(1);                                  // 1 stream
            _w.Write(0);                                  // suggested buffer (se parche)
            _w.Write(_width);
            _w.Write(_height);
            for (int i = 0; i < 4; i++) _w.Write(0);

            // LIST strl
            _w.Write(BitConverter.GetBytes(0x5453494C));  // "LIST"
            _strlSizePos = _w.BaseStream.Position;
            _w.Write(0);
            _w.Write(BitConverter.GetBytes(0x6C727473));  // "strl"

            // strh
            _w.Write(BitConverter.GetBytes(0x68727473));  // "strh"
            _w.Write(56);
            _strhPos = _w.BaseStream.Position;
            _w.Write(BitConverter.GetBytes(0x73646976));  // "vids"
            _w.Write(BitConverter.GetBytes(0x47504A4D));  // "MJPG" codec MJPEG
            _w.Write(0);
            _w.Write((short)0);
            _w.Write((short)0);
            _w.Write(0);
            _w.Write(1);                                  // scale
            _w.Write(_framerate);                         // rate
            _w.Write(0);
            _w.Write(0);                                  // length (se parche)
            _w.Write(0);                                  // suggested buffer (se parche)
            _w.Write(-1);                                 // quality
            _w.Write(0);                                  // sample size (0 = variable)
            _w.Write((short)0); _w.Write((short)0);
            _w.Write((short)_width); _w.Write((short)_height);

            // strf (BITMAPINFOHEADER con codec MJPG)
            _w.Write(BitConverter.GetBytes(0x66727473));  // "strf"
            _w.Write(40);
            _w.Write(40);                                 // BITMAPINFOHEADER size
            _w.Write(_width);
            _w.Write(_height);
            _w.Write((short)1);                           // planes
            _w.Write((short)24);                          // bits per pixel
            _w.Write(BitConverter.GetBytes(0x47504A4D));  // "MJPG" compression
            _w.Write(_width * _height * 3);               // size image (max)
            _w.Write(0);
            _w.Write(0);
            _w.Write(0);
            _w.Write(0);

            // LIST movi
            _w.Write(BitConverter.GetBytes(0x5453494C));  // "LIST"
            _moviSizePos = _w.BaseStream.Position;
            _w.Write(0);
            _w.Write(BitConverter.GetBytes(0x69766F6D));  // "movi"
            _moviStartPos = _w.BaseStream.Position;
        }

        private void WriteIndex()
        {
            _w.Write(BitConverter.GetBytes(0x31786469));  // "idx1"
            _w.Write(_frameIndex.Count * 16);
            foreach (var (offset, size) in _frameIndex)
            {
                _w.Write(BitConverter.GetBytes(0x63643030));  // "00dc"
                _w.Write(0x10);                               // AVIIF_KEYFRAME
                _w.Write(offset);
                _w.Write(size);
            }
        }

        private void FinalizeAvi()
        {
            var moviEndPos = _w.BaseStream.Position;
            WriteIndex();
            var fileEnd = _w.BaseStream.Position;

            // RIFF size
            _w.BaseStream.Position = _riffSizePos;
            _w.Write((int)(fileEnd - _riffSizePos - 4));

            // hdrl LIST size
            _w.BaseStream.Position = _hdrlSizePos;
            _w.Write((int)(_moviSizePos - _hdrlSizePos - 4));

            // avih: max bytes/s
            _w.BaseStream.Position = _aviHeaderPos + 4;
            _w.Write(_maxFrameSize * _framerate);
            // avih: total frames
            _w.BaseStream.Position = _aviHeaderPos + 16;
            _w.Write(_frameCount);
            // avih: suggested buffer size
            _w.BaseStream.Position = _aviHeaderPos + 28;
            _w.Write(_maxFrameSize);

            // strl LIST size
            _w.BaseStream.Position = _strlSizePos;
            _w.Write((int)(_moviSizePos - _strlSizePos - 4));

            // strh: length
            _w.BaseStream.Position = _strhPos + 32;
            _w.Write(_frameCount);
            // strh: suggested buffer
            _w.BaseStream.Position = _strhPos + 36;
            _w.Write(_maxFrameSize);

            // movi LIST size
            _w.BaseStream.Position = _moviSizePos;
            _w.Write((int)(moviEndPos - _moviSizePos - 4));
        }

        public void Dispose()
        {
            try { FinalizeAvi(); } catch { }
            _w.Dispose();
        }
    }
}
