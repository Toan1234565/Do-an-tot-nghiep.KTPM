using Microsoft.Identity.Client;
using QuanLyTaiKhoanNguoiDung.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien
{
    public class NguoiDungListModel
    {
        public int MaNguoiDung { get; set; }
        public string? HoTenNhanVien { get; set; }
        public int? MaDiaChi { get; set; }             
       

        public string? GioiTinh { get; set; }

        public string? TenChucVu { get; set; }
        public string? DonViLamViec { get; set; }
       
        public bool? TrangThai { get; set; }

        public int? MaChucVu { get; set; }
      

    }
}
