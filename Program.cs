using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;

namespace LANPrinterFixPro;

internal static class Program
{
    [STAThread]
    static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}

public sealed class MainForm : Form
{
    readonly TextBox host = new() { PlaceholderText = "Tên máy hoặc IP máy chia sẻ (ví dụ: MAY-KETOAN)", Dock = DockStyle.Fill };
    readonly TextBox printer = new() { PlaceholderText = "Tên máy in chia sẻ (không bắt buộc)", Dock = DockStyle.Fill };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
    readonly Button scan = new() { Text = "Kiểm tra kết nối", AutoSize = true };
    readonly Button spool = new() { Text = "Khởi động lại Print Spooler", AutoSize = true };
    readonly Button connect = new() { Text = "Kết nối máy in", AutoSize = true };
    readonly Button export = new() { Text = "Xuất nhật ký", AutoSize = true };

    public MainForm()
    {
        Text = "LAN Printer Fix Pro — Bản thử nghiệm 0.1";
        Width = 850; Height = 630; MinimumSize = new Size(660, 480);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 6, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.Controls.Add(new Label { Text = "LAN PRINTER FIX PRO", Font = new Font("Segoe UI", 18, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(host, 0, 1); layout.Controls.Add(printer, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false };
        buttons.Controls.AddRange(new Control[] { scan, spool, connect, export });
        layout.Controls.Add(buttons, 0, 3); layout.Controls.Add(log, 0, 4);
        layout.Controls.Add(new Label { Text = "Không tự động thay đổi Firewall, RPC, SMB1 hoặc Registry. Thao tác sửa cần xác nhận.", Dock = DockStyle.Fill }, 0, 5);
        Controls.Add(layout);
        scan.Click += async (_, _) => await CheckAsync();
        spool.Click += async (_, _) => await RestartSpoolerAsync();
        connect.Click += async (_, _) => await ConnectAsync();
        export.Click += (_, _) => ExportLog();
        Write("Sẵn sàng. Nhập tên máy chủ và chọn Kiểm tra kết nối.");
    }

    void Write(string message) => log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    async Task CheckAsync()
    {
        scan.Enabled = false;
        try
        {
            Write($"Máy hiện tại: {Environment.MachineName}; Windows: {Environment.OSVersion}");
            var service = await Task.Run(() => Run("sc.exe", "query spooler"));
            Write(service.output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ? "Print Spooler: đang chạy." : "Print Spooler: cần kiểm tra. " + service.output.Trim());
            var name = host.Text.Trim();
            if (string.IsNullOrEmpty(name)) { Write("Nhập tên/IP máy chia sẻ để kiểm tra mạng."); return; }
            if (!ValidHost(name)) { Write("Tên/IP máy chủ không hợp lệ."); return; }
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(name, 2500);
                Write($"Ping {name}: {reply.Status} (Ping bị chặn không đồng nghĩa máy chủ không hoạt động).");
            }
            catch (Exception ex) { Write("Ping: " + ex.Message); }
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var task = tcp.ConnectAsync(name, 445);
                var completed = await Task.WhenAny(task, Task.Delay(3000));
                Write(completed == task && tcp.Connected ? "Cổng SMB TCP/445: truy cập được." : "Cổng SMB TCP/445: không kết nối được.");
                if (completed == task) await task;
            }
            catch (Exception ex) { Write("SMB: " + ex.Message); }
            Write("Nếu SMB hoạt động nhưng vẫn không in được, cần kiểm tra quyền chia sẻ và driver trên máy chủ.");
        }
        finally { scan.Enabled = true; }
    }

    async Task RestartSpoolerAsync()
    {
        if (MessageBox.Show("Khởi động lại Print Spooler trên MÁY NÀY? Các tác vụ in đang xử lý có thể bị gián đoạn.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        spool.Enabled = false;
        try
        {
            var result = await Task.Run(() => Run("powershell.exe", "-NoProfile -NonInteractive -Command \"Restart-Service -Name Spooler -ErrorAction Stop\""));
            Write(result.code == 0 ? "Đã yêu cầu khởi động lại Print Spooler." : "Không thể khởi động lại: " + result.output.Trim() + " (có thể cần chạy với quyền Administrator)");
        }
        finally { spool.Enabled = true; }
    }

    async Task ConnectAsync()
    {
        var name = host.Text.Trim(); var share = printer.Text.Trim();
        if (!ValidHost(name) || !ValidShare(share)) { Write("Cần tên máy chủ và tên chia sẻ hợp lệ."); return; }
        var path = $@"\\{name}\{share}";
        if (MessageBox.Show($"Thêm máy in chia sẻ {path} vào Windows? Chỉ thực hiện với máy chủ đáng tin cậy.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        connect.Enabled = false;
        try
        {
            var result = await Task.Run(() => Run("rundll32.exe", $"printui.dll,PrintUIEntry /in /n\"{path}\""));
            Write(result.code == 0 ? $"Đã gửi yêu cầu kết nối {path}. Kiểm tra trong Printers & scanners." : "Kết nối thất bại, mã: " + result.code + ". " + result.output.Trim());
        }
        finally { connect.Enabled = true; }
    }

    static bool ValidHost(string value) => value.Length is > 0 and <= 253 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or ':');
    static bool ValidShare(string value) => value.Length is > 0 and <= 100 && !value.Any(c => c is '\\' or '/' or '"' or '\'' or '&' or '|' or '<' or '>' or '\n' or '\r');
    static (int code, string output) Run(string file, string args)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd(); var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        if (!p.HasExited) { p.Kill(); return (-1, "Quá thời gian thực thi."); }
        return (p.ExitCode, stdout + stderr);
    }
    void ExportLog()
    {
        using var dialog = new SaveFileDialog { Filter = "Text files|*.txt", FileName = $"LANPrinterFix-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (dialog.ShowDialog() == DialogResult.OK) { File.WriteAllText(dialog.FileName, log.Text, Encoding.UTF8); Write("Đã xuất nhật ký."); }
    }
}