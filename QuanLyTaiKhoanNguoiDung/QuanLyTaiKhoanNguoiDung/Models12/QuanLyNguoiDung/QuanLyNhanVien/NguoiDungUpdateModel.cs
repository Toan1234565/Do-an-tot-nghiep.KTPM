using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien
{
    public class NguoiDungUpdateModel
    {
      
        public string? HoTenNhanVien { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public int? MaChucVu { get; set; }
      
        public string? BaoHiemXaHoi { get; set; }

        public string? DonViLamViec { get; set; }
    }
}
