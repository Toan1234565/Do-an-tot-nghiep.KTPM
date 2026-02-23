using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class DiaChi
{
    public int MaDiaChi { get; set; }

    public string? Duong { get; set; }

    public string? ThanhPho { get; set; }

    public string? MaBuuDien { get; set; }

    public double? ViDo { get; set; }

    public double? KinhDo { get; set; }

    public string? Phuong { get; set; }

    public virtual ICollection<KhachHang> KhachHangs { get; set; } = new List<KhachHang>();

    public virtual ICollection<SanBay> SanBays { get; set; } = new List<SanBay>();
}
