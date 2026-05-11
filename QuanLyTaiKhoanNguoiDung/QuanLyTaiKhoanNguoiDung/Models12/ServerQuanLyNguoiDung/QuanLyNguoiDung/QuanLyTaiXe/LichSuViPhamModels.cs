using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe
{
    public class LichSuViPhamModels
    {
       

        public int MaTaiXe { get; set; }

        public DateTime? NgayViPham { get; set; }

        public string LoaiViPham { get; set; } = null!;

        public string? MoTaChiTiet { get; set; }

        public decimal? MucPhat { get; set; }

        public string? HinhThucXuLy { get; set; }

        public string? TrangThaiXuLy { get; set; }

        public int? NguoiLapBienBan { get; set; }
      
    }
}
