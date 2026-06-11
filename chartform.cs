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
    // ── Helpers de UI reutilizables ───────────────────────────────
    public static class UiHelpers
    {
        public static FlowLayoutPanel Toolbar(DockStyle dock, int height) =>
            new() { Dock = dock, Height = height, BackColor = Theme.BgPanel,
                    Padding = new Padding(8, 8, 0, 0) };

        public static Button ActionButton(string text, int width, Action onClick, bool accent = false)
        {
            var btn = accent
                ? Theme.AccentBtn(text, width, 30)
                : Theme.FlatBtn(text, width, 30);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        public static ComboBox FormatSelector(FlowLayoutPanel parent, string[] options)
        {
            var combo = Theme.ComboBox();
            combo.Width = 90;
            combo.Items.AddRange(options);
            combo.SelectedIndex = 0;
            parent.Controls.Add(combo);
            return combo;
        }

        public static SplitContainer HorizontalSplit(DockStyle dock) =>
            new() { Dock = dock, SplitterWidth = 3, BackColor = Theme.Border,
                    Orientation = Orientation.Horizontal };

        public static SplitContainer VerticalSplit(DockStyle dock) =>
            new() { Dock = dock, SplitterWidth = 3, BackColor = Theme.Border,
                    Orientation = Orientation.Vertical };

        public static RichTextBox EditorBox(SplitterPanel panel, bool readOnly = false)
        {
            var box = Theme.RichBox(readOnly);
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(box);
            return box;
        }

        public static Label ArrowLabel() =>
            new() { Text = " → ", ForeColor = Theme.TxtMuted,
                    Font = Theme.FontLarge, AutoSize = true };

        public static void ShowSuccess(Label label, string message)
        { label.Text = message; label.ForeColor = Theme.Green; }

        public static void ShowError(Label label, string message)
        { label.Text = "✗ " + message; label.ForeColor = Theme.Red; }
    }

    // Backward-compat aliases kept for existing callers
    public static class H
    {
        public static FlowLayoutPanel Row(DockStyle d, int h) => UiHelpers.Toolbar(d, h);
        public static Button Btn(string t, int w, Action a, bool accent = false) => UiHelpers.ActionButton(t, w, a, accent);
        public static ComboBox Combo(FlowLayoutPanel p, string[] i) => UiHelpers.FormatSelector(p, i);
        public static SplitContainer Split(DockStyle d, Orientation o = Orientation.Vertical) =>
            o == Orientation.Horizontal ? UiHelpers.HorizontalSplit(d) : UiHelpers.VerticalSplit(d);
        public static RichTextBox RBox(SplitterPanel p, bool r = false) => UiHelpers.EditorBox(p, r);
        public static Label Lbl(string t, Color c, Font f) => new() { Text = t, ForeColor = c, Font = f, AutoSize = true };
        public static void Ok(Label l, string m)  => UiHelpers.ShowSuccess(l, m);
        public static void Err(Label l, string m) => UiHelpers.ShowError(l, m);
    }

    public static class FormBase
    {
        public static FlowLayoutPanel Row(DockStyle d, int h)                                => UiHelpers.Toolbar(d, h);
        public static Button Btn(string t, int w, Action a, bool accent = false)              => UiHelpers.ActionButton(t, w, a, accent);
        public static SplitContainer Split(DockStyle d, Orientation o = Orientation.Vertical) =>
            o == Orientation.Horizontal ? UiHelpers.HorizontalSplit(d) : UiHelpers.VerticalSplit(d);
        public static RichTextBox RBox(SplitterPanel p, bool r = false)                       => UiHelpers.EditorBox(p, r);
        public static void Ok(Label l, string m)                                              => UiHelpers.ShowSuccess(l, m);
        public static void Err(Label l, string m)                                             => UiHelpers.ShowError(l, m);
    }

}
