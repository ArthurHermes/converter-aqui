using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using Microsoft.Win32;

namespace ConverterAqui {
    sealed class JsonAdapter {
        public string Serialize(object value) { return JsonCodec.Serialize(value); }
        public T Deserialize<T>(string value) { return JsonCodec.Deserialize<T>(value); }
    }

    static class Program {
        [STAThread] static int Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                if (args.Length == 1 && args[0] == "--install") { ShellMenu.Install(); return 0; }
                if (args.Length == 1 && args[0] == "--uninstall") { ShellMenu.Remove(); return 0; }
                if (args.Length == 2 && args[0] == "--job") return Engine.Run(args[1], null).GetAwaiter().GetResult();
                string target = null;
                string[] inputs = args;
                if (args.Length >= 3 && args[0] == "--to") { target = args[1]; inputs = args.Skip(2).ToArray(); }
                else if (args.Length > 0 && args[0] == "--queue") inputs = args.Skip(1).ToArray();
                Application.Run(new MainForm(inputs, target)); return 0;
            } catch (Exception ex) { MessageBox.Show(ex.Message, "Converter Aqui", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }

    static class Catalog {
        public static readonly string[] ImageTargets = { "PNG", "JPG", "WEBP", "HEIC", "BMP", "GIF", "TIFF" };
        public static readonly string[] AudioTargets = { "MP3", "WAV", "FLAC", "OGG", "M4A" };
        public static readonly string[] VideoTargets = { "MP4", "MKV", "WEBM" };
        public static readonly string[] Images = { ".png", ".jpg", ".jpeg", ".webp", ".heic", ".heif", ".svg", ".bmp", ".gif", ".tif", ".tiff" };
        public static readonly string[] Audio = { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".opus", ".wma", ".aiff" };
        public static readonly string[] Video = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v", ".mpeg", ".mpg" };
        public static readonly string[] Docs = { ".doc", ".docx", ".rtf", ".odt", ".txt", ".xls", ".xlsx", ".xlsm", ".ods", ".csv" };
        public static string[] Extensions { get { return Images.Concat(Audio).Concat(Video).Concat(Docs).Concat(new[] { ".pdf" }).ToArray(); } }
        public static string[] Targets(string extension) {
            extension = extension.ToLowerInvariant();
            if (Images.Contains(extension)) return ImageTargets.Concat(new[] { "PDF" }).ToArray();
            if (extension == ".pdf") return ImageTargets;
            if (Audio.Contains(extension)) return AudioTargets;
            if (Video.Contains(extension)) return VideoTargets.Concat(AudioTargets).ToArray();
            if (Docs.Contains(extension)) return new[] { "PDF" };
            return new string[0];
        }
    }

    static class Engine {
        public static readonly JsonAdapter Json = new JsonAdapter();
        public static async Task<int> Run(string job, Action<string> progress) {
            string home = AppDomain.CurrentDomain.BaseDirectory;
            string python = Path.Combine(home, @"runtime\python.exe");
            if (!File.Exists(python)) throw new FileNotFoundException("Componentes ausentes. Extraia a pasta inteira do ZIP antes de abrir o app.");
            var start = new ProcessStartInfo(python, Quote(Path.Combine(home, "converter_backend.py")) + " " + Quote(Path.GetFullPath(job)));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8; start.StandardErrorEncoding = Encoding.UTF8;
            start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            using (var process = Process.Start(start)) {
                var errors = process.StandardError.ReadToEndAsync();
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null) {
                    try {
                        var data = Json.Deserialize<Dictionary<string, object>>(line);
                        if (progress != null && data.ContainsKey("message")) progress(Convert.ToString(data["message"]));
                    } catch (System.Text.Json.JsonException) { }
                }
                await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                string stderr = await errors.ConfigureAwait(false);
                if (!File.Exists(job + ".result.json")) throw new Exception("O componente não retornou um resultado. " + stderr);
                return process.ExitCode;
            }
        }
        static string Quote(string text) { return "\"" + text.Replace("\"", "") + "\""; }
    }

