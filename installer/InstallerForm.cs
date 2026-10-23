/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The installer window: one page, drawn by hand
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CotlCoopInstaller
{
    internal sealed class InstallerForm : Form
    {
        private readonly float S;
        private readonly string Version;
        private readonly TextBox PathBox;
        private readonly RuneButton BrowseButton, InstallButton, RemoveButton, LaunchButton, CloseButton;
        private readonly Timer Candles;
        private float Flicker, FlickerTarget;
        private readonly Random Rng = new();

        private string Status = "";
        private Color StatusColor = Theme.BoneDim;
        private float Progress = -1f;
        private string ProgressText = "";
        private bool Busy;
        private string FoundBuild;

        internal InstallerForm(string Dir, string Auto)
        {
            using (Graphics G = CreateGraphics())
                S = G.DpiX / 96f;
            Version = Assembly.GetExecutingAssembly().GetName().Version is Version V ? $"{V.Major}.{V.Minor}.{V.Build}" : "";

            Text = "Cult of the Lamb Co-op Anything";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Sz(680, 470);
            BackColor = Theme.Night;
            DoubleBuffered = true;
            Font = Theme.Text(14 * S);
            using (Stream Icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))
                if (Icon != null)
                    this.Icon = new Icon(Icon);

            PathBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Ash,
                ForeColor = Theme.Bone,
                Font = Theme.Text(15 * S),
                Location = Pt(48, 225),
                Width = (int)(456 * S)
            };
            PathBox.TextChanged += (_, _) => Check();
            Controls.Add(PathBox);

            BrowseButton = Add(new RuneButton(Words.Browse, false), 528, 214, 116, 36);
            BrowseButton.Click += (_, _) => Browse();
            RemoveButton = Add(new RuneButton(Words.Uninstall, false), 36, 392, 160, 48);
            RemoveButton.Click += (_, _) => Run(false);
            LaunchButton = Add(new RuneButton(Words.Launch, false), 284, 392, 170, 48);
            LaunchButton.Click += (_, _) => { Installer.LaunchGame(); Close(); };
            InstallButton = Add(new RuneButton(Words.Install, true), 474, 392, 170, 48);
            InstallButton.Click += (_, _) => Run(true);
            CloseButton = Add(new RuneButton("✕", false) { Flat = true }, 640, 8, 32, 32);
            CloseButton.Click += (_, _) => Close();

            Candles = new Timer { Interval = 60 };
            Candles.Tick += (_, _) =>
            {
                if (Rng.NextDouble() < 0.15)
                    FlickerTarget = (float)Rng.NextDouble();
                Flicker += (FlickerTarget - Flicker) * 0.25f;
                Invalidate(new Rectangle(0, 0, ClientSize.Width, (int)(178 * S)));
            };
            Candles.Start();

            MouseDown += (_, Args) =>
            {
                if (Args.Button == MouseButtons.Left && Args.Y < 170 * S)
                {
                    NativeDrag();
                }
            };

            if (string.IsNullOrEmpty(Dir))
            {
                Installer.FoundGame Found = Installer.FindGame();
                Dir = Found?.Dir;
                FoundBuild = Found?.Build;
            }
            PathBox.Text = Dir ?? "";
            Check();

            if (Auto == "install" || Auto == "uninstall")
                Shown += (_, _) => Run(Auto == "install");
        }

        private Size Sz(float W, float H) => new((int)(W * S), (int)(H * S));
        private Point Pt(float X, float Y) => new((int)(X * S), (int)(Y * S));

        private RuneButton Add(RuneButton Button, float X, float Y, float W, float H)
        {
            Button.Location = Pt(X, Y);
            Button.Size = Sz(W, H);
            Button.Font = Theme.Title((Button.Flat ? 18 : 17) * S);
            Controls.Add(Button);
            return Button;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr Window, int Message, IntPtr W, IntPtr L);

        private void NativeDrag()
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        private string Dir => PathBox.Text.Trim().Trim('"');

        /// <summary>
        /// What the chosen folder holds, and which buttons make sense for it
        /// </summary>
        private void Check()
        {
            bool Game = Installer.IsGameFolder(Dir);
            bool Installed = Game && Installer.IsInstalled(Dir);
            if (!Busy)
            {
                if (string.IsNullOrEmpty(Dir))
                    SetStatus(Words.NotFound, Theme.Gold);
                else if (!Game)
                    SetStatus(Words.NotGame, Theme.BloodLight);
                else if (Installed)
                    SetStatus(Words.AlreadyInstalled, Theme.Moss);
                else if (FoundBuild != null && FoundBuild != Installer.ExpectedBuild)
                    SetStatus(Words.OtherBuild(FoundBuild), Theme.Gold);
                else if (FoundBuild != null)
                    SetStatus(Words.FoundSteam(FoundBuild) + " " + Words.Ready, Theme.Moss);
                else
                    SetStatus(Words.Ready, Theme.Moss);
            }
            InstallButton.Label = Installed ? Words.Update : Words.Install;
            InstallButton.Enabled = Game && !Busy;
            RemoveButton.Enabled = Installed && !Busy;
            LaunchButton.Enabled = Installed && !Busy;
            BrowseButton.Enabled = !Busy;
            PathBox.Enabled = !Busy;
        }

        private void SetStatus(string Message, Color Tint)
        {
            Status = Message;
            StatusColor = Tint;
            Invalidate();
        }

        private void Browse()
        {
            using FolderBrowserDialog Picker = new() { Description = Words.PickFolder, ShowNewFolderButton = false };
            if (Directory.Exists(Dir))
                Picker.SelectedPath = Dir;
            if (Picker.ShowDialog(this) == DialogResult.OK)
            {
                FoundBuild = null;
                PathBox.Text = Picker.SelectedPath;
            }
        }

        private async void Run(bool Install)
        {
            string Target = Dir;
            if (!Installer.IsGameFolder(Target))
                return;
            if (Installer.GameRunning(Target))
            {
                SetStatus(Words.CloseGame, Theme.BloodLight);
                return;
            }
            if (!Installer.CanWrite(Target))
            {
                /* Ask for the rights once, the elevated copy does the work */
                if (Program.Elevate(Target, Install ? "install" : "uninstall"))
                    Close();
                else
                    SetStatus(Words.NeedAdmin, Theme.BloodLight);
                return;
            }

            Busy = true;
            Check();
            string Verb = Install ? Words.Installing : Words.Removing;
            try
            {
                int Count = await Task.Run(() =>
                {
                    void Report(int Done, int Total, string File) => BeginInvoke(new Action(() =>
                    {
                        Progress = Total == 0 ? 1f : (float)Done / Total;
                        ProgressText = File.Length == 0 ? "" : $"{Verb}: {File}";
                        Invalidate();
                    }));
                    return Install ? Installer.Install(Target, Report) : Installer.Uninstall(Target, Report);
                });
                Busy = false;
                Check();
                SetStatus(Install ? Words.Installed(Count) : Words.Removed, Theme.Moss);
            }
            catch (Exception Error)
            {
                Busy = false;
                Check();
                SetStatus(Words.Failed(Error.Message), Theme.BloodLight);
            }
            ProgressText = "";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs Args)
        {
            Graphics G = Args.Graphics;
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float W = ClientSize.Width, H = ClientSize.Height;

            /* Header: embers rising out of the dark */
            RectangleF Head = new(0, 0, W, 172 * S);
            using (LinearGradientBrush Back = new(Head, Theme.Ember, Theme.Night, 90f))
                G.FillRectangle(Back, Head);
            Theme.Emblem(G, new RectangleF(34 * S, 24 * S, 124 * S, 124 * S), Flicker);
            Theme.Candle(G, 590 * S, 156 * S, S, Flicker);
            Theme.Candle(G, 618 * S, 160 * S, S * 0.8f, 1f - Flicker);

            using (Font Kicker = Theme.Text(13 * S, FontStyle.Bold))
            using (SolidBrush Dim = new(Theme.Blood))
                DrawSpaced(G, Words.Kicker, Kicker, Dim, 182 * S, 46 * S, 4.2f * S);
            using (Font Big = Theme.Title(38 * S))
            using (SolidBrush Bone = new(Theme.Bone))
                G.DrawString(Words.Heading, Big, Bone, 178 * S, 64 * S);
            using (Font Small = Theme.Text(14 * S))
            using (SolidBrush Dim = new(Theme.BoneDim))
                G.DrawString(Words.Sub(Version), Small, Dim, 182 * S, 114 * S);

            /* An ornament between the header and the page */
            using (Pen Line = new(Theme.BloodDark, 1.5f * S))
            {
                float Y = 178 * S;
                G.DrawLine(Line, 36 * S, Y, W / 2 - 14 * S, Y);
                G.DrawLine(Line, W / 2 + 14 * S, Y, W - 36 * S, Y);
                PointF[] Diamond = { new(W / 2, Y - 6 * S), new(W / 2 + 8 * S, Y), new(W / 2, Y + 6 * S), new(W / 2 - 8 * S, Y) };
                using SolidBrush Red = new(Theme.Blood);
                G.FillPolygon(Red, Diamond);
            }

            using (Font Label = Theme.Text(13 * S, FontStyle.Bold))
            using (SolidBrush Dim = new(Theme.BoneDim))
                G.DrawString(Words.Folder.ToUpperInvariant(), Label, Dim, 36 * S, 194 * S);

            /* The path box's frame */
            RectangleF Box = new(36 * S, 214 * S, 480 * S, 36 * S);
            using (GraphicsPath Frame = Theme.Rounded(Box, 6 * S))
            {
                using SolidBrush Fill = new(Theme.Ash);
                G.FillPath(Fill, Frame);
                using Pen Edge = new(PathBox.Focused ? Theme.Blood : Theme.BloodDark, 1.5f * S);
                G.DrawPath(Edge, Frame);
            }

            using (Font Body = Theme.Text(14 * S))
            using (SolidBrush Tint = new(StatusColor))
                G.DrawString(Status, Body, Tint, new RectangleF(36 * S, 262 * S, W - 72 * S, 40 * S));

            /* Progress: a bar of blood filling up */
            RectangleF Bar = new(36 * S, 306 * S, W - 72 * S, 8 * S);
            using (GraphicsPath Track = Theme.Rounded(Bar, 4 * S))
            using (SolidBrush Fill = new(Theme.Ash))
                G.FillPath(Fill, Track);
            if (Progress > 0f)
            {
                RectangleF Done = new(Bar.X, Bar.Y, Math.Max(Bar.Height, Bar.Width * Math.Min(1f, Progress)), Bar.Height);
                using GraphicsPath Filled = Theme.Rounded(Done, 4 * S);
                using LinearGradientBrush Blood = new(Done, Theme.BloodDark, Theme.BloodLight, 0f);
                G.FillPath(Blood, Filled);
            }
            using (Font Small = Theme.Text(12 * S))
            using (SolidBrush Dim = new(Theme.BoneDim))
            {
                string Under = ProgressText.Length > 0 ? ProgressText : Words.HowTo;
                G.DrawString(Under, Small, Dim, new RectangleF(36 * S, 322 * S, W - 72 * S, 60 * S));
            }

            using (Font Tiny = Theme.Text(11 * S))
            using (SolidBrush Dim = new(Color.FromArgb(120, Theme.BoneDim)))
                G.DrawString(Words.Footer, Tiny, Dim, new RectangleF(36 * S, 448 * S, W - 72 * S, 20 * S));

            using (Pen Border = new(Theme.BloodDark, 2f * S))
                G.DrawRectangle(Border, S, S, W - 2 * S, H - 2 * S);
        }

        private static void DrawSpaced(Graphics G, string Line, Font Font, Brush Brush, float X, float Y, float Gap)
        {
            foreach (char Letter in Line)
            {
                string One = Letter.ToString();
                G.DrawString(One, Font, Brush, X, Y);
                X += G.MeasureString(One, Font, PointF.Empty, StringFormat.GenericTypographic).Width + Gap;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs Args)
        {
            Candles.Stop();
            base.OnFormClosed(Args);
        }

        /// <summary>
        /// The window as a picture, for checking the look without showing it
        /// </summary>
        internal void SaveShot(string File)
        {
            /* Shown far off screen for a moment, so the controls are made and drawn too */
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000);
            ShowInTaskbar = false;
            Show();
            Application.DoEvents();
            using Bitmap Picture = new(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(Picture, new Rectangle(System.Drawing.Point.Empty, ClientSize));
            Picture.Save(File, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    /// <summary>
    /// A button like the game's: blood red with bone letters, or dark with a red edge
    /// </summary>
    internal sealed class RuneButton : Control
    {
        private readonly bool Primary;
        private bool Hover, Down;
        internal bool Flat;

        internal string Label
        {
            get => Text;
            set { Text = value; Invalidate(); }
        }

        internal RuneButton(string Label, bool Primary)
        {
            this.Primary = Primary;
            Text = Label;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs Args) { Hover = true; Invalidate(); base.OnMouseEnter(Args); }
        protected override void OnMouseLeave(EventArgs Args) { Hover = Down = false; Invalidate(); base.OnMouseLeave(Args); }
        protected override void OnMouseDown(MouseEventArgs Args) { Down = true; Invalidate(); base.OnMouseDown(Args); }
        protected override void OnMouseUp(MouseEventArgs Args) { Down = false; Invalidate(); base.OnMouseUp(Args); }
        protected override void OnEnabledChanged(EventArgs Args) { Invalidate(); base.OnEnabledChanged(Args); }

        protected override void OnPaint(PaintEventArgs Args)
        {
            Graphics G = Args.Graphics;
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            RectangleF Box = new(1, 1, Width - 3, Height - 3);
            float Fade = Enabled ? 1f : 0.35f;

            if (!Flat)
            {
                using GraphicsPath Shape = Theme.Rounded(Box, Height * 0.18f);
                Color Top = Primary ? (Hover ? Theme.BloodLight : Theme.Blood) : (Hover ? Theme.Ember : Theme.Ash);
                Color Bottom = Primary ? Theme.BloodDark : Theme.Night;
                if (Down)
                    (Top, Bottom) = (Bottom, Top);
                using LinearGradientBrush Fill = new(Box, Fade < 1f ? Color.FromArgb(90, Top) : Top, Fade < 1f ? Color.FromArgb(90, Bottom) : Bottom, 90f);
                G.FillPath(Fill, Shape);
                using Pen Edge = new(Color.FromArgb((int)(255 * Fade), Primary ? Theme.Bone : Theme.BloodDark), Primary ? 1.6f : 1.4f);
                G.DrawPath(Edge, Shape);
            }

            Color Ink = Flat ? (Hover ? Theme.BloodLight : Theme.BoneDim) : Theme.Bone;
            using SolidBrush Letters = new(Color.FromArgb((int)(255 * Fade), Ink));
            using StringFormat Center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            G.DrawString(Text, Font, Letters, new RectangleF(0, 0, Width, Height), Center);
        }
    }
}
