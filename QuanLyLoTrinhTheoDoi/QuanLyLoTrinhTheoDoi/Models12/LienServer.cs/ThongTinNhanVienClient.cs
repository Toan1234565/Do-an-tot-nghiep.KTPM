using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class ThongTinNhanVienClient : INhanVienService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ThongTinNhanVienClient> _logger;

        public ThongTinNhanVienClient(HttpClient httpClient, ILogger<ThongTinNhanVienClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<TenNhanVienModel?> GetTenNhanVienAsync(int maNguoiDung)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://localhost:7022/api/quanlynguoidung/lay-ten-nhan-vien/{maNguoiDung}");

                if (response.IsSuccessStatusCode)
                {
                    // Thử đọc dưới dạng chuỗi trước để kiểm tra nội dung
                    string content = await response.Content.ReadAsStringAsync();

                    // Nếu chuỗi rỗng
                    if (string.IsNullOrWhiteSpace(content)) return null;

                    // Nếu nội dung bắt đầu bằng '{', nó là một JSON Object
                    if (content.Trim().StartsWith("{"))
                    {
                        return JsonSerializer.Deserialize<TenNhanVienModel>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    else
                    {
                        // Nếu nó là một chuỗi tên thuần túy (ví dụ: "Nguyễn Văn A")
                        return new TenNhanVienModel
                        {
                            MaNguoiDung = maNguoiDung,
                            TenNguoiDung = content.Replace("\"", "") // Loại bỏ dấu ngoặc kép nếu có
                        };
                    }
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy nhân viên với mã: {MaNguoiDung}", maNguoiDung);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API nhân viên: {MaNguoiDung}", maNguoiDung);
                return null;
            }
        }
    }
}