using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ConverterAqui {
    sealed class Release {
        public Version Version; public string Tag, Url;
    }

    // Consulta só o release mais recente no GitHub. Nenhum arquivo nem dado do usuário é enviado.
    static class UpdateCheck {
        public const string ReleasesPage = "https://github.com/ArthurHermes/converter-aqui/releases/latest";
        const string Api = "https://api.github.com/repos/ArthurHermes/converter-aqui/releases/latest";
        const string SettingsKey = @"Software\ConverterAqui";
        public static Version Current {
            get { var v = Assembly.GetExecutingAssembly().GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }
        }
        // HKCU\Software\ConverterAqui\VerificarAtualizacoes = 0 desativa a consulta (ex.: redes sem internet).
        public static bool Enabled {
            get {
                try { using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey)) return key == null || !"0".Equals(Convert.ToString(key.GetValue("VerificarAtualizacoes"))); }
                catch { return true; }
            }
        }
        public static async Task<Release> NewerAsync() {
            if (!Enabled) return null;
            try {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) }) {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ConverterAqui/" + Current);
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    var data = JsonCodec.Deserialize<Dictionary<string, object>>(await http.GetStringAsync(Api).ConfigureAwait(false));
                    string tag = Convert.ToString(data["tag_name"]);
                    Version latest;
                    if (!Version.TryParse(tag.TrimStart('v', 'V'), out latest)) return null;
                    latest = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build));
                    if (latest <= Current) return null;
                    string url = data.ContainsKey("html_url") ? Convert.ToString(data["html_url"]) : null;
                    // Só abre links do próprio repositório.
                    if (url == null || !url.StartsWith("https://github.com/ArthurHermes/converter-aqui/", StringComparison.OrdinalIgnoreCase)) url = ReleasesPage;
                    return new Release { Version = latest, Tag = tag, Url = url };
                }
            } catch { return null; } // Sem internet ou GitHub indisponível: o app segue normalmente.
        }
    }

    sealed class UpdateBanner : Panel {
        readonly Release release;
        public event EventHandler Dismissed;
        public UpdateBanner(Release release) {
            this.release = release; Size = new Size(360, 64); BackColor = Palette.Tint;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            var download = new AppButton { Text = "Atualizar", Primary = true, Size = new Size(92, 32), Location = new Point(220, 16), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            download.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true }); } catch { } };
            var close = new AppButton { Text = "✕", Subtle = true, Size = new Size(30, 30), Location = new Point(320, 17), ForeColor = Palette.Muted, Font = new Font("Segoe UI", 9) };
            close.Click += (s, e) => { if (Dismissed != null) Dismissed(this, e); };
            new ToolTip().SetToolTip(close, "Lembrar depois");
            Controls.Add(download); Controls.Add(close);
        }
        protected override void OnPaintBackground(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Palette.Behind(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 12)) {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(197, 214, 252))) g.DrawPath(pen, p);
            }
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            Palette.Glyph(g, "", 13, new Rectangle(12, 0, 30, Height), Palette.Blue);
            using (var f = new Font("Segoe UI", 9.5f, FontStyle.Bold)) TextRenderer.DrawText(g, "Versão " + release.Version + " disponível", f, new Rectangle(48, 13, 170, 20), Palette.Ink, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (var f = new Font("Segoe UI", 8.5f)) TextRenderer.DrawText(g, "Você está usando a " + UpdateCheck.Current, f, new Rectangle(48, 33, 170, 18), Palette.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    sealed partial class MainForm {
        async Task CheckForUpdates(Panel heading) {
            var release = await UpdateCheck.NewerAsync();
            if (release == null || IsDisposed) return;
            var banner = new UpdateBanner(release) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            banner.Location = new Point(heading.ClientSize.Width - banner.Width, 4);
            banner.Dismissed += (s, e) => { heading.Controls.Remove(banner); banner.Dispose(); };
            heading.Controls.Add(banner); banner.BringToFront();
        }
    }
}
