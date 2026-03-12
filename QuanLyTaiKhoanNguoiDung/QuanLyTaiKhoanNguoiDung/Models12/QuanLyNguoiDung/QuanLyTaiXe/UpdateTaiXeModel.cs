using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class UpdateTaiXeModel
    {
        public int MaNguoiDung { get; set; }

        public string SoBangLai { get; set; } = null!;

        public string LoaiBangLai { get; set; } = null!;

        public DateOnly? NgayCapBang { get; set; }

        public DateOnly NgayHetHanBang { get; set; }

        public int? KinhNghiemNam { get; set; }

        public string? TrangThaiHoatDong { get; set; }

        public decimal? DiemUyTin { get; set; }

        public string? AnhBangLaiTruoc { get; set; }

        public string? AnhBangLaiSau { get; set; }

        public string? BaoHiemXaHoi { get; set; }
        public string? DonViLamViec { get; set; } 
        public string? SoDienThoai { get; set; }
        public int? MaChucVu { get; set; }

        public string? TrangThaiMoi { get; set; }

        public virtual NguoiDungModel? MaNguoiDungNavigation { get; set; } 
    }
}
