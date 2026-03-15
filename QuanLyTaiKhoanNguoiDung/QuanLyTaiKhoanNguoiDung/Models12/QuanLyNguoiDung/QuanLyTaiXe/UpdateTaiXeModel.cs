using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class UpdateTaiXeModel
    {
        public int MaNguoiDung { get; set; }


        public string? TrangThaiMoi { get; set; }


        public virtual NguoiDungModel? MaNguoiDungNavigation { get; set; } 
    }
}
