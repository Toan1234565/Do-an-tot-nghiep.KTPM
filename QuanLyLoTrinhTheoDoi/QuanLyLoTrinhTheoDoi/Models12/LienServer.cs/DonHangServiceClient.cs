using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang;
using System.Net.Http.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class DonHangServiceClient : IDonHangService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DonHangServiceClient> _logger;
        private const string BaseUrl = "https://localhost:7264";

        public DonHangServiceClient(HttpClient httpClient, ILogger<DonHangServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ChiTietDonHangLoTrinhModel?> GetChiTietDonHangAsync(int madonhang)
        {
            try
            {
                // Gọi API với route bạn đã định nghĩa ở Controller
                // Lưu ý: Tên endpoint "chi-tiet-don-hang/{madonhang}"
                var response = await _httpClient.GetAsync($"https://localhost:7264/api/quanlydonhang/chi-tiet-don-hang/{madonhang}");

                if (response.IsSuccessStatusCode)
                {
                    // Giải mã JSON trả về thành Object
                    return await response.Content.ReadFromJsonAsync<ChiTietDonHangLoTrinhModel>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy đơn hàng mã {MaDonHang} từ API.", madonhang);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy chi tiết đơn hàng {MaDonHang}", madonhang);
                return null;
            }
        }

        public async Task<ClusterResponseModel?> TuDongGomNhomDonHangAsync(ClusterRequest request)
        {
            try
            {
                // Gọi API sử dụng phương thức POST và truyền body dạng JSON
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/quanlydonhang/cho-dieu-phoi", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ClusterResponseModel>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("API trả về: Không có đơn hàng nào cần thu gom.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Lỗi khi gom nhóm đơn hàng. Status: {Status}, Chi tiết: {Error}", response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi thực hiện gọi API gom nhóm đơn hàng.");
                return null;
            }
        }
        public async Task<UpdateMultiStatusResponseModel?> CapNhatTrangThaiNhieuDonHangAsync(UpdateMultiStatusRequest request)
        {
            try
            {
                // Gọi API sử dụng phương thức PUT và truyền body dạng JSON
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/quanlydonhang/cap-nhat-trang-thai-nhieu", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UpdateMultiStatusResponseModel>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi khi cập nhật trạng thái hàng loạt. Status: {Status}, Chi tiết: {Error}", response.StatusCode, errorContent);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API cập nhật trạng thái nhiều đơn hàng.");
                return null;
            }
        }
        public async Task<DonHangViTriDto?> GetViTriHienTaiDonHangAsync(int maDonHang)
        {
            try
            {
                // Gọi API GET: /api/quanlydonhang/vi-tri-hien-tai/{maDonHang}
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/quanlydonhang/vi-tri-hien-tai/{maDonHang}");

                if (response.IsSuccessStatusCode)
                {
                    // Giải mã JSON trả về thành Object DTO vị trí đơn hàng
                    return await response.Content.ReadFromJsonAsync<DonHangViTriDto>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy thông tin vị trí của đơn hàng mã {MaDonHang} từ API hệ thống.", maDonHang);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Lỗi khi truy vấn vị trí đơn hàng {MaDonHang}. Status: {Status}, Chi tiết: {Error}",
                        maDonHang, response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy vị trí hiện tại của đơn hàng {MaDonHang}", maDonHang);
                return null;
            }
        }
        // --- THÊM TOÀN BỘ HÀM NÀY VÀO TRONG CLASS DONHANGSERVICECLIENT ---
        public async Task<ThongTinGiaoHangDto?> GetThongTinGiaoHangAsync(int? maDonHang)
        {
            if (maDonHang == null || maDonHang == 0) return null;

            try
            {
                // Gọi API GET tới endpoint quản lý đơn hàng để lấy thông tin vùng/miền giao hàng
                // (Lưu ý: Thay đổi route "thong-tin-giao-hang" đúng với router thực tế trên API Server của bạn nếu cần)
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/quanlydonhang/thong-tin-giao-hang/{maDonHang}");

                if (response.IsSuccessStatusCode)
                {
                    // Giải mã JSON trả về thành Object DTO thông tin giao hàng
                    return await response.Content.ReadFromJsonAsync<ThongTinGiaoHangDto>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy thông tin giao hàng của đơn hàng mã {MaDonHang} từ API.", maDonHang);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Lỗi khi lấy thông tin giao hàng đơn {MaDonHang}. Status: {Status}, Chi tiết: {Error}",
                        maDonHang, response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lấy thông tin giao hàng cho đơn hàng {MaDonHang}", maDonHang);
                return null;
            }
        }
    }
}