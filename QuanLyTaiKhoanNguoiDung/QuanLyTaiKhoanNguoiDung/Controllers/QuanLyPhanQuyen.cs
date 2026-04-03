using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyTaiKhoan;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Security.Claims;
using System.Text;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyPhanQuyen : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyPhanQuyen> _logger;
        private readonly IEmailService _emailService; // Thêm biến này
        public QuanLyPhanQuyen(IHttpClientFactory httpClientFactory, TmdtContext context, ILogger<QuanLyPhanQuyen> logger, IEmailService emailService)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }
        private const string apiBaseUrl = "https://localhost:7022/api/quanlyphanquyen";
        private const string apiBaseTaiKhoan = "https://localhost:7022/api/quanlytaikhoan";
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult DangNhap()
        {
            return View();
        }
        public class LoginResponse
        {
            public int userId { get; set; }
            public string? userName { get; set; }
            public string? fullName { get; set; } // Thêm dòng này để lấy họ tên từ API
            public int? maKho { get; set; } // Thêm dòng này để lấy mã kho từ API
        }

        [HttpPost]
        public async Task<IActionResult> DangNhap(DangNhapModel login)
        {
            if (!ModelState.IsValid) return View(login);

            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseTaiKhoan}/dangnhap";

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(login), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<LoginResponse>(responseString);

                    if (result != null)
                    {
                        // 1. LƯU SESSION (Để dùng chung dữ liệu qua Redis)
                        HttpContext.Session.SetString("MaNguoiDung", result.userId.ToString());
                        HttpContext.Session.SetString("TenDangNhap", result.userName ?? "");
                        HttpContext.Session.SetString("HoTenNhanVien", result.fullName ?? result.userName ?? "Thành viên");
                        HttpContext.Session.SetString("MaKho", result.maKho?.ToString() ?? "");

                        // 2. TẠO CLAIMS VÀ SIGNIN (Để kích hoạt isAuth = true)
                        // Bước này cực kỳ quan trọng để các [Authorize] và User.Identity hoạt động
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, result.fullName ?? result.userName ?? "Thành viên"),
                            new Claim(ClaimTypes.NameIdentifier, result.userId.ToString()),
                            new Claim("Username", result.userName ?? ""),
                            new Claim("MaKho", result.maKho?.ToString() ?? "")

                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true, // Ghi nhớ đăng nhập
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                        };

                        // Đóng dấu Cookie tại Client App
                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties
                        );

                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<dynamic>(responseString);
                        string msg = errorObj?.message ?? "Tài khoản hoặc mật khẩu không chính xác";
                        ModelState.AddModelError(string.Empty, msg);
                    }
                    catch
                    {
                        ModelState.AddModelError(string.Empty, responseString ?? "Lỗi đăng nhập");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Lỗi kết nối hệ thống: " + ex.Message);
            }
            return View(login);
        }

        [HttpGet]
        public async Task<IActionResult> DangXuat()
        {
            // 1. Gọi API đăng xuất để xóa Cookie phía Server API (nếu có)
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseTaiKhoan}/dangxuat";

            try
            {
                // Gọi API với phương thức POST như đã định nghĩa ở QuanLyTaiKhoanAPI
                await client.PostAsync(apiUrl, null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API đăng xuất: {ex.Message}");
            }

            // 2. Xóa Session ở phía Client App
            HttpContext.Session.Clear();
            // Hoặc cụ thể: HttpContext.Session.Remove("TenDangNhap");

            // 3. Chuyển hướng về trang đăng nhập
            return RedirectToAction("DangNhap", "QuanLyPhanQuyen");
        
        }

        // API Vô hiệu hóa tài khoản
        [HttpPut("vohieuhoa/{id}")]
        public async Task<IActionResult> VoHieuHoa(int id, KhoaTaiKhoanRequest request)
        {
            // 1. Tối ưu: Lấy tài khoản kèm thông tin người dùng để gửi mail ngay
            var tk = await _context.TaiKhoans
                .Include(t => t.NguoiDung)
                .FirstOrDefaultAsync(t => t.MaNguoiDung == id);

            if (tk == null) return NotFound(new { message = "Không tìm thấy tài khoản" });
            if (tk.HoatDong == false) return BadRequest(new { message = "Tài khoản đã bị khóa trước đó" });

            // 2. Cập nhật trạng thái
            tk.HoatDong = false;

            try
            {
                await _context.SaveChangesAsync();

                // 3. Gửi email thông báo (Tối ưu: Không dùng await để API phản hồi ngay lập tức)
                if (tk.NguoiDung != null && !string.IsNullOrEmpty(tk.NguoiDung.Email))
                {
                    _ = Task.Run(async () => {
                        try
                        {
                            await _emailService.SendLockAccountEmailAsync(
                                tk.NguoiDung.Email,
                                tk.NguoiDung.HoTenNhanVien ?? tk.TenDangNhap,
                                request.LyDo,
                                true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi gửi mail vô hiệu hóa: {ex.Message}");
                        }
                    });
                }

                return Ok(new { message = "Vô hiệu hóa tài khoản thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi cập nhật dữ liệu", detail = ex.Message });
            }
        }

        

        [HttpGet]
        public async Task<IActionResult> PhanQuyen()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/danhsachchucvu";
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var phanquyen = JsonConvert.DeserializeObject<List<ChucVuModel>>(content);
                    return View(phanquyen);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View(new List<ChucVuModel>());
        }

    }
}
