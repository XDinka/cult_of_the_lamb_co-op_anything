/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The installer window: one page, drawn by hand
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.Diagnostics;
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

        private string Status = "";
        private Color StatusColor = Theme.BoneDim;
        private float Progress = -1f;
        private string ProgressText = "";
        private bool Busy;
        private string FoundBuild;

        /* Start the game again once installed (the mod updating itself), with the arguments it had */
        private readonly bool Relaunch;
        private readonly string RelaunchArgs;

        /* A newer installer on GitHub, once known */
        private Updates.Release Newer;

        /* For a picture of the offer: the version this installer takes itself for */
        internal static string PretendVersion;

        internal InstallerForm(string Dir, string Auto, bool Relaunch = false, string RelaunchArgs = null)
        {
            this.Relaunch = Relaunch;
            this.RelaunchArgs = RelaunchArgs;
            using (Graphics G = CreateGraphics())
                S = G.DpiX / 96f;
            Version = PretendVersion ?? (Assembly.GetExecutingAssembly().GetName().Version is Version V ? $"{V.Major}.{V.Minor}.{V.Build}" : "");

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
                BackColor = Theme.Ink,
                ForeColor = Theme.Bone,
                Font = Theme.Text(15 * S),
                Location = Pt(50, 225),
                Width = (int)(452 * S)
            };
            PathBox.TextChanged += (_, _) => Check();
            Controls.Add(PathBox);

            BrowseButton = Add(new RuneButton(Words.Browse, false, 31), 528, 212, 118, 42);
            BrowseButton.Click += (_, _) => Browse();
            RemoveButton = Add(new RuneButton(Words.Uninstall, false, 41), 32, 388, 172, 54);
            RemoveButton.Click += (_, _) => Run(false);
            LaunchButton = Add(new RuneButton(Words.Launch, false, 51), 276, 388, 180, 54);
            LaunchButton.Click += (_, _) => { Installer.LaunchGame(); Close(); };
            InstallButton = Add(new RuneButton(Words.Install, true, 61), 468, 388, 180, 54);
            InstallButton.Click += (_, _) =>
            {
                if (Newer != null)
                    GetNewer();
                else
                    Run(true);
            };
            CloseButton = Add(new RuneButton("✕", false, 0) { Flat = true }, 626, 22, 30, 30);
            CloseButton.Click += (_, _) => Close();

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

            /* Nothing selected or focused at first, the path reads as a plain line */
            Shown += (_, _) =>
            {
                PathBox.Select(PathBox.TextLength, 0);
                ActiveControl = null;
            };

            if (Auto == "install" || Auto == "uninstall")
                Shown += (_, _) => Run(Auto == "install");
            else
                Shown += (_, _) => AskForNewer();
        }

        /// <summary>
        /// A newer release than this installer: the install button gets it instead
        /// </summary>
        private async void AskForNewer()
        {
            OfferNewer(await Task.Run(Updates.Latest));
        }

        private void OfferNewer(Updates.Release Found)
        {
            if (Found == null || !Updates.Newer(Found.Version, Version) || Busy)
                return;
            Newer = Found;
            InstallButton.Label = Words.GetNewer(Found.Version);
            SetStatus(Words.NewerOut(Found.Version, Version), Theme.Gold);
        }

        /// <summary>
        /// The newer installer, checked, started for the same folder; this one closes
        /// </summary>
        private async void GetNewer()
        {
            Busy = true;
            Check();
            SetStatus(Words.Downloading(Newer.Version), Theme.BoneDim);
            try
            {
                string File = await Task.Run(() => Updates.Download(Newer));
                Process.Start(new ProcessStartInfo(File, $"--dir \"{Dir}\"") { UseShellExecute = true });
                Close();
            }
            catch (Exception Error)
            {
                Busy = false;
                Newer = null;
                InstallButton.Label = Words.Install;
                Check();
                SetStatus(Words.Failed(Error.Message) + " " + Updates.Page, Theme.BloodLight);
            }
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
                if (Program.Elevate(Target, Install ? "install" : "uninstall", Relaunch))
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

                /* Updated from the game: back into it */
                if (Install && Relaunch)
                {
                    SetStatus(Words.Relaunching, Theme.Moss);
                    await Task.Delay(1500);
                    Installer.LaunchGame(Target, RelaunchArgs);
                    Close();
                }
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

            /* The ritual circle faint behind the page */
            Theme.DrawArt(G, "ritual.png", new RectangleF(W / 2 - 230 * S, 120 * S, 460 * S, 460 * S));

            /* Header: the README's red banner, the Lamb and the Goat on either side */
            RectangleF Banner = new(14 * S, 12 * S, W - 28 * S, 142 * S);
            Theme.RedPanel(G, Banner, S, 7);
            float HeadH = 104 * S;
            SizeF Lamb = Theme.Fit("lamb.png", HeadH), Goat = Theme.Fit("goat.png", HeadH);
            Theme.DrawArt(G, "lamb.png", new RectangleF(Banner.X + 26 * S, Banner.Y + (Banner.Height - HeadH) / 2 + 4 * S, Lamb.Width, Lamb.Height));
            Theme.DrawArt(G, "goat.png", new RectangleF(Banner.Right - 26 * S - Goat.Width, Banner.Y + (Banner.Height - HeadH) / 2 + 4 * S, Goat.Width, Goat.Height), Flip: true);

            using (Font Kicker = Theme.Title(13 * S))
            using (SolidBrush Ink = new(Color.FromArgb(215, Theme.Bone)))
                DrawSpaced(G, Words.Kicker, Kicker, Ink, W / 2, 30 * S, 4f * S, Centered: true);
            string TitleArt = Words.Russian ? "title-ru.png" : "title-en.png";
            SizeF Title = Theme.Fit(TitleArt, 50 * S);
            float MaxW = Banner.Width - 2 * (Lamb.Width + 44 * S);
            if (Title.Width > MaxW)
                Title = new SizeF(MaxW, Title.Height * MaxW / Title.Width);
            Theme.DrawArt(G, TitleArt, new RectangleF(W / 2 - Title.Width / 2, 54 * S + (50 * S - Title.Height) / 2, Title.Width, Title.Height));
            using (Font Small = Theme.Title(13 * S))
            using (SolidBrush Bone = new(Color.FromArgb(225, Theme.Bone)))
            using (StringFormat Center = new() { Alignment = StringAlignment.Center })
                G.DrawString(Words.Sub(Version), Small, Bone, new RectangleF(Banner.X, 114 * S, Banner.Width, 22 * S), Center);

            /* The game's divider under the banner */
            SizeF Divider = Theme.Fit("divider.png", 22 * S);
            Theme.DrawArt(G, "divider.png", new RectangleF(W / 2 - Divider.Width / 2, 160 * S, Divider.Width, Divider.Height));

            /* A crown with its red eye, then the label */
            SizeF Crown = Theme.Fit("crown.png", 18 * S);
            Theme.DrawArt(G, "crown.png", new RectangleF(34 * S, 190 * S, Crown.Width, Crown.Height));
            using (Font Label = Theme.Title(13 * S))
            using (SolidBrush Dim = new(Theme.BoneDim))
                DrawSpaced(G, Words.Folder.ToUpperInvariant(), Label, Dim, 34 * S + Crown.Width + 8 * S, 191 * S, 2f * S);

            /* The path box: a torn black panel */
            Theme.InkPanel(G, new RectangleF(32 * S, 212 * S, 488 * S, 42 * S), S, 21, PathBox.Focused ? Theme.Blood : Theme.BloodDark);

            using (Font Body = Theme.Text(14 * S))
            using (SolidBrush Tint = new(StatusColor))
                G.DrawString(Status, Body, Tint, new RectangleF(34 * S, 264 * S, W - 68 * S, 40 * S));

            /* Progress: a torn strip of blood filling up */
            RectangleF Bar = new(34 * S, 306 * S, W - 68 * S, 10 * S);
            using (GraphicsPath Track = Theme.Torn(Bar, 1.2f * S, 71, 6f * S))
            using (SolidBrush Fill = new(Theme.Ink))
                G.FillPath(Fill, Track);
            if (Progress > 0f)
            {
                RectangleF Done = new(Bar.X, Bar.Y, Math.Max(Bar.Height, Bar.Width * Math.Min(1f, Progress)), Bar.Height);
                using GraphicsPath Filled = Theme.Torn(Done, 1.2f * S, 71, 6f * S);
                using LinearGradientBrush Blood = new(RectangleF.Inflate(Done, 1, 1), Theme.BloodDark, Theme.BloodLight, 0f);
                G.FillPath(Blood, Filled);
            }
            using (Font Small = Theme.Text(12 * S))
            using (SolidBrush Dim = new(Theme.BoneDim))
            {
                string Under = ProgressText.Length > 0 ? ProgressText : Words.HowTo;
                G.DrawString(Under, Small, Dim, new RectangleF(34 * S, 324 * S, W - 68 * S, 60 * S));
            }

            using (Font Tiny = Theme.Text(11 * S))
            using (SolidBrush Dim = new(Color.FromArgb(120, Theme.BoneDim)))
            using (StringFormat Center = new(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                G.DrawString(Words.Footer, Tiny, Dim, new RectangleF(10 * S, 450 * S, W - 20 * S, 18 * S), Center);

            using (Pen Border = new(Theme.BloodDark, 2f * S))
                G.DrawRectangle(Border, S, S, W - 2 * S, H - 2 * S);
        }

        private static void DrawSpaced(Graphics G, string Line, Font Font, Brush Brush, float X, float Y, float Gap, bool Centered = false)
        {
            if (Centered)
            {
                float Width = -Gap;
                foreach (char Letter in Line)
                    Width += Advance(G, Letter, Font) + Gap;
                X -= Width / 2;
            }
            foreach (char Letter in Line)
            {
                string One = Letter.ToString();
                G.DrawString(One, Font, Brush, X, Y);
                X += Advance(G, Letter, Font) + Gap;
            }
        }

        /* A typographic measure gives a space no width */
        private static float Advance(Graphics G, char Letter, Font Font) =>
            Letter == ' ' ? Font.Size * 0.4f : G.MeasureString(Letter.ToString(), Font, PointF.Empty, StringFormat.GenericTypographic).Width;

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
            if (PretendVersion != null)
            {
                OfferNewer(Updates.Latest());
                Application.DoEvents();
            }
            using Bitmap Picture = new(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(Picture, new Rectangle(System.Drawing.Point.Empty, ClientSize));
            Picture.Save(File, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    /// <summary>
    /// A button like the README's panels: torn red with bone letters, or torn black with a red edge
    /// </summary>
    internal sealed class RuneButton : Control
    {
        private readonly bool Primary;
        private readonly int Seed;
        private bool Hover, Down;
        internal bool Flat;

        internal string Label
        {
            get => Text;
            set { Text = value; Invalidate(); }
        }

        internal RuneButton(string Label, bool Primary, int Seed)
        {
            this.Primary = Primary;
            this.Seed = Seed;
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
            float S = DeviceDpi / 96f;
            RectangleF Box = new(3 * S, 3 * S, Width - 6 * S, Height - 6 * S);
            float Fade = Enabled ? 1f : 0.4f;

            if (!Flat)
            {
                if (Primary)
                    Theme.RedPanel(G, Box, S, Seed, Hover, Down, Fade);
                else
                    Theme.InkPanel(G, Box, S, Seed, Hover ? Theme.Blood : Theme.BloodDark, Hover && !Down, Fade);
            }

            Color Ink = Flat ? (Hover ? Theme.Ink : Theme.Bone) : Theme.Bone;
            using SolidBrush Letters = new(Color.FromArgb((int)(255 * Fade), Ink));
            using StringFormat Center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            G.DrawString(Text, Font, Letters, new RectangleF(0, 0, Width, Height), Center);
        }
    }
}
