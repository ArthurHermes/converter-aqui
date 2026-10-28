using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;

namespace ConverterAqui {
    static class Palette {
        public static readonly Color Canvas = Color.FromArgb(245, 247, 251);
        public static readonly Color Ink = Color.FromArgb(26, 36, 57);
        public static readonly Color Muted = Color.FromArgb(105, 118, 140);
        public static readonly Color Line = Color.FromArgb(230, 235, 243);
        public static readonly Color Blue = Color.FromArgb(51, 99, 235);
        public static readonly Color Tint = Color.FromArgb(237, 243, 255);
        public static GraphicsPath Round(RectangleF r, float radius) {
            float d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static Label Text(string text, float size, Color color, bool bold) {
            return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = Padding.Empty };
        }
    }
    sealed class AppButton : Button {
        public bool Primary, Subtle, Active;
        bool hover;
        public AppButton() {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
            BackColor = Color.White; ForeColor = Palette.Ink; Cursor = Cursors.Hand; Height = 36;
            Padding = new Padding(10, 2, 10, 2); Margin = new Padding(0, 0, 7, 0); Font = new Font("Segoe UI", 9.5f);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (hover ? Color.FromArgb(39, 79, 207) : Palette.Blue) : Active || hover ? Palette.Tint : Subtle ? BackColor : Color.White;
            if (!Enabled) fill = Color.FromArgb(235, 239, 246);
            using (var path = Palette.Round(new RectangleF(1, 1, Width-3, Height-3), 9)) {
                using (var b = new SolidBrush(fill)) e.Graphics.FillPath(b, path);
                if (!Primary && !Subtle && !Active) using (var pen = new Pen(Palette.Line)) e.Graphics.DrawPath(pen, path);
            }
            Color ink = !Enabled ? Color.FromArgb(151, 161, 178) : Primary ? Color.White : Active ? Palette.Blue : ForeColor;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(8, 0, Width-16, Height), ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5,5,Width-10,Height-10), ink, fill);
        }
    }
    sealed class Card : Panel {
        public Card() { BackColor = Color.White; Padding = new Padding(20); SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Palette.Round(new RectangleF(0,0,Width-1,Height-1), 12)) using (var pen = new Pen(Palette.Line)) e.Graphics.DrawPath(pen,p);
        }
    }
    sealed class DropZone : Panel {
        public event EventHandler BrowseRequested;
        public DropZone() { DoubleBuffered = true; Cursor = Cursors.Hand; BackColor = Palette.Canvas; }
        protected override void OnClick(EventArgs e) { base.OnClick(e); if (BrowseRequested != null) BrowseRequested(this, e); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Palette.Round(new RectangleF(1,1,Width-3,Height-3),10)) using (var pen = new Pen(Color.FromArgb(194,209,238))) { pen.DashStyle = DashStyle.Dash; g.DrawPath(pen,shape); }
            int x = 24, y = (Height-32)/2;
            using (var pen = new Pen(Palette.Blue,2)) {
                g.DrawLine(pen,x,y+9,x,y+28); g.DrawLine(pen,x,y+28,x+27,y+28); g.DrawLine(pen,x+27,y+28,x+27,y+9);
                g.DrawLine(pen,x+13,y+19,x+13,y); g.DrawLine(pen,x+13,y,x+6,y+7); g.DrawLine(pen,x+13,y,x+20,y+7);
            }
            using(var f = new Font("Segoe UI",10,FontStyle.Bold)) TextRenderer.DrawText(g,"Arraste seus arquivos para cá",f,new Rectangle(72,15,Width-85,25),Palette.Ink,TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using(var f = new Font("Segoe UI",9)) TextRenderer.DrawText(g,"ou clique para escolher no computador",f,new Rectangle(72,42,Width-85,22),Palette.Muted,TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    sealed partial class MainForm {
        Label queueCount, stateTitle;
        Panel imageOptions, rangeOptions, logCard;
        Label emptyQueue;
        AppButton navConvert, navPdf, navOcr, showDetails;
        CheckBox advanced;
        bool modernReady;
        void InitializeModernInterface(string[] inputs, string preset) {
            Text = "Converter Aqui 2.1"; Font = new Font("Segoe UI",10); ForeColor = Palette.Ink; BackColor = Palette.Canvas;
            ClientSize = new Size(1200,810); MinimumSize = new Size(1080,740); StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi; AllowDrop = true; DoubleBuffered = true;
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(shell);
            var side = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18,26,18,18) }; shell.Controls.Add(side,0,0);
            var brand = Palette.Text("converter aqui",16,Palette.Ink,true); brand.Location = new Point(19,28); side.Controls.Add(brand);
            var brandSub = Palette.Text("Seu conversor de arquivos",8.5f,Palette.Muted,false); brandSub.Location = new Point(20,61); side.Controls.Add(brandSub);
            var nav = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.None, Location = new Point(12,126), Size = new Size(185,180), WrapContents = false };
            navConvert = NavButton("Conversão de arquivos",() => operation.SelectedIndex=0);
            navPdf = NavButton("Ferramentas de PDF",() => operation.SelectedIndex=1);
            navOcr = NavButton("Reconhecer texto",() => operation.SelectedIndex=6);
            nav.Controls.AddRange(new Control[] { navConvert,navPdf,navOcr }); side.Controls.Add(nav);
            var sideFoot = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 184, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var title = Palette.Text("MENU DO WINDOWS",8,Palette.Muted,true); title.Margin = new Padding(4,0,0,12); sideFoot.Controls.Add(title);
            var install = ModernButton("Ativar botão direito",() => MenuAction(true)); install.Width=165; sideFoot.Controls.Add(install);
            var uninstall = ModernButton("Remover integração",() => MenuAction(false)); uninstall.Width=165; uninstall.Subtle=true; uninstall.ForeColor=Palette.Muted; sideFoot.Controls.Add(uninstall);
            var privacy = Palette.Text("●  Conversão local\nSeus arquivos ficam com você.",8.5f,Palette.Muted,false); privacy.Margin=new Padding(4,18,0,0); sideFoot.Controls.Add(privacy); side.Controls.Add(sideFoot); locked.Add(sideFoot);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28,24,28,20), ColumnCount=1,RowCount=5, Margin=Padding.Empty };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,102)); main.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,10)); main.RowStyles.Add(new RowStyle(SizeType.AutoSize)); main.RowStyles.Add(new RowStyle(SizeType.Absolute,64)); shell.Controls.Add(main,1,0);
            var heading = new Panel { Dock=DockStyle.Fill };
            var eyebrow=Palette.Text("MENOS ETAPAS. MAIS POSSIBILIDADES.",8.2f,Palette.Blue,true); eyebrow.Location=new Point(0,1); heading.Controls.Add(eyebrow);
            var headline=Palette.Text("O formato certo, em poucos cliques.",23,Palette.Ink,true); headline.Location=new Point(-2,24); heading.Controls.Add(headline);
            var subtitle=Palette.Text("Imagens, documentos, áudio e vídeo. Tudo no seu computador.",10,Palette.Muted,false); subtitle.Location=new Point(0,68); heading.Controls.Add(subtitle); main.Controls.Add(heading,0,0);

            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,322)); main.Controls.Add(body,0,1);
            var queue=new Card { Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(18) }; body.Controls.Add(queue,0,0);
            var queueLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty };
            queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,92)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); queue.Controls.Add(queueLayout);
            var queueHeader=new Panel { Dock=DockStyle.Fill }; var queueTitle=Palette.Text("Seus arquivos",14,Palette.Ink,true); queueTitle.Location=new Point(0,0); queueHeader.Controls.Add(queueTitle);
            queueCount=Palette.Text("0 na fila",9,Palette.Muted,false); queueCount.Dock=DockStyle.Right; queueCount.TextAlign=ContentAlignment.MiddleRight; queueHeader.Controls.Add(queueCount); queueLayout.Controls.Add(queueHeader,0,0);
            var queueActions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty };
            var add=ModernButton("+  Adicionar arquivos",AddDialog); add.Width=170; add.Active=true;
            var clear=ModernButton("Limpar fila",() => { files.Items.Clear(); RefreshTargets(); }); clear.Subtle=true; clear.ForeColor=Palette.Muted; clear.Width=95;
            queueActions.Controls.AddRange(new Control[]{add,clear}); queueLayout.Controls.Add(queueActions,0,1); locked.Add(queueActions);
            var drop=new DropZone { Dock=DockStyle.Fill,Margin=new Padding(0,1,0,12) }; drop.BrowseRequested+=(s,e)=> { if(!busy) AddDialog(); }; queueLayout.Controls.Add(drop,0,2); locked.Add(drop);
            var fileArea=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty }; queueLayout.Controls.Add(fileArea,0,3);
            files.BorderStyle=BorderStyle.None; files.BackColor=Color.White; files.ForeColor=Palette.Ink; files.HeaderStyle=ColumnHeaderStyle.Nonclickable;
            files.Columns.Add("ARQUIVO",300); files.Columns.Add("TIPO",72); files.Columns.Add("TAMANHO",85); files.OwnerDraw=true;
            var rowHeight=new ImageList { ImageSize=new Size(1,56),ColorDepth=ColorDepth.Depth32Bit }; files.SmallImageList=rowHeight;
            files.DrawColumnHeader+=(s,e)=> { using(var brush=new SolidBrush(Color.White)) e.Graphics.FillRectangle(brush,e.Bounds); using(var font=new Font("Segoe UI",8,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,e.Header.Text,font,e.Bounds,Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter); using(var pen=new Pen(Palette.Line)) e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1); };
            files.DrawItem+=(s,e)=> { }; files.DrawSubItem+=DrawFile;
            fileArea.Controls.Add(files); locked.Add(files);
            emptyQueue=Palette.Text("Sua fila começa aqui.\nAdicione um ou vários arquivos para converter.",10,Palette.Muted,false); emptyQueue.AutoSize=false; emptyQueue.Dock=DockStyle.Fill; emptyQueue.TextAlign=ContentAlignment.MiddleCenter; fileArea.Controls.Add(emptyQueue);
            fileArea.Resize+=(s,e)=> { files.Columns[0].Width=Math.Max(180,fileArea.ClientSize.Width-157); };
            files.Resize+=(s,e)=> { files.Columns[0].Width=Math.Max(180,files.ClientSize.Width-157); };
            var reorder=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,4,0,0) };
            var up=ModernButton("↑  Subir",()=>MoveSelection(-1)); up.Width=82;
            var down=ModernButton("↓  Descer",()=>MoveSelection(1)); down.Width=82;
            var remove=ModernButton("Remover",()=> { foreach(ListViewItem item in files.SelectedItems.Cast<ListViewItem>().ToArray()) files.Items.Remove(item); RefreshTargets(); }); remove.Width=90; remove.Subtle=true; remove.ForeColor=Palette.Muted;
            reorder.Controls.AddRange(new Control[]{up,down,remove}); queueLayout.Controls.Add(reorder,0,4); locked.Add(reorder);

            var config=new Card { Dock=DockStyle.Fill,Padding=new Padding(20),Margin=Padding.Empty }; body.Controls.Add(config,2,0);
            var configShell=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty }; configShell.RowStyles.Add(new RowStyle(SizeType.Percent,100)); configShell.RowStyles.Add(new RowStyle(SizeType.Absolute,106)); config.Controls.Add(configShell);
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty }; configShell.Controls.Add(scroll,0,0); locked.Add(scroll);
            var settings=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(0,0,3,8) }; settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); scroll.Controls.Add(settings);
            var configTitle=Palette.Text("Como você quer converter?",13,Palette.Ink,true); configTitle.Margin=new Padding(0,0,0,18); settings.Controls.Add(configTitle);
            settings.Controls.Add(FieldLabel("Operação")); StyleCombo(operation,false); settings.Controls.Add(operation);
            operation.Items.AddRange(new object[]{new Operation("Converter formato","convert"),new Operation("Juntar PDFs e imagens","merge-pdf"),new Operation("Separar PDF por página","split-pdf"),new Operation("Selecionar páginas do PDF","select-pages"),new Operation("Extrair texto do PDF","text-pdf"),new Operation("Extrair imagens do PDF","extract-images"),new Operation("Reconhecer texto (OCR)","ocr")});
            var formatLabel=FieldLabel("Formato de saída"); settings.Controls.Add(formatLabel); StyleCombo(target,true); settings.Controls.Add(target);
            operation.SelectedIndexChanged+=(s,e)=> { RefreshTargets(); formatLabel.Visible=target.Visible=ActionId=="convert"; };
            target.SelectedIndexChanged+=(s,e)=>UpdateHints();
            rangeOptions=new Panel { Dock=DockStyle.Top,Height=80,Margin=new Padding(0,12,0,0) };
            var rangeTitle=FieldLabel("Páginas"); rangeTitle.Location=Point.Empty; rangeOptions.Controls.Add(rangeTitle); pages.Dock=DockStyle.None; pages.Location=new Point(1,26); pages.Width=252; pages.BorderStyle=BorderStyle.FixedSingle; rangeOptions.Controls.Add(pages);
            var rangeHelp=Palette.Text("Ex.: 1-3,5  •  vazio = todas",8.5f,Palette.Muted,false); rangeHelp.Location=new Point(0,58); rangeOptions.Controls.Add(rangeHelp); settings.Controls.Add(rangeOptions);
            advanced=new CheckBox { Appearance=Appearance.Button,FlatStyle=FlatStyle.Flat,Text="+  Ajustes de imagem",AutoSize=false,Height=34,Dock=DockStyle.Top,ForeColor=Palette.Blue,BackColor=Color.White,Cursor=Cursors.Hand,Margin=new Padding(0,12,0,0) }; advanced.FlatAppearance.BorderSize=0; settings.Controls.Add(advanced);
            imageOptions=new Panel { Dock=DockStyle.Top,Height=100,Visible=false,Margin=new Padding(0,2,0,0) };
            var q=FieldLabel("Qualidade (1–100)"); q.Location=new Point(0,3); imageOptions.Controls.Add(q); quality.Location=new Point(184,0); quality.Width=76; imageOptions.Controls.Add(quality);
            var w=FieldLabel("Largura máxima"); w.Location=new Point(0,43); imageOptions.Controls.Add(w); width.Location=new Point(160,40); width.Width=100; imageOptions.Controls.Add(width);
            var widthHint=Palette.Text("0 mantém o tamanho original",8.5f,Palette.Muted,false); widthHint.Location=new Point(0,77); imageOptions.Controls.Add(widthHint); settings.Controls.Add(imageOptions);
            advanced.CheckedChanged+=(s,e)=> { imageOptions.Visible=advanced.Checked; advanced.Text=advanced.Checked?"−  Ajustes de imagem":"+  Ajustes de imagem"; };
            var saveTitle=FieldLabel("Salvar em"); saveTitle.Margin=new Padding(0,20,0,10); settings.Controls.Add(saveTitle);
            besideOriginal.Text="Mesma pasta do original"; besideOriginal.Font=new Font("Segoe UI",9.5f); besideOriginal.Margin=new Padding(0,0,0,10); settings.Controls.Add(besideOriginal);
            var outputRow=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=2,Height=34,Margin=Padding.Empty }; outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,78));
            destination.BorderStyle=BorderStyle.FixedSingle; destination.Font=new Font("Segoe UI",9); destination.Margin=new Padding(0,5,6,0); outputRow.Controls.Add(destination);
            var choose=ModernButton("Alterar…",()=> { using(var dialog=new FolderBrowserDialog { SelectedPath=destination.Text }) if(dialog.ShowDialog()==DialogResult.OK) { destination.Text=dialog.SelectedPath; besideOriginal.Checked=false; } }); choose.Dock=DockStyle.Fill; choose.Margin=Padding.Empty; outputRow.Controls.Add(choose); settings.Controls.Add(outputRow);
            outputRow.Visible=!besideOriginal.Checked;
            besideOriginal.CheckedChanged+=(s,e)=> { outputRow.Visible=!besideOriginal.Checked; };
            hint.Font=new Font("Segoe UI",8.8f); hint.ForeColor=Palette.Muted; hint.MaximumSize=new Size(262,0); hint.Padding=new Padding(0,14,0,8); hint.Margin=Padding.Empty; settings.Controls.Add(hint);
            var runArea=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(0,8,0,0) }; runArea.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); runArea.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); configShell.Controls.Add(runArea,0,1);
            convert.Text="Converter arquivos  →"; convert.Dock=DockStyle.Fill; convert.AutoSize=false; convert.Margin=Padding.Empty; ((AppButton)convert).Primary=true; convert.Font=new Font("Segoe UI",11,FontStyle.Bold); convert.Click+=async(s,e)=>await ConvertNow(); runArea.Controls.Add(convert,0,0);
            var runSecondary=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,5,0,0),Margin=Padding.Empty }; cancel.AutoSize=false; cancel.Width=92; cancel.Text="Cancelar"; cancel.Click+=(s,e)=>RequestCancel(); open.AutoSize=false; open.Width=167; open.Text="Abrir resultados"; open.Click+=(s,e)=> { if(Directory.Exists(resultsFolder)) Process.Start("explorer.exe","\""+resultsFolder+"\""); }; runSecondary.Controls.AddRange(new Control[]{cancel,open}); runArea.Controls.Add(runSecondary,0,1);
            progress.Height=4; progress.Visible=false; progress.Dock=DockStyle.Fill; progress.Margin=new Padding(0,5,0,0); main.Controls.Add(progress,0,2);
            logCard=new Panel { Dock=DockStyle.Top,Height=108,Visible=false,Margin=Padding.Empty,Padding=new Padding(12),BackColor=Color.White }; log.BorderStyle=BorderStyle.None; log.Font=new Font("Consolas",8.5f); log.ForeColor=Palette.Muted; logCard.Controls.Add(log); main.Controls.Add(logCard,0,3);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=new Padding(0,12,0,0) }; footer.RowStyles.Add(new RowStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
            var state=new Panel { Dock=DockStyle.Fill }; stateTitle=Palette.Text("Pronto para converter",10,Palette.Ink,true); stateTitle.Location=Point.Empty; state.Controls.Add(stateTitle); status.Font=new Font("Segoe UI",8.8f); status.ForeColor=Palette.Muted; status.AutoSize=false; status.Dock=DockStyle.None; status.Location=new Point(0,25); status.Size=new Size(650,22); status.Padding=Padding.Empty; state.Controls.Add(status); footer.Controls.Add(state);
            showDetails=ModernButton("Ver detalhes",()=> { logCard.Visible=!logCard.Visible; showDetails.Text=logCard.Visible?"Ocultar detalhes":"Ver detalhes"; }); showDetails.Dock=DockStyle.Fill; showDetails.Margin=new Padding(0,7,0,7); showDetails.Subtle=true; footer.Controls.Add(showDetails); main.Controls.Add(footer,0,4);
            modernReady=true; operation.SelectedIndex=0;
            DragEnter+=(s,e)=> { if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy; };
            DragDrop+=(s,e)=> { if(!busy) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); };
            FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; RequestCancel(); } };
            AddFiles(inputs??new string[0]);
            if(preset!=null && target.Items.Contains(preset.ToUpperInvariant())) { target.SelectedItem=preset.ToUpperInvariant(); Shown+=async(s,e)=>await ConvertNow(); }
        }
        AppButton NavButton(string text,Action action) { var button=ModernButton(text,action); button.Size=new Size(180,44); button.Subtle=true; button.Margin=new Padding(0,0,0,6); return button; }
        static AppButton ModernButton(string text,Action action) { var button=new AppButton { Text=text }; button.Click+=(s,e)=>action(); return button; }
        static Label FieldLabel(string text) { var label=Palette.Text(text,9,Palette.Ink,true); label.Margin=new Padding(0,7,0,7); return label; }
        void StyleCombo(ComboBox box,bool large) {
            box.FlatStyle=FlatStyle.Flat; box.BackColor=large?Palette.Tint:Palette.Canvas; box.ForeColor=large?Palette.Blue:Palette.Ink; box.Font=new Font("Segoe UI",large?13:9.5f,large?FontStyle.Bold:FontStyle.Regular); box.Margin=Padding.Empty;
            box.DrawMode=DrawMode.OwnerDrawFixed; box.ItemHeight=large?34:28; box.DropDownWidth=280;
            box.DrawItem+=(s,e)=> { if(e.Index<0)return; Color bg=(e.State&DrawItemState.Selected)!=0?Palette.Tint:Color.White; using(var b=new SolidBrush(bg)) e.Graphics.FillRectangle(b,e.Bounds); TextRenderer.DrawText(e.Graphics,box.Items[e.Index].ToString(),box.Font,new Rectangle(e.Bounds.X+9,e.Bounds.Y,e.Bounds.Width-15,e.Bounds.Height),large?Palette.Blue:Palette.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); e.DrawFocusRectangle(); };
        }
        void DrawFile(object sender,DrawListViewSubItemEventArgs e) {
            bool selected=e.Item.Selected; using(var b=new SolidBrush(selected?Palette.Tint:Color.White))e.Graphics.FillRectangle(b,e.Bounds);
            string path=(string)e.Item.Tag;
            if(e.ColumnIndex==0) {
                var text=new Rectangle(e.Bounds.X+4,e.Bounds.Y+8,e.Bounds.Width-8,22);
                using(var font=new Font("Segoe UI",9.8f,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,e.Item.Text,font,text,Palette.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.Left);
                text.Y+=23; text.Height=18; using(var font=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,Path.GetDirectoryName(path),font,text,Palette.Muted,TextFormatFlags.PathEllipsis|TextFormatFlags.Left);
            } else if(e.ColumnIndex==1) {
                var r=new Rectangle(e.Bounds.X+2,e.Bounds.Y+15,Math.Min(58,e.Bounds.Width-5),25);
                using(var shape=Palette.Round(r,5))using(var b=new SolidBrush(Color.FromArgb(239,242,248)))e.Graphics.FillPath(b,shape);
                using(var font=new Font("Segoe UI",8,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),font,r,Palette.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            } else {
                string size=""; try { long bytes=new FileInfo(path).Length; size=bytes>=1048576?(bytes/1048576d).ToString("0.0")+" MB":Math.Max(1,bytes/1024d).ToString("0")+" KB"; } catch { }
                using(var font=new Font("Segoe UI",8.5f))TextRenderer.DrawText(e.Graphics,size,font,e.Bounds,Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            }
            using(var pen=new Pen(Palette.Line))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
        }
        void RefreshModernState() {
            if(!modernReady)return;
            if(!busy)stateTitle.Text="Pronto para converter";
            queueCount.Text=files.Items.Count+" na fila"; emptyQueue.Visible=files.Items.Count==0; files.Visible=files.Items.Count>0;
            navConvert.Active=ActionId=="convert"; navOcr.Active=ActionId=="ocr"; navPdf.Active=!navConvert.Active&&!navOcr.Active;
            navConvert.Invalidate(); navOcr.Invalidate(); navPdf.Invalidate();
            rangeOptions.Visible=ActionId!="merge-pdf"&&(ActionId!="convert"||files.Items.Count>0&&Path.GetExtension((string)files.Items[0].Tag).Equals(".pdf",StringComparison.OrdinalIgnoreCase));
            bool images=ActionId=="convert"&&target.SelectedItem!=null&&(Catalog.ImageTargets.Contains((string)target.SelectedItem)||(string)target.SelectedItem=="PDF"&&files.Items.Count>0&&Catalog.Images.Contains(Path.GetExtension((string)files.Items[0].Tag).ToLowerInvariant()))||ActionId=="merge-pdf";
            advanced.Visible=images; imageOptions.Visible=images&&advanced.Checked;
            convert.Text=ActionId=="merge-pdf"?"Juntar em um PDF  →":ActionId=="ocr"?"Reconhecer texto  →":"Converter arquivos  →";
        }
    }
}
