using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class LoaiKho
{
    public int MaLoaiKho { get; set; }

    public string TenLoaiKho { get; set; } = null!;

    public string? GhiChu { get; set; }

    public virtual ICollection<KhoBai> KhoBais { get; set; } = new List<KhoBai>();
}
