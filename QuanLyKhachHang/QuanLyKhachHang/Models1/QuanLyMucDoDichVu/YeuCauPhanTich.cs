using QuanLyKhachHang.Models1.CauHinhTichDiem;

namespace QuanLyKhachHang.Models1.QuanLyMucDoDichVu
{
    public class YeuCauPhanTich
    {
        public double LatGui { get; set; }
        public double LonGui { get; set; }
        public double LatNhan { get; set; }
        public double LonNhan { get; set; } // Trường bị thiếu bạn đang gặp lỗi
        public double KhoiLuong { get; set; }
        public double TheTich { get; set; }
    }
    // Lớp kết quả trả về (Giữ nguyên của bạn)
    public class FlightServiceCalculationResult
    {
        public string? SanBayGui { get; set; }
        public string? SanBayNhan { get; set; }
        public string? MaChuyenBay { get; set; }
        public DateTime? GioKhoiHanh { get; set; }

        public double KmToiSanBayGui { get; set; }
        public double KmTuSanBayNhan { get; set; }

        public double ThoiGiaThoiGianDuongBo_t1_t2 { get; set; }
        public double ThoiGianThuTuc_t3_t5 { get; set; }
        public double ThoiGianBay_t4 { get; set; }
        public double ThoiGianChoChuyenBay_t6 { get; set; }
        public double ThoiGianRuiRo { get; set; }

        public double TongThoiGianDuKien { get; set; } 

        public List<ServiceFeeDetail>? DanhSachDichVu { get; set; }
    }

    public class ServiceFeeDetail
    {
        public int MaDichVu { get; set; }
        public string? TenDichVu { get; set; }
        public double HeSo { get; set; }
        public decimal PhiHangKhong { get; set; }
    }
}