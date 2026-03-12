using System;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class ChiTietTaiXeModels
    {
        // --- Thông tin định danh hệ thống ---
        public int MaNguoiDung { get; set; }
        public string? TenDangNhap { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }

        // --- Thông tin cá nhân (đã giải mã nếu cần) ---
        public string? HoTenTaiXe { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? SoCccd { get; set; }
        public string? NoiSinh { get; set; }
        public int? MaChucVu { get; set; }

        // --- Thông tin bằng lái và nghiệp vụ ---
        public string? SoBangLai { get; set; }
        public string? LoaiBangLai { get; set; }
        public DateOnly? NgayCapBang { get; set; }

        public DateOnly NgayHetHanBang { get; set; }
        public int? KinhNghiemNam { get; set; }
        public string? AnhBangLaiTruoc { get; set; }

        public string? AnhBangLaiSau { get; set; }

        // --- Thông tin tài chính & Bảo hiểm (Dữ liệu nhạy cảm) ---
        public string? SoTaiKhoan { get; set; }
        public string? TenNganHang { get; set; }
        public string? BaoHiemXaHoi { get; set; }

        // --- Chỉ số vận hành (KPI) ---
        public string? TrangThaiHoatDong { get; set; } // Ví dụ: Đang chạy, Sẵn sàng, Nghỉ phép
        public decimal? DiemUyTin { get; set; }
        

        // --- Thông tin đơn vị ---
        public string? DonViLamViec { get; set; }
        public string? TenChucVu { get; set; }
    }
}