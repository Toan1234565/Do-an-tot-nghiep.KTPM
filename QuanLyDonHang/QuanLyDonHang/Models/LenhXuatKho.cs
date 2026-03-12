using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class LenhXuatKho
{
    public int MaLenhXuat { get; set; }

    public int MaKho { get; set; }

    public int MaLoTrinhExternal { get; set; }

    public DateTime? NgayTao { get; set; }

    public int? MaNguoiSoan { get; set; }

    public DateTime? ThoiGianSoanDon { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<ChiTietLenhXuat> ChiTietLenhXuats { get; set; } = new List<ChiTietLenhXuat>();
}
