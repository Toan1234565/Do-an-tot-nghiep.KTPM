using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class LoaiXe
{
    public int MaLoaiXe { get; set; }

    public string? TenLoai { get; set; }

    public virtual ICollection<DinhMucBaoTri> DinhMucBaoTris { get; set; } = new List<DinhMucBaoTri>();

    public virtual ICollection<PhuongTien> PhuongTiens { get; set; } = new List<PhuongTien>();
}
