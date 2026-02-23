using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class DiemThuong
{
    public int MaDiem { get; set; }

    public int MaKhachHang { get; set; }

    public int? TongDiemTichLuy { get; set; }

    public int? DiemDaDung { get; set; }

    public DateTime? NgayCapNhatCuoi { get; set; }

    public virtual KhachHang MaKhachHangNavigation { get; set; } = null!;
}
