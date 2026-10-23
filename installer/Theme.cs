/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Colors, fonts and drawn pieces in the game's spirit: black, blood red, bone
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace CotlCoopInstaller
{
    internal static class Theme
    {
        internal static readonly Color Night = Color.FromArgb(18, 10, 12);
        internal static readonly Color Ash = Color.FromArgb(34, 22, 25);
        internal static readonly Color Ember = Color.FromArgb(58, 18, 22);
        internal static readonly Color Blood = Color.FromArgb(196, 30, 44);
        internal static readonly Color BloodLight = Color.FromArgb(226, 58, 68);
        internal static readonly Color BloodDark = Color.FromArgb(110, 16, 24);
        internal static readonly Color Bone = Color.FromArgb(242, 230, 207);
        internal static readonly Color BoneDim = Color.FromArgb(171, 154, 134);
        internal static readonly Color Gold = Color.FromArgb(214, 170, 92);
        internal static readonly Color Moss = Color.FromArgb(132, 176, 108);

        private static readonly string Serif = FirstFont("Constantia", "Palatino Linotype", "Georgia", "Cambria", "Times New Roman");
        private static readonly string Sans = FirstFont("Segoe UI", "Tahoma", "Arial");

        private static string FirstFont(params string[] Names)
        {
            using InstalledFontCollection Installed = new();
            return Names.FirstOrDefault(Name => Installed.Families.Any(Family => Family.Name == Name)) ?? FontFamily.GenericSerif.Name;
        }

        internal static Font Title(float Size) => new(Serif, Size, FontStyle.Bold, GraphicsUnit.Pixel);
        internal static Font Text(float Size, FontStyle Style = FontStyle.Regular) => new(Sans, Size, Style, GraphicsUnit.Pixel);
        internal static Font Rune(float Size) => new(Serif, Size, FontStyle.Italic, GraphicsUnit.Pixel);

        /// <summary>
        /// The red crown over a ring of runes: drawn here, nothing of the game's art is used
        /// </summary>
        internal static void Emblem(Graphics G, RectangleF Box, float Glow)
        {
            G.SmoothingMode = SmoothingMode.AntiAlias;
            PointF Center = new(Box.X + Box.Width / 2f, Box.Y + Box.Height / 2f);
            float R = Math.Min(Box.Width, Box.Height) / 2f;

            /* Glow behind it, breathing with the candles */
            using (GraphicsPath Halo = new())
            {
                Halo.AddEllipse(Center.X - R * 1.25f, Center.Y - R * 1.25f, R * 2.5f, R * 2.5f);
                using PathGradientBrush Brush = new(Halo)
                {
                    CenterColor = Color.FromArgb((int)(90 + 60 * Glow), Blood),
                    SurroundColors = new[] { Color.FromArgb(0, Blood) }
                };
                G.FillPath(Brush, Halo);
            }

            /* Two rings with ticks between them */
            using (Pen Ring = new(Color.FromArgb(200, Bone), R * 0.05f))
            {
                G.DrawEllipse(Ring, Center.X - R, Center.Y - R, R * 2f, R * 2f);
                Ring.Width = R * 0.03f;
                G.DrawEllipse(Ring, Center.X - R * 0.84f, Center.Y - R * 0.84f, R * 1.68f, R * 1.68f);
                for (int Tick = 0; Tick < 24; Tick++)
                {
                    double A = Tick * Math.PI / 12;
                    float In = Tick % 3 == 0 ? 0.84f : 0.9f;
                    G.DrawLine(Ring, Center.X + (float)Math.Cos(A) * R * In, Center.Y + (float)Math.Sin(A) * R * In,
                        Center.X + (float)Math.Cos(A) * R * 0.97f, Center.Y + (float)Math.Sin(A) * R * 0.97f);
                }
            }

            /* The crown: five points, a band, an eye on the band */
            float W = R * 1.1f, H = R * 0.78f;
            float L = Center.X - W / 2f, T = Center.Y - H * 0.62f, B = T + H;
            PointF[] Crown =
            {
                new(L, B), new(L - W * 0.04f, T + H * 0.18f), new(L + W * 0.2f, T + H * 0.5f), new(L + W * 0.3f, T - H * 0.02f),
                new(L + W * 0.5f, T + H * 0.42f), new(L + W * 0.7f, T - H * 0.02f), new(L + W * 0.8f, T + H * 0.5f),
                new(L + W * 1.04f, T + H * 0.18f), new(L + W, B)
            };
            using (GraphicsPath Shape = new())
            {
                Shape.AddPolygon(Crown);
                using LinearGradientBrush Fill = new(new RectangleF(L, T, W, H), BloodLight, BloodDark, 90f);
                G.FillPath(Fill, Shape);
                using Pen Edge = new(Bone, R * 0.045f) { LineJoin = LineJoin.Round };
                G.DrawPath(Edge, Shape);
            }
            using (SolidBrush Tip = new(Bone))
            {
                foreach (int Point in new[] { 1, 3, 5, 7 })
                    G.FillEllipse(Tip, Crown[Point].X - R * 0.07f, Crown[Point].Y - R * 0.07f, R * 0.14f, R * 0.14f);
            }
            float EyeY = B - H * 0.2f;
            using (SolidBrush White = new(Bone))
                G.FillEllipse(White, Center.X - R * 0.2f, EyeY - R * 0.1f, R * 0.4f, R * 0.2f);
            using (SolidBrush Pupil = new(Night))
                G.FillEllipse(Pupil, Center.X - R * 0.025f, EyeY - R * 0.09f, R * 0.05f, R * 0.18f);
        }

        /// <summary>
        /// A small candle with a flame that flickers by <paramref name="Flicker"/>
        /// </summary>
        internal static void Candle(Graphics G, float X, float Bottom, float Scale, float Flicker)
        {
            G.SmoothingMode = SmoothingMode.AntiAlias;
            float W = 9f * Scale, H = 26f * Scale;
            using (SolidBrush Wax = new(Color.FromArgb(225, 214, 190)))
                G.FillRectangle(Wax, X - W / 2f, Bottom - H, W, H);
            using (SolidBrush Drip = new(Color.FromArgb(200, 186, 160)))
                G.FillEllipse(Drip, X - W / 2f, Bottom - H - 2f * Scale, W, 4f * Scale);

            float FlameH = (13f + 3f * Flicker) * Scale, FlameW = 6f * Scale;
            float Top = Bottom - H - FlameH - 1f * Scale;
            using (GraphicsPath Light = new())
            {
                Light.AddEllipse(X - FlameH * 1.4f, Top - FlameH * 0.6f, FlameH * 2.8f, FlameH * 2.8f);
                using PathGradientBrush Brush = new(Light)
                {
                    CenterColor = Color.FromArgb((int)(70 + 40 * Flicker), Gold),
                    SurroundColors = new[] { Color.FromArgb(0, Gold) }
                };
                G.FillPath(Brush, Light);
            }
            using GraphicsPath Flame = new();
            Flame.AddBezier(X, Top, X + FlameW, Top + FlameH * 0.55f, X + FlameW * 0.6f, Top + FlameH, X, Top + FlameH);
            Flame.AddBezier(X, Top + FlameH, X - FlameW * 0.6f, Top + FlameH, X - FlameW, Top + FlameH * 0.55f, X, Top);
            using LinearGradientBrush Fire = new(new RectangleF(X - FlameW, Top, FlameW * 2f, FlameH), Color.FromArgb(255, 244, 200), Color.FromArgb(232, 92, 40), 90f);
            G.FillPath(Fire, Flame);
        }

        internal static GraphicsPath Rounded(RectangleF Box, float Radius)
        {
            GraphicsPath Path = new();
            float D = Radius * 2f;
            Path.AddArc(Box.X, Box.Y, D, D, 180, 90);
            Path.AddArc(Box.Right - D, Box.Y, D, D, 270, 90);
            Path.AddArc(Box.Right - D, Box.Bottom - D, D, D, 0, 90);
            Path.AddArc(Box.X, Box.Bottom - D, D, D, 90, 90);
            Path.CloseFigure();
            return Path;
        }
    }
}
