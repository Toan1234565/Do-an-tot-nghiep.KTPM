using System;

namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class LoTrinhDieuPhoiThuCongModels
    {
        // --- 1. THÔNG TIN ĐỊNH DANH HỆ THỐNG ---
        public int MaLoTrinh { get; set; }
        public DateTime? ThoiGianBatDauKeHoach { get; set; }
        public string TrangThai { get; set; }
        public int? MaNguoiDung { get; set; }
        public int? MaPhuongTien { get; set; }

      
        public int TongSoDonHang { get; set; }
        public int TongSoDiemDung { get; set; }
       
        public string KhoXuatPhat { get; set; }

        
        public string TuyenDuongHienThi { get; set; }

        public string YeuCauXe { get; set; }

      
        public string TenLoaiHang { get; set; }

        public double KhoiLuongHienThi { get; set; }

        public string TenTaiXeThucHien { get; set; }

        public string BienSoXe { get; set; }

        public int? MaKho { get; set; }
    }
}