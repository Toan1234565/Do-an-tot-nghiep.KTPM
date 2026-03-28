using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class HoaDon
{
    public int MaHoaDon { get; set; }

    public int MaDonHang { get; set; }

    public int MaPttt { get; set; }

    public decimal SoTienThanhToan { get; set; }

    public DateTime? NgayThanhToan { get; set; }

    public string? TrangThaiThanhToan { get; set; }

    public string? MaGiaoDichNgoai { get; set; }

    public string? NoiDungThanhToan { get; set; }

    public string? HinhAnhChungTu { get; set; }

    public virtual DonHang MaDonHangNavigation { get; set; } = null!;

    public virtual DanhMucPhuongThucThanhToan MaPtttNavigation { get; set; } = null!;
}
