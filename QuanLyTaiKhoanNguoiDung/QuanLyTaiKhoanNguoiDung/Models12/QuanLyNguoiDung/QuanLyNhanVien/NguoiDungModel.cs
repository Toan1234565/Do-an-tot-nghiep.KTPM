using QuanLyTaiKhoanNguoiDung.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien
{
    public class NguoiDungModel
    {
        
        public string? TenDangNhap { get; set; }

       
        public string? MatKhau { get; set; }

        public int MaNguoiDung { get; set; }

        
        public string? HoTenNhanVien { get; set; }


        public int ? MaDiaChi { get; set; }


        public string? Email { get; set; }


        public string? SoDienThoai { get; set; }

        public int? MaChucVu { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public string? GioiTinh { get; set; }

        public string? TenChucVu { get; set; }


        public string? SoCccd { get; set; }

        public string? NoiSinh { get; set; }
       
        public string? SoTaiKhoan { get; set; }
        
        public string? TenNganHang { get; set; }
     
        public string? BaoHiemXaHoi { get; set; }

        public string? DonViLamViec { get; set; }

        public int? MaKho { get; set; }

        public virtual TaiKhoan? MaNguoiDungNavigation { get; set; } 

        // Trong file TaiKhoan.cs hoặc NguoiDung.cs
        public virtual ChucVu? MaChucVuNavigation { get; set; }
       
    }
}
