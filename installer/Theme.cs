/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Colors, fonts and the torn panels of the README's look: ink, blood red, bone
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CotlCoopInstaller
{
    internal static class Theme
    {
        internal static readonly Color Night = Color.FromArgb(17, 10, 12);
        internal static readonly Color Ink = Color.FromArgb(10, 6, 7);
        internal static readonly Color Ash = Color.FromArgb(30, 18, 21);
        internal static readonly Color Blood = Color.FromArgb(196, 30, 42);
        internal static readonly Color BloodLight = Color.FromArgb(226, 58, 68);
        internal static readonly Color BloodDark = Color.FromArgb(110, 10, 20);
        internal static readonly Color Bone = Color.FromArgb(243, 233, 214);
        internal static readonly Color BoneDim = Color.FromArgb(171, 154, 134);
        internal static readonly Color Gold = Color.FromArgb(214, 170, 92);
        internal static readonly Color Moss = Color.FromArgb(132, 176, 108);

        /* Georgia carries Latin and Cyrillic and sits close to the game's serif */
        private static readonly string Serif = FirstFont("Georgia", "Constantia", "Palatino Linotype", "Cambria", "Times New Roman");
        private static readonly string Sans = FirstFont("Segoe UI", "Tahoma", "Arial");

        private static string FirstFont(params string[] Names)
        {
            using InstalledFontCollection Installed = new();
            return Names.FirstOrDefault(Name => Installed.Families.Any(Family => Family.Name == Name)) ?? FontFamily.GenericSerif.Name;
        }

        internal static Font Title(float Size) => new(Serif, Size, FontStyle.Bold, GraphicsUnit.Pixel);
        internal static Font Text(float Size, FontStyle Style = FontStyle.Regular) => new(Sans, Size, Style, GraphicsUnit.Pixel);

        private static readonly Dictionary<string, Image> Pictures = new();

        /// <summary>
        /// One of the pictures in installer/art (drawn by tools/make-readme-art.py, like the README's)
        /// </summary>
        internal static Image Art(string Name)
        {
            if (Pictures.TryGetValue(Name, out Image Found))
                return Found;
            using Stream Data = Assembly.GetExecutingAssembly().GetManifestResourceStream("art." + Name);
            Image Picture = Data == null ? null : new Bitmap(Image.FromStream(Data));
            Pictures[Name] = Picture;
            return Picture;
        }

        internal static void DrawArt(Graphics G, string Name, RectangleF Box, bool Flip = false)
        {
            Image Picture = Art(Name);
            if (Picture == null)
                return;
            G.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Flip)
                G.DrawImage(Picture, new[] { new PointF(Box.Right, Box.Top), new PointF(Box.Left, Box.Top), new PointF(Box.Right, Box.Bottom) });
            else
                G.DrawImage(Picture, Box);
        }

        /// <summary>
        /// The size of picture <paramref name="Name"/> fitted to <paramref name="Height"/>, keeping its shape
        /// </summary>
        internal static SizeF Fit(string Name, float Height)
        {
            Image Picture = Art(Name);
            return Picture == null ? SizeF.Empty : new SizeF(Picture.Width * Height / Picture.Height, Height);
        }

        /// <summary>
        /// A rectangle with torn paper edges, as the game's panels: the same tear for the same seed
        /// </summary>
        internal static GraphicsPath Torn(RectangleF Box, float Amp, int Seed, float Step)
        {
            Random Rng = new(Seed);
            List<PointF> Points = new();
            void Edge(PointF From, PointF To, float Nx, float Ny)
            {
                float Length = Math.Max(Math.Abs(To.X - From.X), Math.Abs(To.Y - From.Y));
                for (float At = 0; At < Length; At += Step)
                {
                    float T = At / Length;
                    float Off = (float)(Rng.NextDouble() * 2 - 1) * Amp * (Rng.NextDouble() < 0.7 ? 0.4f : 1f);
                    Points.Add(new PointF(From.X + (To.X - From.X) * T + Nx * Off, From.Y + (To.Y - From.Y) * T + Ny * Off));
                }
            }
            Edge(new PointF(Box.Left, Box.Top), new PointF(Box.Right, Box.Top), 0, 1);
            Edge(new PointF(Box.Right, Box.Top), new PointF(Box.Right, Box.Bottom), 1, 0);
            Edge(new PointF(Box.Right, Box.Bottom), new PointF(Box.Left, Box.Bottom), 0, 1);
            Edge(new PointF(Box.Left, Box.Bottom), new PointF(Box.Left, Box.Top), 1, 0);
            GraphicsPath Path = new();
            Path.AddPolygon(Points.ToArray());
            return Path;
        }

        /// <summary>
        /// The README's red panel: torn ink outside, red shading inside, a bone hairline
        /// </summary>
        internal static void RedPanel(Graphics G, RectangleF Box, float S, int Seed, bool Lit = false, bool Pressed = false, float Fade = 1f)
        {
            float Rim = Math.Min(Box.Height * 0.12f, 9f * S);
            using (GraphicsPath Outer = Torn(Box, 3.2f * S, Seed, 5f * S))
            using (SolidBrush Back = new(Color.FromArgb((int)(255 * Fade), Ink)))
                G.FillPath(Back, Outer);

            RectangleF Inner = RectangleF.Inflate(Box, -Rim, -Rim);
            using (GraphicsPath Red = Torn(Inner, 1.6f * S, Seed + 1, 5f * S))
            {
                Color Top = Lit ? BloodLight : Blood, Bottom = BloodDark;
                if (Pressed)
                    (Top, Bottom) = (Bottom, Top);
                using LinearGradientBrush Fill = new(RectangleF.Inflate(Inner, 0, 1), Color.FromArgb((int)(255 * Fade), Top), Color.FromArgb((int)(255 * Fade), Bottom), 90f);
                G.FillPath(Fill, Red);
            }

            RectangleF Line = RectangleF.Inflate(Inner, -Rim * 0.55f, -Rim * 0.55f);
            using (GraphicsPath Hair = Torn(Line, 0.9f * S, Seed + 2, 6f * S))
            using (Pen Pen = new(Color.FromArgb((int)(170 * Fade), Bone), Math.Max(1f, 1.1f * S)))
                G.DrawPath(Pen, Hair);
        }

        /// <summary>
        /// The README's black panel (its section titles): torn ink with an optional thin edge
        /// </summary>
        internal static void InkPanel(Graphics G, RectangleF Box, float S, int Seed, Color Edge, bool Lit = false, float Fade = 1f)
        {
            using GraphicsPath Shape = Torn(Box, 2.6f * S, Seed, 5f * S);
            using (SolidBrush Back = new(Color.FromArgb((int)(255 * Fade), Lit ? Ash : Ink)))
                G.FillPath(Back, Shape);
            if (Edge.A == 0)
                return;
            RectangleF Line = RectangleF.Inflate(Box, -4f * S, -4f * S);
            using GraphicsPath Hair = Torn(Line, 0.9f * S, Seed + 3, 6f * S);
            using Pen Pen = new(Color.FromArgb((int)(Edge.A * Fade), Edge), Math.Max(1f, 1.1f * S));
            G.DrawPath(Pen, Hair);
        }
    }
}
