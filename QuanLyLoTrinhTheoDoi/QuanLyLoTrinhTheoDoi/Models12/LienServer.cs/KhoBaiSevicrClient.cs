using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class KhoBaiSevicrClient : IKhoBaiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<KhoBaiSevicrClient> _logger;

        // Định nghĩa BaseUrl tới Server chứa API của bạn
        private const string BaseUrl = "https://localhost:7286"; // Cấu hình lại đúng cổng của bạn nếu cần

        public KhoBaiSevicrClient(HttpClient httpClient, ILogger<KhoBaiSevicrClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Gọi API tìm kiếm kho bãi tối ưu theo lô địa chỉ khách hàng (Ưu tiên H3 -> Haversine)
        /// </summary>
        public async Task<Dictionary<int, KhoTimDuocDto>?> TimKhoTheoLoAsync(BatchKhoRequest request)
        {
            if (request == null || request.MaDiaChis == null || !request.MaDiaChis.Any())
            {
                _logger.LogWarning("Yêu cầu tìm kho theo lô không hợp lệ: Danh sách mã địa chỉ trống.");
                return null;
            }

            try
            {
                // Giả định Route trên Controller của bạn nằm trong Route chung dạng: api/quanlykhobai hoặc tương tự.
                // Ở đây cấu hình route khớp với [HttpPost("tim-kho-theo-lo")]
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/quanlykhobai/tim-kho-theo-lo", request);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc dữ liệu JSON ánh xạ thành Dictionary với Key là MaDiaChi (int) và Value là dữ liệu Kho tìm được
                    return await response.Content.ReadFromJsonAsync<Dictionary<int, KhoTimDuocDto>>();
                }

                // Xử lý khi API trả lỗi
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Tìm kho theo lô trả về lỗi. Status: {Status}, Chi tiết: {Error}", response.StatusCode, errorContent);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API tìm kho theo lô.");
                return null;
            }
        }

        /// <summary>
        /// 2. Gọi API lấy thông tin chi tiết một kho bãi theo mã kho (Mới thêm)
        /// </summary>
        public async Task<KhoBaiDetailModel?> GetChiTietKhoBaiAsync(int maKho)
        {
            try
            {
                // Gọi API dạng GET với mã kho truyền trực tiếp qua Route: [HttpGet("chitietkhobai/{maKho}")]
                // Đảm bảo phần prefix route (/api/quanlykhobai) khớp với cấu hình Route chung trên Controller của bạn
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/quanlykhobai/chitietkhobai/{maKho}");

                if (response.IsSuccessStatusCode)
                {
                    // Đọc JSON phản hồi và chuyển đổi thành Object KhoBaiDetailModel
                    return await response.Content.ReadFromJsonAsync<KhoBaiDetailModel>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy thông tin kho bãi với mã kho: {MaKho} từ API.", maKho);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API Lấy chi tiết kho bãi trả về lỗi. Status: {Status}, Chi tiết: {Error}", response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối hệ thống khi lấy chi tiết thông tin kho bãi cho mã: {MaKho}", maKho);
                return null;
            }
        }
    }
}