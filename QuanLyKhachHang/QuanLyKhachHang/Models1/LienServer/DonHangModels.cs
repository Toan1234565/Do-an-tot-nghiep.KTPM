using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models1.LienServer
{
    public class DonHangModels
    {
        public int MaDonHang { get; set; }
        public string? TenDonHang { get; set; }
        public decimal? TongTienThucTe { get; set; }

        // Nếu API trả về là "Đang giao", "Hủy"... thì string? là chuẩn xác
        public string? TrangThaiHienTai { get; set; }

        public DateTime ThoiGianTao { get; set; }
    }
}