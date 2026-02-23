using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class LoaiKhuyenMai
{
    public int MaLoaiKm { get; set; }

    public string TenLoai { get; set; } = null!;

    public string? MoTa { get; set; }

    public string? IconUrl { get; set; }

    public bool? TrangThai { get; set; }

    public virtual ICollection<KhuyenMai> KhuyenMais { get; set; } = new List<KhuyenMai>();
}
