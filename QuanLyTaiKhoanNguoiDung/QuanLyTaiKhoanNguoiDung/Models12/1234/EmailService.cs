namespace QuanLyTaiKhoanNguoiDung.Models12._1234
{
    public interface IEmailService
    {
        Task SendEmailAsync(string emailNhan, string tenNhan, string ngayHetHan);
    }

    public class EmailService : IEmailService
    {
        private const string AdminEmail = "nguuentoanbs2k4@gmail.com";
        private const string AdminPassword = "nadf mrjb rxqm jtxx";

        public async Task SendEmailAsync(string emailNhan, string tenNhan, string ngayHetHan)
        {
            var email = new MimeKit.MimeMessage();
            email.From.Add(MimeKit.MailboxAddress.Parse(AdminEmail));
            email.To.Add(MimeKit.MailboxAddress.Parse(emailNhan));
            email.Subject = "[YÊU CẦU] Thực hiện cấp lại Giấy phép lái xe";

            var builder = new MimeKit.BodyBuilder
            {
                HtmlBody = $@"
                        <div style='font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
                            <div style='background-color: #f8d7da; padding: 20px; text-align: center; border-bottom: 2px solid #ee3545;'>
                                <h2 style='color: #721c24; margin: 0;'>⚠️ THÔNG BÁO GIA HẠN BẰNG LÁI</h2>
                            </div>
    
                            <div style='padding: 30px; background-color: #ffffff;'>
                                <p>Xin chào <strong>{tenNhan}</strong>,</p>
        
                                <p>Hệ thống quản lý nhân sự xin thông báo: Giấy phép lái xe (GPLX) của bạn sẽ chính thức hết hạn vào ngày:</p>
        
                                <div style='background-color: #fff3cd; border: 1px solid #ffeeba; padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                    <span style='font-size: 1.2em; color: #856404; font-weight: bold;'>{ngayHetHan}</span>
                                </div>

                                <p>Để đảm bảo tuân thủ quy định pháp luật và không làm gián đoạn kế hoạch vận hành của công ty, bạn vui lòng thực hiện các bước sau:</p>
        
                                <ul style='color: #555;'>
                                    <li>Kiểm tra lại hồ sơ và chuẩn bị các giấy tờ liên quan (CCCD, Giấy khám sức khỏe...).</li>
                                    <li>Liên hệ cơ quan chức năng hoặc truy cập Cổng dịch vụ công để làm thủ tục gia hạn.</li>
                                    <li>Cập nhật ảnh chụp bằng lái mới lên hệ thống ngay sau khi nhận được thẻ mới.</li>
                                </ul>

                                <p style='color: #d73a49; font-weight: 500;'><em>* Lưu ý: Việc điều khiển phương tiện với bằng lái quá hạn có thể dẫn đến các mức phạt nặng từ cơ quan chức năng.</em></p>
                            </div>

                            <div style='background-color: #f9f9f9; padding: 20px; border-top: 1px solid #eeeeee; font-size: 0.85em; color: #777;'>
                                <p style='margin: 0;'>Đây là email tự động từ <strong>Hệ Thống Quản Lý Tài Xế</strong>.</p>
                                <p style='margin: 5px 0 0 0;'>Nếu bạn đã thực hiện gia hạn hoặc có thắc mắc, vui lòng liên hệ Phòng Hành chính - Nhân sự để được hỗ trợ.</p>
                            </div>                                      
                          </div>"
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(AdminEmail, AdminPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
        // Trong IEmailService.cs
        public interface IEmailService
        {
            Task SendEmailAsync(string emailNhan, string tenNhan, string ngayHetHan);
            // Thêm phương thức này:
            Task SendAccountInfoAsync(string emailNhan, string tenNhan, string tenDangNhap, string matKhau);
        }

        // Trong EmailService.cs
        public async Task SendAccountInfoAsync(string emailNhan, string tenNhan, string tenDangNhap, string matKhau)
        {
            var email = new MimeKit.MimeMessage();
            email.From.Add(MimeKit.MailboxAddress.Parse(AdminEmail));
            email.To.Add(MimeKit.MailboxAddress.Parse(emailNhan));
            email.Subject = "[HỆ THỐNG] Thông tin tài khoản nhân viên mới";

            var builder = new MimeKit.BodyBuilder
            {
                HtmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px;'>
                <h2 style='color: #007bff; text-align: center;'>CHÀO MỪNG THÀNH VIÊN MỚI</h2>
                <p>Xin chào <strong>{tenNhan}</strong>,</p>
                <p>Tài khoản hệ thống của bạn đã được khởi tạo thành công với thông tin như sau:</p>
                <div style='background: #f4f4f4; padding: 15px; border-radius: 5px;'>
                    <p><strong>Tên đăng nhập:</strong> {tenDangNhap}</p>
                    <p><strong>Mật khẩu tạm thời:</strong> <span style='color: #d9534f; font-size: 1.2em;'>{matKhau}</span></p>
                </div>
                <p style='color: #555; font-size: 0.9em; margin-top: 20px;'>
                    * Vui lòng đăng nhập và đổi mật khẩu ngay trong lần đầu sử dụng để bảo mật thông tin.
                </p>
            </div>"
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(AdminEmail, AdminPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
