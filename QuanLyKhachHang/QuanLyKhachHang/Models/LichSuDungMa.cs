using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class LichSuDungMa
{
    public int MaLichSu { get; set; }

    public int MaKhachHang { get; set; }

    public int MaKhuyenMai { get; set; }

    public int MaDonHang { get; set; }

    public DateTime? NgaySuDung { get; set; }

    public virtual KhachHang MaKhachHangNavigation { get; set; } = null!;

    public virtual KhuyenMai MaKhuyenMaiNavigation { get; set; } = null!;
}
