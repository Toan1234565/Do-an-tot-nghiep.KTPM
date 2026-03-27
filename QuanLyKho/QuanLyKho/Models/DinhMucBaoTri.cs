using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class DinhMucBaoTri
{
    public int MaDinhMuc { get; set; }

    public int MaLoaiXe { get; set; }

    public string? TenHangMuc { get; set; }

    public double? DinhMucKm { get; set; }

    public int? DinhMucThang { get; set; }

    public virtual ICollection<LichSuBaoTri> LichSuBaoTris { get; set; } = new List<LichSuBaoTri>();

    public virtual LoaiXe MaLoaiXeNavigation { get; set; } = null!;
}
