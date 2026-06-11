using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorador_de_Archivo
{
    internal class AppColors
    {
        public static readonly Color BgPrimary = Color.FromArgb(28, 28, 28);
        public static readonly Color BgSecondary = Color.FromArgb(37, 37, 37);
        public static readonly Color BgTertiary = Color.FromArgb(45, 45, 45);
        public static readonly Color BgHover = Color.FromArgb(58, 58, 58);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentHover = Color.FromArgb(0, 106, 190);
        public static readonly Color Border = Color.FromArgb(61, 61, 61);
        public static readonly Color TextPrimary = Color.FromArgb(230, 230, 230);
        public static readonly Color TextSecondary = Color.FromArgb(176, 176, 176);
        public static readonly Color TextMuted = Color.FromArgb(112, 112, 112);
        public static readonly Color Green = Color.FromArgb(76, 175, 80);
        public static readonly Color Red = Color.FromArgb(244, 67, 54);
        public static readonly Color Amber = Color.FromArgb(255, 193, 7);
    }

    public static class AppFonts
    {
        public static readonly Font Regular = new("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font Bold = new("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Small = new("Segoe UI", 8f, FontStyle.Regular);
        public static readonly Font Large = new("Segoe UI", 11f, FontStyle.Regular);
        public static readonly Font Heading = new("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font Mono = new("Cascadia Code", 9f, FontStyle.Regular);
        public static readonly Font MonoLg = new("Cascadia Code", 11f, FontStyle.Regular);
    }

}
