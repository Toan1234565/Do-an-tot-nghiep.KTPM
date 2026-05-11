namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe
{
    public class TaiXeDetailModel
    {
        public string SoBangLai { get; set; } = null!;

        public string LoaiBangLai { get; set; } = null!;

        public DateOnly? NgayCapBang { get; set; }

        public DateOnly NgayHetHanBang { get; set; }

        public int? KinhNghiemNam { get; set; }

        public string? TrangThaiHoatDong { get; set; }

        public decimal? DiemUyTin { get; set; }

        public string? AnhBangLaiTruoc { get; set; }

        public string? AnhBangLaiSau { get; set; }
    }
}
