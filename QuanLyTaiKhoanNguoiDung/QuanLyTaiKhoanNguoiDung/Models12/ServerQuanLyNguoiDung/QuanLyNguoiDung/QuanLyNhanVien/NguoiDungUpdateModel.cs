using QuanLyTaiKhoanNguoiDung.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien
{
    public class NguoiDungUpdateModel
    {
      
        public string? HoTenNhanVien { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public int? MaChucVu { get; set; }
      
        public string? BaoHiemXaHoi { get; set; }

        public string? DonViLamViec { get; set; }
        public int? MaKho { get; internal set; }
        public virtual ChucVu? MaChucVuNavigation { get; set; }
    }
}
