using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;

namespace ConverterAqui {
    static class Palette {
        public static readonly Color Canvas = Color.FromArgb(244, 246, 250);
        public static readonly Color Ink = Color.FromArgb(17, 24, 39);
        public static readonly Color Muted = Color.FromArgb(100, 112, 133);
        public static readonly Color Faint = Color.FromArgb(160, 169, 184);
        public static readonly Color Line = Color.FromArgb(226, 231, 239);
        public static readonly Color Field = Color.FromArgb(248, 250, 252);
        public static readonly Color Hover = Color.FromArgb(236, 239, 245);
        public static readonly Color Blue = Color.FromArgb(37, 99, 235);
        public static readonly Color BlueDark = Color.FromArgb(29, 78, 216);
        public static readonly Color Tint = Color.FromArgb(236, 242, 255);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        // Segoe Fluent Icons existe no Windows 11; o Windows 10 usa Segoe MDL2 Assets com os mesmos códigos.
        public static readonly string IconFont = FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        public static GraphicsPath Round(RectangleF r, float radius) {
            radius = Math.Max(0.5f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2));
            float d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static Label Text(string text, float size, Color color, bool bold) {
            return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = Padding.Empty };
        }
        // Controles arredondados pintam os cantos com a cor do pai; sem isso sobram pixels pretos.
        public static Color Behind(Control control) { return control.Parent != null ? control.Parent.BackColor : Canvas; }
        public static void Chevron(Graphics g, PointF c, Color color) {
            using (var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(c.X-4, c.Y-2), new PointF(c.X, c.Y+2), new PointF(c.X+4, c.Y-2) });
        }
        public static void Glyph(Graphics g, string glyph, float size, Rectangle bounds, Color color) {
            using (var font = new Font(IconFont, size)) TextRenderer.DrawText(g, glyph, font, bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        // Cor do selo de tipo na fila: cada família de formato tem sua cor.
        public static void KindColors(string extension, out Color back, out Color ink) {
            extension = extension.ToLowerInvariant();
            if (extension == ".pdf") { back = Color.FromArgb(254, 236, 236); ink = Color.FromArgb(200, 40, 40); }
            else if (Catalog.Images.Contains(extension)) { back = Color.FromArgb(232, 240, 255); ink = Blue; }
            else if (Catalog.Audio.Contains(extension)) { back = Color.FromArgb(243, 236, 255); ink = Color.FromArgb(124, 58, 237); }
            else if (Catalog.Video.Contains(extension)) { back = Color.FromArgb(255, 243, 224); ink = Color.FromArgb(194, 100, 10); }
            else { back = Color.FromArgb(229, 246, 236); ink = Color.FromArgb(21, 128, 61); }
        }
    }
    sealed class AppButton : Button {
        public bool Primary, Subtle, Active, Nav;
        public string Icon;
        bool hover, pressed;
        public AppButton() {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
            BackColor = Color.White; ForeColor = Palette.Ink; Cursor = Cursors.Hand; Height = 36;
            Padding = new Padding(10, 2, 10, 2); Margin = new Padding(0, 0, 8, 0); Font = new Font("Segoe UI", 9.5f);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = pressed = false; base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; Color behind = Palette.Behind(this);
            g.Clear(behind); g.SmoothingMode = SmoothingMode.AntiAlias;
            bool live = Enabled && hover;
            Color fill, ink, border = Color.Empty;
            if (Primary) { fill = !Enabled ? Color.FromArgb(190, 207, 248) : pressed ? Color.FromArgb(30, 64, 175) : live ? Palette.BlueDark : Palette.Blue; ink = Color.White; }
            else if (Active) { fill = live ? Color.FromArgb(222, 232, 255) : Palette.Tint; ink = Enabled ? Palette.Blue : Palette.Faint; }
            else if (Subtle || Nav) { fill = pressed ? Palette.Line : live ? Palette.Hover : behind; ink = Enabled ? ForeColor : Palette.Faint; }
            else { fill = pressed ? Palette.Hover : live ? Palette.Field : Color.White; border = Palette.Line; ink = Enabled ? ForeColor : Palette.Faint; }
            using (var path = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 8)) {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border != Color.Empty) using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }
            if (Nav && Active) using (var bar = Palette.Round(new RectangleF(0, 11, 3, Height-22), 1.5f)) using (var b = new SolidBrush(Palette.Blue)) g.FillPath(b, bar);
            if (Icon != null) {
                Palette.Glyph(g, Icon, 11, new Rectangle(10, 0, 26, Height), ink);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(42, 0, Width-48, Height), ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            } else {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(6, 0, Width-12, Height), ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            if (Focused && ShowFocusCues) using (var ring = Palette.Round(new RectangleF(1.5f, 1.5f, Width-3.5f, Height-3.5f), 7)) using (var pen = new Pen(Color.FromArgb(150, Palette.Blue), 1.5f)) g.DrawPath(pen, ring);
        }
    }
    sealed class Card : Panel {
        public Card() { BackColor = Color.White; Padding = new Padding(20); SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Palette.Behind(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 14)) {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, p);
                using (var pen = new Pen(Palette.Line)) g.DrawPath(pen, p);
            }
        }
    }
    // Moldura arredondada para TextBox, que no WinForms não tem borda personalizável.
    sealed class FieldFrame : Panel {
        readonly Control inner;
        public FieldFrame(Control inner) {
            this.inner = inner; BackColor = Palette.Field; Padding = new Padding(10, 7, 8, 4); Height = 34;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            var box = inner as TextBox; if (box != null) box.BorderStyle = BorderStyle.None;
            inner.BackColor = Palette.Field; inner.Dock = DockStyle.Fill; inner.Margin = Padding.Empty; Controls.Add(inner);
            inner.GotFocus += (s, e) => Invalidate(); inner.LostFocus += (s, e) => Invalidate(); inner.EnabledChanged += (s, e) => Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Palette.Behind(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 7)) {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, p);
                using (var pen = new Pen(inner.Focused ? Palette.Blue : Palette.Line)) g.DrawPath(pen, p);
            }
        }
    }
    // ComboBox com a mesma moldura dos campos: desenha por cima da seta e da borda do sistema.
    sealed class FlatCombo : ComboBox {
        bool hover;
        public FlatCombo() { DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            if (e.Index < 0) return;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0, selected = (e.State & DrawItemState.Selected) != 0;
            Color back = edit ? BackColor : selected ? Palette.Tint : Color.White;
            Color ink = !Enabled ? Palette.Faint : edit ? ForeColor : selected ? Palette.Blue : Palette.Ink;
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            using (var font = edit ? null : new Font("Segoe UI", 9.5f))
                TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), font ?? Font, new Rectangle(e.Bounds.X+(edit ? 6 : 10), e.Bounds.Y, e.Bounds.Width-16, e.Bounds.Height), ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && IsHandleCreated) using (var g = Graphics.FromHwnd(Handle)) PaintChrome(g);
        }
        void PaintChrome(Graphics g) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int arrow = SystemInformation.VerticalScrollBarWidth + 6;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, Width-arrow-2, 1, arrow+1, Height-2);
            using (var path = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 7)) {
                using (var outside = new Region(new Rectangle(0, 0, Width, Height))) using (var b = new SolidBrush(Palette.Behind(this))) { outside.Exclude(path); g.FillRegion(b, outside); }
                using (var pen = new Pen(Focused || DroppedDown ? Palette.Blue : hover && Enabled ? Color.FromArgb(196, 207, 226) : Palette.Line)) g.DrawPath(pen, path);
            }
            Palette.Chevron(g, new PointF(Width-arrow/2f-4, Height/2f), Enabled ? Palette.Muted : Palette.Faint);
        }
    }
    sealed class ProgressLine : Control {
        readonly Timer timer = new Timer { Interval = 16 };
        float phase; double value = -1;
        public ProgressLine() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            timer.Tick += (s, e) => { phase = (phase + 0.012f) % 1.4f; Invalidate(); };
        }
        // Valor entre 0 e 1; negativo mostra a animação contínua.
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Value { get { return value; } set { this.value = value; timer.Enabled = Visible && value < 0; Invalidate(); } }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); timer.Enabled = Visible && value < 0; }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Palette.Behind(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new RectangleF(0, 0, Width-1, Height-1);
            using (var p = Palette.Round(track, Height/2f)) using (var b = new SolidBrush(Palette.Line)) g.FillPath(b, p);
            var bar = value >= 0 ? new RectangleF(0, 0, (float)(track.Width * Math.Min(1, value)), track.Height) : new RectangleF((phase-0.4f) * Width, 0, Width * 0.4f, track.Height);
            bar.Intersect(track);
            if (bar.Width >= 1) using (var p = Palette.Round(bar, Height/2f)) using (var b = new SolidBrush(Palette.Blue)) g.FillPath(b, p);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
    sealed class DropZone : Panel {
        public event EventHandler BrowseRequested;
        bool hover, highlight;
        public DropZone() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; BackColor = Color.White; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Highlight { get { return highlight; } set { if (highlight != value) { highlight = value; Invalidate(); } } }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); if (BrowseRequested != null) BrowseRequested(this, e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Palette.Behind(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            bool lit = Enabled && (hover || highlight);
            using (var shape = Palette.Round(new RectangleF(0.5f, 0.5f, Width-1.5f, Height-1.5f), 10)) {
                using (var b = new SolidBrush(highlight ? Palette.Tint : lit ? Color.FromArgb(246, 249, 255) : Palette.Field)) g.FillPath(b, shape);
                using (var pen = new Pen(lit ? Palette.Blue : Color.FromArgb(196, 208, 230), highlight ? 1.5f : 1f) { DashStyle = highlight ? DashStyle.Solid : DashStyle.Dash }) g.DrawPath(pen, shape);
            }
            int size = 44, x = 18, y = (Height-size)/2;
            using (var circle = new SolidBrush(lit ? Color.White : Palette.Tint)) g.FillEllipse(circle, x, y, size, size);
            Palette.Glyph(g, "", 15, new Rectangle(x, y, size, size), Palette.Blue);
            int text = x + size + 16;
            using (var f = new Font("Segoe UI", 10, FontStyle.Bold)) TextRenderer.DrawText(g, highlight ? "Solte para adicionar à fila" : "Arraste seus arquivos para cá", f, new Rectangle(text, Height/2-23, Width-text-12, 24), Palette.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var f = new Font("Segoe UI", 9)) TextRenderer.DrawText(g, "ou clique para escolher no computador  •  Ctrl+O", f, new Rectangle(text, Height/2+1, Width-text-12, 22), Palette.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    sealed partial class MainForm {
        Label queueCount, stateTitle;
        Panel imageOptions, rangeOptions, logCard, stateDot;
        Label emptyQueue;
        AppButton navConvert, navPdf, navOcr, showDetails;
        CheckBox advanced;
        DropZone drop;
        Color stateColor = Palette.Faint;
        bool modernReady;
        void InitializeModernInterface(string[] inputs, string preset) {
            SuspendLayout();
            Text = "Converter Aqui 2.1"; Font = new Font("Segoe UI",10); ForeColor = Palette.Ink; BackColor = Palette.Canvas;
            ClientSize = new Size(1200,810); MinimumSize = new Size(1080,740); StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true; KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(shell);
            var side = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16,26,16,18), Margin = Padding.Empty }; shell.Controls.Add(side,0,0);
            side.Paint += (s,e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Palette.Line)) g.DrawLine(pen, side.Width-1, 0, side.Width-1, side.Height);
                using (var mark = Palette.Round(new RectangleF(20,28,36,36), 10)) using (var b = new LinearGradientBrush(new PointF(20,28), new PointF(56,64), Color.FromArgb(59,130,246), Color.FromArgb(79,70,229))) g.FillPath(b, mark);
                Palette.Glyph(g, "", 14, new Rectangle(20,28,36,36), Color.White);
            };
            var brand = Palette.Text("converter aqui",14,Palette.Ink,true); brand.Location = new Point(64,25); side.Controls.Add(brand);
            var brandSub = Palette.Text("Seu conversor de arquivos",8.5f,Palette.Muted,false); brandSub.Location = new Point(66,48); side.Controls.Add(brandSub);
            var navTitle = Palette.Text("FERRAMENTAS",7.8f,Palette.Faint,true); navTitle.Location = new Point(22,100); side.Controls.Add(navTitle);
            var nav = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.None, Location = new Point(14,122), Size = new Size(192,160), WrapContents = false, Margin = Padding.Empty };
            navConvert = NavButton("Converter formato","",() => operation.SelectedIndex=0);
            navPdf = NavButton("Ferramentas de PDF","",() => operation.SelectedIndex=1);
            navOcr = NavButton("Reconhecer texto","",() => operation.SelectedIndex=6);
            nav.Controls.AddRange(new Control[] { navConvert,navPdf,navOcr }); side.Controls.Add(nav); locked.Add(nav);
            var sideFoot = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 190, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var title = Palette.Text("MENU DO WINDOWS",7.8f,Palette.Faint,true); title.Margin = new Padding(6,0,0,10); sideFoot.Controls.Add(title);
            var install = ModernButton("Ativar botão direito",() => MenuAction(true)); install.Width=186; install.Margin=new Padding(0,0,0,6); sideFoot.Controls.Add(install);
            var uninstall = ModernButton("Remover integração",() => MenuAction(false)); uninstall.Width=186; uninstall.Subtle=true; uninstall.ForeColor=Palette.Muted; sideFoot.Controls.Add(uninstall);
            var privacy = new Label { AutoSize=false, Size=new Size(186,52), Margin=new Padding(0,16,0,0), BackColor=Palette.Field };
            privacy.Paint += (s,e) => {
                var g = e.Graphics; g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = Palette.Round(new RectangleF(0.5f,0.5f,privacy.Width-1.5f,privacy.Height-1.5f),8)) using (var b = new SolidBrush(Color.FromArgb(240,253,244))) g.FillPath(b,p);
                Palette.Glyph(g, "", 11, new Rectangle(8,0,24,privacy.Height), Palette.Success);
                using (var f = new Font("Segoe UI",8.5f,FontStyle.Bold)) TextRenderer.DrawText(g,"Conversão local",f,new Rectangle(36,8,privacy.Width-40,18),Color.FromArgb(21,101,52),TextFormatFlags.Left);
                using (var f = new Font("Segoe UI",8)) TextRenderer.DrawText(g,"Nada sai do seu PC.",f,new Rectangle(36,26,privacy.Width-40,18),Palette.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
            };
            sideFoot.Controls.Add(privacy); side.Controls.Add(sideFoot); locked.Add(sideFoot);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28,24,28,16), ColumnCount=1,RowCount=5, Margin=Padding.Empty };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,102)); main.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,14)); main.RowStyles.Add(new RowStyle(SizeType.AutoSize)); main.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); shell.Controls.Add(main,1,0);
            var heading = new Panel { Dock=DockStyle.Fill, Margin=Padding.Empty };
            var eyebrow=Palette.Text("MENOS ETAPAS. MAIS POSSIBILIDADES.",8.2f,Palette.Blue,true); eyebrow.Location=new Point(0,1); heading.Controls.Add(eyebrow);
            var headline=Palette.Text("O formato certo, em poucos cliques.",22,Palette.Ink,true); headline.Location=new Point(-3,22); heading.Controls.Add(headline);
            var subtitle=Palette.Text("Imagens, documentos, áudio e vídeo. Tudo no seu computador.",10,Palette.Muted,false); subtitle.Location=new Point(0,66); heading.Controls.Add(subtitle); main.Controls.Add(heading,0,0);

            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330)); main.Controls.Add(body,0,1);
            var queue=new Card { Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(20,18,20,16) }; body.Controls.Add(queue,0,0);
            var queueLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty };
            queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,96)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,46)); queue.Controls.Add(queueLayout);
            var queueHeader=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
            queueHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); queueHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); queueHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var queueTitleBox=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty };
            var queueTitle=Palette.Text("Seus arquivos",13,Palette.Ink,true); queueTitle.Location=new Point(-1,0); queueTitleBox.Controls.Add(queueTitle);
            queueCount=Palette.Text("Nenhum arquivo na fila",8.8f,Palette.Muted,false); queueCount.Location=new Point(0,25); queueTitleBox.Controls.Add(queueCount); queueHeader.Controls.Add(queueTitleBox,0,0);
            var add=ModernButton("+  Adicionar arquivos",AddDialog); add.Width=166; add.Active=true; add.Margin=new Padding(0,4,8,6);
            var clear=ModernButton("Limpar fila",() => { files.Items.Clear(); RefreshTargets(); }); clear.Subtle=true; clear.ForeColor=Palette.Muted; clear.Width=92; clear.Margin=new Padding(0,4,0,6);
            queueHeader.Controls.Add(add,1,0); queueHeader.Controls.Add(clear,2,0); queueLayout.Controls.Add(queueHeader,0,0); locked.Add(add); locked.Add(clear);
            drop=new DropZone { Dock=DockStyle.Fill,Margin=new Padding(0,4,0,12) }; drop.BrowseRequested+=(s,e)=> { if(!busy) AddDialog(); }; queueLayout.Controls.Add(drop,0,1); locked.Add(drop);
            var fileArea=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty }; queueLayout.Controls.Add(fileArea,0,2);
            files.BorderStyle=BorderStyle.None; files.BackColor=Color.White; files.ForeColor=Palette.Ink; files.HeaderStyle=ColumnHeaderStyle.Nonclickable;
            files.Columns.Add("ARQUIVO",300); files.Columns.Add("TIPO",72); files.Columns.Add("TAMANHO",85); files.OwnerDraw=true;
            var rowHeight=new ImageList { ImageSize=new Size(1,54),ColorDepth=ColorDepth.Depth32Bit }; files.SmallImageList=rowHeight;
            files.DrawColumnHeader+=(s,e)=> { using(var brush=new SolidBrush(Color.White)) e.Graphics.FillRectangle(brush,e.Bounds); using(var font=new Font("Segoe UI",7.8f,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,e.Header.Text,font,new Rectangle(e.Bounds.X+(e.ColumnIndex==0?44:2),e.Bounds.Y,e.Bounds.Width,e.Bounds.Height),Palette.Faint,TextFormatFlags.Left|TextFormatFlags.VerticalCenter); using(var pen=new Pen(Palette.Line)) e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1); };
            files.DrawItem+=(s,e)=> { }; files.DrawSubItem+=DrawFile;
            files.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Delete && !busy) { RemoveSelected(); e.Handled=true; } else if(e.Control && e.KeyCode==Keys.A) { foreach(ListViewItem item in files.Items) item.Selected=true; e.Handled=true; } };
            files.SelectedIndexChanged+=(s,e)=>UpdateQueueButtons();
            fileArea.Controls.Add(files); locked.Add(files);
            emptyQueue=new Label { AutoSize=false, Dock=DockStyle.Fill };
            emptyQueue.Paint+=(s,e)=> {
                var g=e.Graphics; int cy=emptyQueue.Height/2;
                Palette.Glyph(g,"",22,new Rectangle(0,cy-62,emptyQueue.Width,40),Color.FromArgb(196,205,220));
                using(var f=new Font("Segoe UI",10.5f,FontStyle.Bold)) TextRenderer.DrawText(g,"Sua fila começa aqui",f,new Rectangle(0,cy-14,emptyQueue.Width,24),Palette.Ink,TextFormatFlags.HorizontalCenter);
                using(var f=new Font("Segoe UI",9.2f)) TextRenderer.DrawText(g,"Adicione um ou vários arquivos para converter.",f,new Rectangle(0,cy+12,emptyQueue.Width,22),Palette.Muted,TextFormatFlags.HorizontalCenter);
            };
            emptyQueue.Resize+=(s,e)=>emptyQueue.Invalidate(); fileArea.Controls.Add(emptyQueue);
            fileArea.Resize+=(s,e)=> { files.Columns[0].Width=Math.Max(180,fileArea.ClientSize.Width-157); };
            files.Resize+=(s,e)=> { files.Columns[0].Width=Math.Max(180,files.ClientSize.Width-157); };
            var reorder=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,10,0,0) };
            moveUp=ModernButton("↑  Subir",()=>MoveSelection(-1)); moveUp.Width=86;
            moveDown=ModernButton("↓  Descer",()=>MoveSelection(1)); moveDown.Width=90;
            removeFiles=ModernButton("Remover",RemoveSelected); removeFiles.Width=90; removeFiles.Subtle=true; removeFiles.ForeColor=Palette.Danger;
            reorder.Controls.AddRange(new Control[]{moveUp,moveDown,removeFiles}); queueLayout.Controls.Add(reorder,0,3); locked.Add(reorder);

            var config=new Card { Dock=DockStyle.Fill,Padding=new Padding(22,20,22,20),Margin=Padding.Empty }; body.Controls.Add(config,2,0);
            var configShell=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty }; configShell.RowStyles.Add(new RowStyle(SizeType.Percent,100)); configShell.RowStyles.Add(new RowStyle(SizeType.Absolute,104)); config.Controls.Add(configShell);
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty }; configShell.Controls.Add(scroll,0,0); locked.Add(scroll);
            var settings=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(0,0,4,8) }; settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); scroll.Controls.Add(settings);
            var configTitle=Palette.Text("Como você quer converter?",13,Palette.Ink,true); configTitle.Margin=new Padding(0,0,0,14); settings.Controls.Add(configTitle);
            settings.Controls.Add(FieldLabel("Operação")); StyleCombo(operation,false); settings.Controls.Add(operation);
            operation.Items.AddRange(new object[]{new Operation("Converter formato","convert"),new Operation("Juntar PDFs e imagens","merge-pdf"),new Operation("Separar PDF por página","split-pdf"),new Operation("Selecionar páginas do PDF","select-pages"),new Operation("Extrair texto do PDF","text-pdf"),new Operation("Extrair imagens do PDF","extract-images"),new Operation("Reconhecer texto (OCR)","ocr")});
            var formatLabel=FieldLabel("Formato de saída"); formatLabel.Margin=new Padding(0,16,0,7); settings.Controls.Add(formatLabel); StyleCombo(target,true); settings.Controls.Add(target);
            operation.SelectedIndexChanged+=(s,e)=> { RefreshTargets(); formatLabel.Visible=target.Visible=ActionId=="convert"; };
            target.SelectedIndexChanged+=(s,e)=>UpdateHints();
            rangeOptions=new Panel { Dock=DockStyle.Top,Height=92,Margin=new Padding(0,14,0,0) };
            var rangeTitle=FieldLabel("Páginas"); rangeTitle.Margin=Padding.Empty; rangeTitle.Location=Point.Empty; rangeOptions.Controls.Add(rangeTitle);
            pages.Font=new Font("Segoe UI",9.5f); var pagesFrame=new FieldFrame(pages) { Location=new Point(0,26),Width=282,Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right }; rangeOptions.Controls.Add(pagesFrame);
            var rangeHelp=Palette.Text("Ex.: 1-3,5  •  vazio = todas",8.5f,Palette.Muted,false); rangeHelp.Location=new Point(0,66); rangeOptions.Controls.Add(rangeHelp); settings.Controls.Add(rangeOptions);
            advanced=new CheckBox { Appearance=Appearance.Button,FlatStyle=FlatStyle.Flat,Text="+  Ajustes de imagem",AutoSize=false,Height=34,Dock=DockStyle.Top,ForeColor=Palette.Blue,BackColor=Color.White,Cursor=Cursors.Hand,Margin=new Padding(0,10,0,0),TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9.5f,FontStyle.Bold) };
            advanced.FlatAppearance.BorderSize=0; advanced.FlatAppearance.MouseOverBackColor=advanced.FlatAppearance.MouseDownBackColor=advanced.FlatAppearance.CheckedBackColor=Color.White; settings.Controls.Add(advanced);
            imageOptions=new Panel { Dock=DockStyle.Top,Height=104,Visible=false,Margin=new Padding(0,2,0,0) };
            var q=FieldLabel("Qualidade (1–100)"); q.Margin=Padding.Empty; q.Location=new Point(0,7); imageOptions.Controls.Add(q); quality.Location=new Point(196,2); quality.Width=86; quality.Anchor=AnchorStyles.Top|AnchorStyles.Right; imageOptions.Controls.Add(quality);
            var w=FieldLabel("Largura máxima (px)"); w.Margin=Padding.Empty; w.Location=new Point(0,47); imageOptions.Controls.Add(w); width.Location=new Point(196,42); width.Width=86; width.Anchor=AnchorStyles.Top|AnchorStyles.Right; imageOptions.Controls.Add(width);
            var widthHint=Palette.Text("0 mantém o tamanho original",8.5f,Palette.Muted,false); widthHint.Location=new Point(0,80); imageOptions.Controls.Add(widthHint); settings.Controls.Add(imageOptions);
            advanced.CheckedChanged+=(s,e)=> { imageOptions.Visible=advanced.Checked; advanced.Text=advanced.Checked?"−  Ajustes de imagem":"+  Ajustes de imagem"; };
            var saveTitle=FieldLabel("Salvar em"); saveTitle.Margin=new Padding(0,16,0,8); settings.Controls.Add(saveTitle);
            besideOriginal.Text="Mesma pasta do original"; besideOriginal.Font=new Font("Segoe UI",9.5f); besideOriginal.Margin=new Padding(0,0,0,10); besideOriginal.Cursor=Cursors.Hand; settings.Controls.Add(besideOriginal);
            var outputRow=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,Height=34,Margin=Padding.Empty }; outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));
            destination.Font=new Font("Segoe UI",9); destination.BackColor=Palette.Field; var destinationFrame=new FieldFrame(destination) { Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0) }; outputRow.Controls.Add(destinationFrame,0,0);
            var choose=ModernButton("Alterar…",ChooseDestination); choose.Dock=DockStyle.Fill; choose.Margin=Padding.Empty; choose.Height=34; outputRow.Controls.Add(choose,1,0); settings.Controls.Add(outputRow);
            outputRow.Visible=!besideOriginal.Checked;
            besideOriginal.CheckedChanged+=(s,e)=> { outputRow.Visible=!besideOriginal.Checked; if(!besideOriginal.Checked && string.IsNullOrWhiteSpace(destination.Text)) BeginInvoke(new Action(ChooseDestination)); };
            hint.Font=new Font("Segoe UI",8.8f); hint.ForeColor=Palette.Muted; hint.MaximumSize=new Size(280,0); hint.Padding=new Padding(0,14,0,8); hint.Margin=Padding.Empty; settings.Controls.Add(hint);
            var runArea=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(0,10,0,0) }; runArea.RowStyles.Add(new RowStyle(SizeType.Absolute,50)); runArea.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); configShell.Controls.Add(runArea,0,1);
            runArea.Paint+=(s,e)=> { using(var pen=new Pen(Palette.Line)) e.Graphics.DrawLine(pen,0,0,runArea.Width,0); };
            convert.Text="Converter arquivos  →"; convert.Dock=DockStyle.Fill; convert.AutoSize=false; convert.Margin=new Padding(0,0,0,0); ((AppButton)convert).Primary=true; convert.Font=new Font("Segoe UI",10.5f,FontStyle.Bold); convert.Click+=async(s,e)=>await ConvertNow(); runArea.Controls.Add(convert,0,0);
            var runSecondary=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,6,0,0),Margin=Padding.Empty }; runSecondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40)); runSecondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
            cancel.AutoSize=false; cancel.Dock=DockStyle.Fill; cancel.Margin=new Padding(0,0,8,0); cancel.Text="Cancelar"; cancel.Click+=(s,e)=>RequestCancel();
            open.AutoSize=false; open.Dock=DockStyle.Fill; open.Margin=Padding.Empty; open.Text="Abrir resultados"; open.Click+=(s,e)=>OpenResults();
            runSecondary.Controls.Add(cancel,0,0); runSecondary.Controls.Add(open,1,0); runArea.Controls.Add(runSecondary,0,1);
            progress.Height=6; progress.Visible=false; progress.Dock=DockStyle.Fill; progress.Margin=new Padding(0,6,0,2); main.Controls.Add(progress,0,2);
            logCard=new Card { Dock=DockStyle.Top,Height=128,Visible=false,Margin=Padding.Empty,Padding=new Padding(14,12,8,10) }; log.BorderStyle=BorderStyle.None; log.Font=new Font("Consolas",8.8f); log.ForeColor=Palette.Muted; log.BackColor=Color.White; logCard.Controls.Add(log); main.Controls.Add(logCard,0,3);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=new Padding(0,10,0,0) }; footer.RowStyles.Add(new RowStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,136));
            var state=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty };
            stateDot=new Panel { Location=new Point(1,7),Size=new Size(10,10) }; stateDot.Paint+=(s,e)=> { e.Graphics.Clear(Palette.Canvas); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var b=new SolidBrush(stateColor)) e.Graphics.FillEllipse(b,0,0,9,9); }; state.Controls.Add(stateDot);
            stateTitle=Palette.Text("Pronto para converter",10,Palette.Ink,true); stateTitle.Location=new Point(18,0); state.Controls.Add(stateTitle);
            status.Font=new Font("Segoe UI",8.8f); status.ForeColor=Palette.Muted; status.AutoSize=false; status.Dock=DockStyle.None; status.Location=new Point(18,22); status.Size=new Size(700,22); status.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right; status.Padding=Padding.Empty; status.AutoEllipsis=true; state.Controls.Add(status); footer.Controls.Add(state);
            showDetails=ModernButton("Ver detalhes",()=> { logCard.Visible=!logCard.Visible; showDetails.Text=logCard.Visible?"Ocultar detalhes":"Ver detalhes"; }); showDetails.Dock=DockStyle.Fill; showDetails.Margin=new Padding(0,4,0,8); showDetails.Subtle=true; showDetails.ForeColor=Palette.Muted; footer.Controls.Add(showDetails); main.Controls.Add(footer,0,4);
            modernReady=true; operation.SelectedIndex=0;
            EnableFileDrop(this);
            KeyDown+=(s,e)=> { if(e.Control && e.KeyCode==Keys.O && !busy) { AddDialog(); e.Handled=true; } };
            FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; closeWhenIdle=true; RequestCancel(); } };
            ResumeLayout(true);
            AddFiles(inputs??new string[0]);
            if(preset!=null && target.Items.Contains(preset.ToUpperInvariant())) { target.SelectedItem=preset.ToUpperInvariant(); Shown+=async(s,e)=>await ConvertNow(); }
        }
        // Eventos de arrastar não sobem para o pai no WinForms: cada controle precisa aceitar o arquivo.
        void EnableFileDrop(Control control) {
            control.AllowDrop=true;
            control.DragEnter+=(s,e)=> { bool ok=!busy && e.Data.GetDataPresent(DataFormats.FileDrop); e.Effect=ok?DragDropEffects.Copy:DragDropEffects.None; drop.Highlight=ok; };
            control.DragLeave+=(s,e)=>drop.Highlight=false;
            control.DragDrop+=(s,e)=> { drop.Highlight=false; if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); };
            foreach(Control child in control.Controls) EnableFileDrop(child);
        }
        AppButton NavButton(string text,string icon,Action action) { var button=ModernButton(text,action); button.Size=new Size(188,42); button.Nav=true; button.Icon=icon; button.Margin=new Padding(0,0,0,4); button.TextAlign=ContentAlignment.MiddleLeft; return button; }
        static AppButton ModernButton(string text,Action action) { var button=new AppButton { Text=text }; button.Click+=(s,e)=>action(); return button; }
        static Label FieldLabel(string text) { var label=Palette.Text(text,9,Palette.Ink,true); label.Margin=new Padding(0,6,0,7); return label; }
        void StyleCombo(ComboBox box,bool large) {
            box.BackColor=large?Palette.Tint:Palette.Field; box.ForeColor=large?Palette.Blue:Palette.Ink; box.Font=new Font("Segoe UI",large?12:9.5f,large?FontStyle.Bold:FontStyle.Regular); box.Margin=Padding.Empty;
            box.ItemHeight=large?32:26; box.DropDownWidth=286; box.Cursor=Cursors.Hand;
        }
        void DrawFile(object sender,DrawListViewSubItemEventArgs e) {
            bool selected=e.Item.Selected; using(var b=new SolidBrush(selected?Palette.Tint:Color.White))e.Graphics.FillRectangle(b,e.Bounds);
            string path=(string)e.Item.Tag, extension=Path.GetExtension(path);
            Color back,ink; Palette.KindColors(extension,out back,out ink);
            if(e.ColumnIndex==0) {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                var tile=new Rectangle(e.Bounds.X+4,e.Bounds.Y+(e.Bounds.Height-32)/2,32,32);
                using(var shape=Palette.Round(tile,8))using(var b=new SolidBrush(back))e.Graphics.FillPath(b,shape);
                Palette.Glyph(e.Graphics,Catalog.Images.Contains(extension.ToLowerInvariant())?"":Catalog.Audio.Contains(extension.ToLowerInvariant())?"":Catalog.Video.Contains(extension.ToLowerInvariant())?"":"",11,tile,ink);
                var text=new Rectangle(e.Bounds.X+46,e.Bounds.Y+8,e.Bounds.Width-50,20);
                using(var font=new Font("Segoe UI",9.6f,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,e.Item.Text,font,text,Palette.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPrefix);
                text.Y+=21; text.Height=18; using(var font=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,Path.GetDirectoryName(path),font,text,Palette.Muted,TextFormatFlags.PathEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPrefix);
            } else if(e.ColumnIndex==1) {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                var r=new Rectangle(e.Bounds.X+2,e.Bounds.Y+(e.Bounds.Height-22)/2,Math.Min(54,e.Bounds.Width-6),22);
                using(var shape=Palette.Round(r,11))using(var b=new SolidBrush(back))e.Graphics.FillPath(b,shape);
                using(var font=new Font("Segoe UI",7.8f,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,extension.TrimStart('.').ToUpperInvariant(),font,r,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            } else {
                string size=""; try { long bytes=new FileInfo(path).Length; size=bytes>=1048576?(bytes/1048576d).ToString("0.0")+" MB":Math.Max(1,bytes/1024d).ToString("0")+" KB"; } catch { }
                using(var font=new Font("Segoe UI",8.5f))TextRenderer.DrawText(e.Graphics,size,font,e.Bounds,Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            }
            using(var pen=new Pen(Palette.Line))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
        }
        void SetState(string title,Color color) { stateTitle.Text=title; stateColor=color; stateDot.Invalidate(); }
        void UpdateQueueButtons() {
            if(!modernReady)return;
            int count=files.SelectedItems.Count, index=count==1?files.SelectedIndices[0]:-1;
            moveUp.Enabled=!busy&&index>0; moveDown.Enabled=!busy&&index>=0&&index<files.Items.Count-1; removeFiles.Enabled=!busy&&count>0;
        }
        void RefreshModernState() {
            if(!modernReady)return;
            if(!busy)SetState("Pronto para converter",files.Items.Count>0?Palette.Blue:Palette.Faint);
            queueCount.Text=files.Items.Count==0?"Nenhum arquivo na fila":files.Items.Count==1?"1 arquivo na fila":files.Items.Count+" arquivos na fila";
            emptyQueue.Visible=files.Items.Count==0; files.Visible=files.Items.Count>0;
            navConvert.Active=ActionId=="convert"; navOcr.Active=ActionId=="ocr"; navPdf.Active=!navConvert.Active&&!navOcr.Active;
            navConvert.Invalidate(); navOcr.Invalidate(); navPdf.Invalidate();
            rangeOptions.Visible=ActionId!="merge-pdf"&&(ActionId!="convert"||files.Items.Count>0&&Path.GetExtension((string)files.Items[0].Tag).Equals(".pdf",StringComparison.OrdinalIgnoreCase));
            bool images=ActionId=="convert"&&target.SelectedItem!=null&&(Catalog.ImageTargets.Contains((string)target.SelectedItem)||(string)target.SelectedItem=="PDF"&&files.Items.Count>0&&Catalog.Images.Contains(Path.GetExtension((string)files.Items[0].Tag).ToLowerInvariant()))||ActionId=="merge-pdf";
            advanced.Visible=images; imageOptions.Visible=images&&advanced.Checked;
            convert.Text=ActionId=="merge-pdf"?"Juntar em um PDF  →":ActionId=="ocr"?"Reconhecer texto  →":"Converter arquivos  →";
            UpdateQueueButtons();
        }
    }
}