    static class ShellMenu {
        const string Root = @"Software\Classes\SystemFileAssociations\";
        const string KeyName = @"\shell\ConverterAqui";
        [DllImport("shell32.dll")] static extern void SHChangeNotify(uint id, uint flags, IntPtr one, IntPtr two);
        public static void Install() {
            foreach (string extension in Catalog.Extensions) {
                // Limpa somente a entrada própria para não manter formatos antigos.
                Registry.CurrentUser.DeleteSubKeyTree(Root + extension + KeyName, false);
                using (var key = Registry.CurrentUser.CreateSubKey(Root + extension + KeyName)) {
                    key.SetValue("MUIVerb", "Converter para…"); key.SetValue("SubCommands", "");
                    key.SetValue("Icon", "\"" + Application.ExecutablePath + "\",0"); key.SetValue("MultiSelectModel", "Single");
                    key.SetValue("OwnerExecutable", Application.ExecutablePath);
                    foreach (string format in Catalog.Targets(extension)) {
                        using (var verb = key.CreateSubKey("shell\\" + format)) {
                            verb.SetValue("MUIVerb", format); verb.SetValue("MultiSelectModel", "Single");
                            using (var cmd = verb.CreateSubKey("command")) cmd.SetValue("", "\"" + Application.ExecutablePath + "\" --to " + format + " \"%1\"");
                        }
                    }
                    using (var verb = key.CreateSubKey(@"shell\zzFerramentas")) {
                        verb.SetValue("MUIVerb", "Abrir conversor / lote…"); verb.SetValue("MultiSelectModel", "Single");
                        using (var cmd = verb.CreateSubKey("command")) cmd.SetValue("", "\"" + Application.ExecutablePath + "\" --queue \"%1\"");
                    }
                }
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        public static void Remove() {
            foreach (string extension in Catalog.Extensions) {
                bool owned = false;
                using (var key = Registry.CurrentUser.OpenSubKey(Root + extension + KeyName)) {
                    if (key != null) owned = string.Equals(key.GetValue("OwnerExecutable") as string, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
                if (owned) Registry.CurrentUser.DeleteSubKeyTree(Root + extension + KeyName, false);
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    sealed class Operation {
        public string Name, Id;
        public Operation(string name, string id) { Name = name; Id = id; }
        public override string ToString() { return Name; }
    }

    sealed partial class MainForm : Form {
        readonly ListView files = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true };
        readonly ComboBox operation = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox target = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox destination = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        readonly TextBox pages = new TextBox { Dock = DockStyle.Fill };
        readonly NumericUpDown quality = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 90, Width = 70 };
        readonly NumericUpDown width = new NumericUpDown { Minimum = 0, Maximum = 16384, Value = 0, Width = 90 };
        readonly CheckBox besideOriginal = new CheckBox { Text = "Salvar junto de cada original", AutoSize = true, Checked = true };
        readonly TextBox log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
        readonly Label status = new Label { Text = "Adicione arquivos ou arraste-os para a janela.", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 4) };
        readonly Label hint = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 6), MaximumSize = new Size(1000, 0) };
        readonly Button convert = new AppButton { Text = "Iniciar", AutoSize = true };
        readonly Button cancel = new AppButton { Text = "Cancelar", AutoSize = true, Enabled = false };
        readonly Button open = new AppButton { Text = "Abrir resultados", AutoSize = true, Enabled = false };
        readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 6, Style = ProgressBarStyle.Blocks };
        readonly List<Control> locked = new List<Control>();
        bool busy; string currentJob, resultsFolder;
        public MainForm(string[] inputs, string preset) { InitializeModernInterface(inputs, preset); }
        static Label Label(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 7, 8, 3) }; }
        static Button Button(string text, Action action) { var button = new Button { Text = text, AutoSize = true }; button.Click += (s, e) => action(); return button; }
        void AddDialog() {
            using (var dialog = new OpenFileDialog { Multiselect = true, CheckFileExists = true, Filter = "Arquivos suportados|" + string.Join(";", Catalog.Extensions.Select(x => "*" + x)) + "|Todos os arquivos|*.*" }) if (dialog.ShowDialog() == DialogResult.OK) AddFiles(dialog.FileNames);
        }
        public void AddFiles(IEnumerable<string> inputs) {
            foreach (string input in inputs) {
                string full;
                try { full = Path.GetFullPath(input); } catch { continue; }
                if (!File.Exists(full)) continue;
                if (!Catalog.Extensions.Contains(Path.GetExtension(full).ToLowerInvariant())) { AddLog("Tipo não suportado: " + Path.GetFileName(full)); continue; }
                if (files.Items.Cast<ListViewItem>().Any(x => string.Equals((string)x.Tag, full, StringComparison.OrdinalIgnoreCase))) continue;
                var item = new ListViewItem(new[] { Path.GetFileName(full), Path.GetExtension(full).ToUpperInvariant(), Path.GetDirectoryName(full) }) { Tag = full };
                files.Items.Add(item);
            }
            RefreshTargets();
        }
        void MoveSelection(int direction) {
            if (files.SelectedItems.Count != 1) { status.Text = "Selecione um arquivo para mudar sua posição."; return; }
            var item = files.SelectedItems[0]; int next = item.Index + direction;
            if (next < 0 || next >= files.Items.Count) return;
            files.Items.Remove(item); files.Items.Insert(next, item); item.Selected = true; item.EnsureVisible();
        }
        string ActionId { get { return operation.SelectedItem == null ? "convert" : ((Operation)operation.SelectedItem).Id; } }
        void RefreshTargets() {
            string selected = target.SelectedItem as string;
            IEnumerable<string> available = Catalog.ImageTargets.Concat(new[] { "PDF" });
            if (files.Items.Count > 0) {
                available = Catalog.Targets(Path.GetExtension((string)files.Items[0].Tag));
                foreach (ListViewItem item in files.Items) available = available.Intersect(Catalog.Targets(Path.GetExtension((string)item.Tag))).ToArray();
            }
            var formats = available.ToArray(); target.Items.Clear(); target.Items.AddRange(formats);
            if (formats.Length > 0) target.SelectedItem = selected != null && formats.Contains(selected) ? selected : formats[0];
            target.Enabled = ActionId == "convert"; pages.Enabled = ActionId != "merge-pdf";
            convert.Enabled = files.Items.Count > 0 && (ActionId != "convert" || formats.Length > 0);
            status.Text = files.Items.Count + " arquivo(s) na fila.";
            UpdateHints();
        }
        void UpdateHints() {
            if (ActionId == "merge-pdf") hint.Text = "A ordem da fila define a ordem das páginas. Aceita PDFs e imagens.";
            else if (ActionId == "ocr") hint.Text = "OCR em português e inglês, com os idiomas já incluídos. Gera TXT; revise o resultado do reconhecimento.";
            else if (ActionId == "text-pdf") hint.Text = "Para PDFs com texto selecionável. Se o documento foi digitalizado, use Reconhecer texto.";
            else if (ActionId == "extract-images") hint.Text = "Salva as imagens incorporadas ao PDF. Para salvar páginas inteiras, converta para PNG.";
            else if (ActionId == "split-pdf") hint.Text = "Cria um PDF para cada página. Deixe o campo de páginas vazio para separar todas.";
            else if (ActionId == "select-pages") hint.Text = "Salva as páginas escolhidas em um PDF. Você também pode reordenar: 2,1,3.";
            else if (target.Items.Count == 0) hint.Text = "Os arquivos não têm um formato de saída em comum. Separe-os em filas diferentes.";
            else hint.Text = "O original será preservado. Arquivos com nomes repetidos recebem um novo nome automaticamente.";
            RefreshModernState();
        }
        void AddLog(string message) {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(AddLog), message); return; }
            log.AppendText(message + Environment.NewLine); status.Text = message.Length > 105 ? message.Substring(0, 102) + "…" : message;
            if (stateTitle != null) {
                stateTitle.Text = message.StartsWith("Concluído:") ? "Tudo pronto!" : message.StartsWith("Falha:") || message.StartsWith("Erro") || message.StartsWith("Concluído com falhas") ? "Confira os detalhes" : message.StartsWith("Cancel") ? "Conversão cancelada" : busy ? "Convertendo seus arquivos…" : "Pronto para converter";
                if (message.StartsWith("Falha:") || message.StartsWith("Concluído com falhas")) { logCard.Visible = true; showDetails.Text = "Ocultar detalhes"; }
            }
        }
        void MenuAction(bool install) {
            try { if (install) ShellMenu.Install(); else ShellMenu.Remove(); AddLog(install ? "Menu atualizado! No Windows 11: Mostrar mais opções → Converter para…" : "Menu removido."); }
            catch (Exception ex) { AddLog("Erro no menu: " + ex.Message); }
        }
        void RequestCancel() {
            if (!busy || currentJob == null) return;
            File.WriteAllText(currentJob + ".cancel", "cancel"); cancel.Enabled = false;
            AddLog("Cancelando… Aguarde o componente encerrar.");
        }
        async Task ConvertNow() {
            if (busy || files.Items.Count == 0) return;
            string[] inputs = files.Items.Cast<ListViewItem>().Select(x => (string)x.Tag).ToArray();
            if (!besideOriginal.Checked && string.IsNullOrWhiteSpace(destination.Text)) { AddLog("Escolha uma pasta de destino."); return; }
            string temp = Path.Combine(Path.GetTempPath(), "ConverterAqui-job-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            currentJob = Path.Combine(temp, "job.json");
            var job = new Dictionary<string, object> { { "inputs", inputs }, { "action", ActionId }, { "target", target.SelectedItem as string ?? "" }, { "output", besideOriginal.Checked ? "" : destination.Text }, { "quality", (int)quality.Value }, { "width", (int)width.Value }, { "pages", pages.Text.Trim() } };
            File.WriteAllText(currentJob, Engine.Json.Serialize(job), new UTF8Encoding(false));
            busy = true; foreach (var control in locked) control.Enabled = false;
            convert.Enabled = false; cancel.Enabled = true; open.Enabled = false; progress.Visible = true; progress.Style = ProgressBarStyle.Marquee;
            log.Clear(); AddLog("Iniciando " + inputs.Length + " arquivo(s)…");
            try {
                await Engine.Run(currentJob, AddLog);
                var result = Engine.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(currentJob + ".result.json", Encoding.UTF8));
                var outputs = ((IEnumerable)result["files"]).Cast<object>().Select(Convert.ToString).ToArray();
                foreach (var output in outputs) AddLog("Salvo: " + output);
                foreach (var error in ((IEnumerable)result["errors"]).Cast<object>()) { var detail = (Dictionary<string, object>)error; AddLog("Falha: " + Convert.ToString(detail["file"]) + " — " + Convert.ToString(detail["message"])); }
                if (outputs.Length > 0) { resultsFolder = Path.GetDirectoryName(outputs[0]); open.Enabled = true; }
                string summary = Convert.ToBoolean(result["cancelled"]) ? "Cancelado. Resultados concluídos foram preservados." : Convert.ToBoolean(result["ok"]) ? "Concluído: " + outputs.Length + " arquivo(s) gerado(s)." : "Concluído com falhas: " + outputs.Length + " resultado(s). Consulte os detalhes.";
                AddLog(summary);
            } catch (Exception ex) { AddLog("Falha: " + ex.Message); }
            finally {
                busy = false; currentJob = null; cancel.Enabled = false; progress.Visible = false; progress.Style = ProgressBarStyle.Blocks;
                foreach (var control in locked) control.Enabled = true;
                target.Enabled = ActionId == "convert"; pages.Enabled = ActionId != "merge-pdf"; convert.Enabled = files.Items.Count > 0 && (ActionId != "convert" || target.Items.Count > 0);
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
