using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien;
using System.Net.Http.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer.cs
{
   

    public class PhuongTienServiceClient : IPhuongTienServiceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PhuongTienServiceClient> _logger;
        private const string BaseUrl = "https://localhost:7286";

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

        public async Task<bool> CapNhatTrangThaiGanXeAsync(int maPhuongTien, int maCa, bool trangThai = true)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PhuongTienApi");

                // Cập nhật URL theo Endpoint mới đã sửa ở Backend
                // Sử dụng Query String để truyền maCa và trangThai (true = bận, false = giải phóng)
                var url = $"https://localhost:7286/api/quanlyxe/cap-nhat-sau-khi-gan/{maPhuongTien}?maCa={maCa}&trangThai={trangThai}";

                _logger.LogInformation("Đang gọi API cập nhật trạng thái xe {Ma}: Ca {MaCa}, Trạng thái {Status}",
                                        maPhuongTien, maCa, trangThai ? "Bận" : "Trống");

                // Gửi yêu cầu PUT (body null vì tham số nằm trên URL)
                var response = await client.PutAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Cập nhật trạng thái xe {Ma} thành công.", maPhuongTien);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi API ({StatusCode}) khi cập nhật xe {Ma}: {Error}",
                                    response.StatusCode, maPhuongTien, errorContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối Server Phương Tiện khi cập nhật xe {Ma}", maPhuongTien);
                return false;
            }
        }

        
        /// 3. Lấy danh sách xe sẵn sàng điều phối dựa trên khối lượng hàng và mã kho (Mới thêm)
        
        public async Task<List<PhuongTienDTO>?> GetXeSanSangDieuPhoiAsync(double khoiLuongHang, int maKho)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PhuongTienApi");

                // Route dạng [HttpGet("xe-san-sang-dieu-phoi")] nhận tham số từ Query String
                var url = $"{BaseUrl}/api/quanlyxe/xe-san-sang-dieu-phoi?khoiLuongHang={khoiLuongHang}&maKho={maKho}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<PhuongTienDTO>>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi lấy danh sách xe sẵn sàng điều phối. Status: {StatusCode}, Chi tiết: {Error}", response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi danh sách xe sẵn sàng điều phối tại kho {MaKho}", maKho);
                return null;
            }
        }

       
        /// 4. Cập nhật trạng thái xe khi chạy xong chuyến hoặc hoàn thành bảo trì (Mới thêm)
        
        public async Task<UpdateTrangThaiXeResponse?> UpdateTrangThaiXeAsync(int maPhuongTien, UpdateTrangThaiXeDto model)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PhuongTienApi");

                // Route dạng [HttpPost("cap-nhat-trang-thai-xe/{maPhuongTien}")] nhận Body JSON
                var url = $"{BaseUrl}/api/quanlyxe/cap-nhat-trang-thai-xe/{maPhuongTien}";

                var response = await client.PostAsJsonAsync(url, model);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UpdateTrangThaiXeResponse>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi khi cập nhật trạng thái hoạt động xe {MaPhuongTien}. Status: {StatusCode}, Chi tiết: {Error}",
                                    maPhuongTien, response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi thay đổi trạng thái phương tiện {MaPhuongTien}", maPhuongTien);
                return null;
            }
        }

        public async Task<PhuongTienPagedResponse?> GetDanhSachXeTheoKhoKhoiLuongAsync(int? maKho, double? khoiLuongCan, int page = 1)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PhuongTienApi");

                // Tạo URL kèm các Query String phục vụ phân trang và lọc thực tế
                var url = $"{BaseUrl}/api/phuongtien/loc-xe-dieu-phoi?page={page}";
                if (maKho.HasValue) url += $"&maKho={maKho.Value}";
                if (khoiLuongCan.HasValue) url += $"&khoiLuongCan={khoiLuongCan.Value}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // Map kết quả trả về object phân trang bao gồm Data và cấu trúc TotalPages
                    return await response.Content.ReadFromJsonAsync<PhuongTienPagedResponse>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Lỗi khi lọc danh sách xe điều phối phân trang. Status: {StatusCode}, Chi tiết: {Error}", response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API phân trang phương tiện: Kho={MaKho}, KL={KL}, Page={Page}", maKho, khoiLuongCan, page);
                return null;
            }
        }
    }
}
