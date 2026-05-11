using System.Net.Http.Json;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer.cs
{
   

    public class PhuongTienServiceClient : IPhuongTienServiceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PhuongTienServiceClient> _logger;

        public PhuongTienServiceClient(IHttpClientFactory httpClientFactory, ILogger<PhuongTienServiceClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<PhuongTienDetailModel?> GetChiTietPhuongTienAsync(int maPhuongTien)
        {
            try
            {
                // Sử dụng Named Client "PhuongTienApi" đã khai báo ở Program.cs
                var client = _httpClientFactory.CreateClient("PhuongTienApi");

                // Gọi đến Endpoint tương ứng trên Server Phương tiện
                var response = await client.GetAsync($"https://localhost:7286/api/quanlyxe/chitietthongtinPT/{maPhuongTien}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PhuongTienDetailModel>();
                }

                _logger.LogWarning("API Phương tiện trả về lỗi: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API phương tiện cho mã {Ma}", maPhuongTien);
                return null;
            }
        }
    }
}