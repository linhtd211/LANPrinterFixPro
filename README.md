# LAN Printer Fix Pro v0.1 (prototype)

Ứng dụng WinForms thử nghiệm: kiểm tra Print Spooler, ping, SMB TCP/445; yêu cầu kết nối máy in chia sẻ; khởi động lại Spooler có xác nhận; xuất log. Chưa có bộ máy tự sửa lỗi 0x0000011b/0x00000709, chưa có chức năng tự cập nhật.

## Tạo EXE không cần cài .NET trên máy sử dụng

1. Mở tab **Actions** > **Build portable Windows EXE** > **Run workflow**.
2. Tải artifact `LANPrinterFixPro-win-x64` sau khi build hoàn tất; giải nén để lấy `LANPrinterFixPro.exe`.

Hoặc trên máy có .NET 8 SDK: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist`

**Lưu ý:** Chỉ chạy trên Windows 10/11 64-bit. Ứng dụng chưa ký số, SmartScreen có thể cảnh báo. Chạy quyền quản trị khi cần khởi động lại Spooler. Không tự động thay đổi các chính sách bảo mật Windows. Đây là prototype chưa được thử nghiệm thực tế trên Windows.