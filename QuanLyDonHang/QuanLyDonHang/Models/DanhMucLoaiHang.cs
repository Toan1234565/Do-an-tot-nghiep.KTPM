using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class DanhMucLoaiHang
{
    public int MaLoaiHang { get; set; }

    public string? TenLoaiHang { get; set; }

    public string? MoTa { get; set; }

    public virtual ICollection<KienHang> KienHangs { get; set; } = new List<KienHang>();
}
