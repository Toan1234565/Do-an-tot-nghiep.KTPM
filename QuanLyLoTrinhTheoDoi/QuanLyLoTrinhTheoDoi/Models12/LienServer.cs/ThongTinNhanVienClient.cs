using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.TaiXe;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class ThongTinNhanVienClient : INhanVienService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ThongTinNhanVienClient> _logger;
        private readonly string _baseUrlNguoiDung = "https://localhost:7022";

        public ThongTinNhanVienClient(HttpClient httpClient, ILogger<ThongTinNhanVienClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        public async Task<TenNhanVienModel?> GetTenNhanVienAsync(int maNguoiDung)
        {
            try
            {
                // Gọi API dạng GET để lấy tên nhân viên qua Route công khai
                var response = await _httpClient.GetAsync($"https://localhost:7022/api/quanlynguoidung/lay-ten-nhan-vien/{maNguoiDung}");

                if (response.IsSuccessStatusCode)
                {
                    // Đọc trực tiếp từ Stream và ép kiểu sang TenNhanVienModel để tối ưu hiệu năng
                    return await response.Content.ReadFromJsonAsync<TenNhanVienModel>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy thông tin nhân viên với mã người dùng: {maNguoiDung} từ API.", maNguoiDung);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API Lấy chi tiết nhân viên trả về lỗi. Status: {Status}, Chi tiết: {Error}", response.StatusCode, errorContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối hệ thống khi lấy chi tiết thông tin nhân viên cho mã: {maNguoiDung}", maNguoiDung);
                return null;
            }
        }


        // 2. KIỂM TRA TÀI XẾ TỒN TẠI
        public async Task<bool> KiemTraTaiXeTonTaiAsync(int maNguoiDung)
        {
            try
            {
                // Gọi lại API lấy tên hoặc API check tồn tại bên server 7022
                var response = await _httpClient.GetAsync($"{_baseUrlNguoiDung}/api/quanlynguoidung/lay-ten-nhan-vien/{maNguoiDung}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra tồn tại tài xế: {MaNguoiDung}", maNguoiDung);
                return false;
            }
        }

        // 3. CẬP NHẬT TRẠNG THÁI GÁN TÀI XẾ (TRUE/FALSE)
        public async Task<bool> CapNhatTrangThaiTaiXeAsync(int maNguoiDung, bool trangThai)
        {
            try
            {
                // Gọi API PUT: api/quanlytaixe/cap-nhat-trang-thai-gan/{id}?trangThai=true
                var response = await _httpClient.PutAsync(
                    $"{_baseUrlNguoiDung}/api/quanlytaixe/cap-nhat-trang-thai-gan/{maNguoiDung}?trangThai={trangThai}",
                    null // Nội dung body trống vì dùng Query String
                );

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Đã cập nhật trạng thái gán cho tài xế {ID} thành {Status}", maNguoiDung, trangThai);
                    return true;
                }

                _logger.LogWarning("Không thể cập nhật trạng thái tài xế {ID}. Status code: {Code}", maNguoiDung, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API cập nhật trạng thái tài xế: {MaNguoiDung}", maNguoiDung);
                return false;
            }
        }

        
        /// 4. Gọi API cập nhật trạng thái hoạt động trực tiếp của tài xế (Mới thêm)
       
        public async Task<UpdateTrangThaiTaiXeResponse?> UpdateTrangThaiTaiXeAsync(UpdateTaiXeTrangTai model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TrangThaiMoi))
            {
                _logger.LogWarning("Yêu cầu cập nhật trạng thái tài xế không hợp lệ.");
                return null;
            }

            try
            {
                // Gọi API dạng POST truyền đối tượng JSON qua Body [HttpPost("cap-nhat-trang-thai")]
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrlNguoiDung}/api/quanlytaixe/cap-nhat-trang-thai", model);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UpdateTrangThaiTaiXeResponse>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi khi cập nhật trạng thái tài xế {ID}. Status: {Code}, Chi tiết: {Error}",
                                    model.MaNguoiDung, response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API cập nhật trạng thái tài xế {ID}", model.MaNguoiDung);
                return null;
            }
        }

        
        /// 5. Gọi API kiểm tra xem tài xế hiện tại có đi làm không và tính sẵn sàng điều phối (Mới thêm)
        
        public async Task<DriverStatusResponseDto?> CheckDriverStatusAsync(int maNguoiDung)
        {
            try
            {
                // Gọi API dạng GET truyền mã người dùng qua Route [HttpGet("check-status/{id}")]
                var response = await _httpClient.GetAsync($"{_baseUrlNguoiDung}/api/quanlytaixe/check-status/{maNguoiDung}");

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Đọc gói dữ liệu phản hồi (Bao gồm cả trường hợp NotFound vì Controller bọc Object báo lỗi cụ thể)
                    return await response.Content.ReadFromJsonAsync<DriverStatusResponseDto>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi khi kiểm tra trạng thái làm việc của tài xế {ID}. Status: {Code}, Chi tiết: {Error}",
                                    maNguoiDung, response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API kiểm tra trạng thái làm việc tài xế: {ID}", maNguoiDung);
                return null;
            }
        }
        
        /// 6. Gọi API lấy toàn bộ danh sách ca làm việc của hệ thống (Mới thêm)
       
        public async Task<List<CaLamViecModels>?> GetDanhSachCaLamAsync()
        {
            try
            {
                // Cấu hình URL endpoint dựa trên Prefix của Controller tương ứng trên Server 7022
                // Ví dụ ngầm định: api/quanlycalam/DanhSachCaLam
                var response = await _httpClient.GetAsync($"{_baseUrlNguoiDung}/api/quanlycalam/DanhSachCaLam");

                if (response.IsSuccessStatusCode)
                {
                    // Giải mã JSON thành Object phản hồi bao gồm cả cờ Success và cụm mảng Data
                    var result = await response.Content.ReadFromJsonAsync<CaLamViecResponseApiResponse>();

                    if (result != null && result.Success)
                    {
                        return result.Data;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Lấy danh sách ca làm việc trả về lỗi. Status: {Code}, Chi tiết: {Error}", response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lấy danh sách ca làm việc.");
                return null;
            }
        }
    }
}