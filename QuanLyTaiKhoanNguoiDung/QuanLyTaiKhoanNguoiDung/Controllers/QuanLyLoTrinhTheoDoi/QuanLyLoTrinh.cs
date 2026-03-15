using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuanLyLoTrinhTheoDoi.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi
{
    public class QuanLyLoTrinh : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLoTrinh> _logger;

        // Cấu hình URL tập trung - Port 7097 là Server Lộ trình
        private const string ApiBaseUrl = "https://localhost:7097/api/dieuphoilotrinh";
        private const string ApiBaseUrlV2 = "https://localhost:7022/api/quanlytaixe";
        private const string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        private const string apiDonHang = "https://localhost:7264/api/quanlydonhang";
        private const string apiKhachHang = "https://localhost:7149/api/quanlykhachhang";
        public QuanLyLoTrinh(IHttpClientFactory httpClientFactory, ILogger<QuanLyLoTrinh> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }


        public async Task<IActionResult> QuanLyLoTrinhTheoDoi(
             DateTime? batDau,
             DateTime? ketThuc,
             string trangThai = "Chờ khởi hành",
             int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var dsLoTrinh = new List<LoTrinhModels>();

            // 1. Xây dựng Query String
            var queryParams = new Dictionary<string, string?>
            {
                ["trangThai"] = trangThai,
                ["page"] = page.ToString(),
                ["pageSize"] = "10"
            };
            if (batDau.HasValue) queryParams["batDau"] = batDau.Value.ToString("yyyy-MM-dd");
            if (ketThuc.HasValue) queryParams["ketThuc"] = ketThuc.Value.ToString("yyyy-MM-dd");

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{ApiBaseUrl}/danhsachlotrinh", queryParams);

            try
            {
                // 2. Gọi API lấy danh sách lộ trình
                var response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Không thể kết nối danh sách lộ trình.";
                    return View(dsLoTrinh);
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonResult = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(content);

                if (jsonResult != null)
                {
                    // Gán thông tin phân trang
                    ViewBag.CurrentPage = jsonResult["currentPage"]?.Value<int>() ?? 1;
                    ViewBag.TotalPages = jsonResult["totalPages"]?.Value<int>() ?? 1;
                    ViewBag.TotalItems = jsonResult["totalItems"]?.Value<int>() ?? 0;

                    // Lấy mảng dữ liệu (Giả định API trả về mảng trong field "items" hoặc "data")
                    var itemsToken = jsonResult["items"] ?? jsonResult["data"];
                    if (itemsToken != null)
                    {
                        dsLoTrinh = itemsToken.ToObject<List<LoTrinhModels>>();
                    }
                }

                // 3. Tối ưu: Lấy tên tài xế cho TỪNG lộ trình trong danh sách
                if (dsLoTrinh != null && dsLoTrinh.Any())
                {
                    // Hàm local để lấy tên tài xế
                    async Task<string> GetTenTaiXe(int? maTx)
                    {
                        if (!maTx.HasValue) return "Chưa phân công";
                        try
                        {
                            var res = await client.GetAsync($"{ApiBaseUrlV2}/lay-ten-tai-xe/{maTx}");
                            if (res.IsSuccessStatusCode)
                            {
                                var json = await res.Content.ReadAsStringAsync();
                                var data = JsonConvert.DeserializeObject<TenTaiXeLoTrinhModels>(json);
                                return data?.TenTaiXeThucHien ?? "N/A"; // Giả định field tên là TenTaiXe
                            }
                        }
                        catch { }
                        return "Lỗi lấy tên";
                    }

                    // Chạy async để cập nhật tên cho toàn bộ danh sách
                    var tasks = dsLoTrinh.Select(async item =>
                    {
                        if (item.MaTaiXeChinh.HasValue)
                            item.TenTaiXeChinh = await GetTenTaiXe(item.MaTaiXeChinh);

                        if (item.MaTaiXePhu.HasValue)
                            item.TenTaiXePhu = await GetTenTaiXe(item.MaTaiXePhu);
                    });

                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi hệ thống: " + ex.Message;
            }

            // Gán dữ liệu tìm kiếm hiện tại lên View
            ViewBag.CurrentTrangThai = trangThai;
            ViewBag.CurrentBatDau = batDau?.ToString("yyyy-MM-dd");
            ViewBag.CurrentKetThuc = ketThuc?.ToString("yyyy-MM-dd");

            return View(dsLoTrinh);
        }

        public async Task<IActionResult> ChiTietLoTrinhTheoDoi(int maLoTrinh)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Sử dụng ConcurrentDictionary để an toàn khi ghi dữ liệu từ nhiều luồng (Parallel)
            var dictDiaChi = new ConcurrentDictionary<int, DiaChiModel>();
            var dictDonHang = new ConcurrentDictionary<int, ChiTietDonHangViewModel>();

            try
            {
                // --- BƯỚC 1: LẤY DỮ LIỆU GỐC (LỘ TRÌNH) ---
                var response = await client.GetAsync($"{ApiBaseUrl}/chi-tiet-lo-trinh/{maLoTrinh}");
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Không tìm thấy dữ liệu lộ trình từ Server API.";
                    return View(null);
                }

                var content = await response.Content.ReadAsStringAsync();
                var ctLoTrinh = JsonConvert.DeserializeObject<LoTrinhModels>(content);

                if (ctLoTrinh == null)
                {
                    ViewBag.Error = "Dữ liệu lộ trình trống.";
                    return View(null);
                }               
                // --- BƯỚC 2: ĐỊNH NGHĨA CÁC LOCAL FUNCTIONS ĐỂ FETCH DỮ LIỆU PHỤ ---

                async Task<TenTaiXeLoTrinhModels> FetchTaiXeAsync(int maTx)
                {
                    try
                    {
                        var res = await client.GetAsync($"{ApiBaseUrlV2}/lay-ten-tai-xe/{maTx}");
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            return JsonConvert.DeserializeObject<TenTaiXeLoTrinhModels>(json);
                        }
                    }
                    catch (Exception ex) { _logger.LogError($"Lỗi lấy tài xế {maTx}: {ex.Message}"); }
                    return new TenTaiXeLoTrinhModels { TenTaiXeThucHien = "N/A" };
                }

                async Task FetchChiTietDiaChiAsync(int maDiaChi)
                {
                    if (maDiaChi <= 0 || dictDiaChi.ContainsKey(maDiaChi)) return;
                    try
                    {
                        var res = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{maDiaChi}");
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            var data = JsonConvert.DeserializeObject<DiaChiModel>(json);
                            if (data != null) dictDiaChi.TryAdd(maDiaChi, data);
                        }
                    }
                    catch (Exception ex) { _logger.LogError($"Lỗi lấy địa chỉ {maDiaChi}: {ex.Message}"); }
                }

                // --- Bước 2: Sửa lại hàm Fetch đơn hàng ---
                async Task FetchChiTietDonHangAsync(int maDonHang)
                {
                    if (maDonHang <= 0 || dictDonHang.ContainsKey(maDonHang)) return;
                    try
                    {
                        // Chú ý: API của bạn trả về object ChiTietDonHang (gồm TenNguoiNhan, SdtNguoiNhan, ...)
                        var res = await client.GetAsync($"{apiDonHang}/chi-tiet-don-hang/{maDonHang}");
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            // Bạn nên tạo/đổi class nhận dữ liệu cho khớp với API (TenNguoiNhan, SdtNguoiNhan)
                            var data = JsonConvert.DeserializeObject<ChiTietDonHangViewModel>(json);
                            if (data != null) dictDonHang.TryAdd(maDonHang, data);
                        }
                    }
                    catch (Exception ex) { _logger.LogError($"Lỗi lấy đơn hàng {maDonHang}: {ex.Message}"); }
                }
                
                // --- BƯỚC 3: THỰC THI SONG SONG (PARALLEL EXECUTION) ---
                var allTasks = new List<Task>();
                if (ctLoTrinh.ChiTietLoTrinhKienHangs != null)
                {
                    var uniqueDonHangIds = ctLoTrinh.ChiTietLoTrinhKienHangs
                        .Where(c => c.MaDonHang.HasValue && c.MaDonHang > 0)
                        .Select(c => c.MaDonHang.Value)
                        .Distinct();

                    foreach (var id in uniqueDonHangIds)
                        allTasks.Add(FetchChiTietDonHangAsync(id)); // Thêm lại dòng này
                }

                

                // 1. Task lấy tên tài xế (Chính & Phụ)
                var taskTxChinh = ctLoTrinh.MaTaiXeChinh.HasValue
                    ? FetchTaiXeAsync(ctLoTrinh.MaTaiXeChinh.Value)
                    : Task.FromResult<TenTaiXeLoTrinhModels>(null);

                var taskTxPhu = ctLoTrinh.MaTaiXePhu.HasValue
                    ? FetchTaiXeAsync(ctLoTrinh.MaTaiXePhu.Value)
                    : Task.FromResult<TenTaiXeLoTrinhModels>(null);

                allTasks.Add(taskTxChinh);
                allTasks.Add(taskTxPhu);

                // 2. Task lấy danh sách địa chỉ duy nhất
                if (ctLoTrinh.DiemDungs != null)
                {
                    var uniqueDiaChiIds = ctLoTrinh.DiemDungs
                        .Select(d => d.MaDiaChi)
                        .Where(id => id > 0)
                        .Distinct();

                    foreach (var id in uniqueDiaChiIds)
                        allTasks.Add(FetchChiTietDiaChiAsync(id));
                }

                //// 3. Task lấy danh sách đơn hàng duy nhất
                //if (ctLoTrinh.ChiTietLoTrinhKienHangs != null)
                //{
                //    var uniqueDonHangIds = ctLoTrinh.ChiTietLoTrinhKienHangs
                //        .Where(c => c.MaDonHang.HasValue && c.MaDonHang > 0)
                //        .Select(c => c.MaDonHang.Value)
                //        .Distinct();

                //    foreach (var id in uniqueDonHangIds)
                //        allTasks.Add(FetchChiTietDonHangAsync(id));
                //}

                // Chờ tất cả các luồng hoàn thành
                await Task.WhenAll(allTasks);

                // --- BƯỚC 4: GÁN DỮ LIỆU RA VIEW ---
                ViewBag.LayTenTaiXe = await taskTxChinh; // Kết quả đã có sẵn sau WhenAll
                ViewBag.LayTenTaiXePhu = await taskTxPhu;
                ViewBag.DictionaryDiaChi = dictDiaChi;
                ViewBag.DictionaryDonHang = dictDonHang;

                return View(ctLoTrinh);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng tại ChiTietLoTrinhTheoDoi");
                ViewBag.Error = "Đã xảy ra lỗi hệ thống: " + ex.Message;
                return View(null);
            }
        }



        // Hàm bổ trợ gọi API an toàn: Tự catch lỗi để không làm sập luồng chính
        private async Task<T?> GetApiDataAsync<T>(HttpClient client, string url, string apiName) where T : class
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (typeof(T) == typeof(Newtonsoft.Json.Linq.JObject))
                        return Newtonsoft.Json.Linq.JObject.Parse(content) as T;

                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
                }
                _logger.LogWarning($"{apiName} trả về mã lỗi: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi {ApiName} tại {Url}", apiName, url);
            }
            return null;
        }
    }
}