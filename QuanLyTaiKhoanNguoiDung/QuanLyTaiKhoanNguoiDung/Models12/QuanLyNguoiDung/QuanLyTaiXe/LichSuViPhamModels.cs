using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class LichSuViPhamModels
    {
        public int MaViPham { get; set; }

        public int MaTaiXe { get; set; }

        public DateTime? NgayViPham { get; set; }

        public string LoaiViPham { get; set; } = null!;

        public string? MoTaChiTiet { get; set; }

        public decimal? MucPhat { get; set; }

        public string? HinhThucXuLy { get; set; }

        public string? TrangThaiXuLy { get; set; }

        public int? NguoiLapBienBan { get; set; }

        public virtual QuanLyTaiXeModels MaTaiXeNavigation { get; set; } = null!;

        public virtual NguoiDungModel? NguoiLapBienBanNavigation { get; set; }
    }
}
