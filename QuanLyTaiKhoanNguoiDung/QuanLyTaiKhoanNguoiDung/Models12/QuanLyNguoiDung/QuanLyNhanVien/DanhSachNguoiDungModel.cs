using Microsoft.Identity.Client;
using QuanLyTaiKhoanNguoiDung.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien
{
    public class DanhSachNguoiDungModel
    {
        public int MaNguoiDung { get; set; }
        public string? HoTenNhanVien { get; set; }
        public int? MaDiaChi { get; set; }             
        public DateOnly? NgaySinh { get; set; }

        public string? GioiTinh { get; set; }

        public string? TenChucVu { get; set; }
        public string? DonViLamViec { get; set; }
        public string? NoiSinh { get; set; }
        public bool? TrangThai { get; set; }

    }
}
