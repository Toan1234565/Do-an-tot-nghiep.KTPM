using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class KhoBai
{
    public int MaKho { get; set; }

    public int MaDiaChi { get; set; }

    public int? MaQuanLy { get; set; }

    public double? DungTichM3 { get; set; }

    public string? TenKhoBai { get; set; }

    public decimal? DienTichM2 { get; set; }

    public string? TrangThai { get; set; }

    public decimal? SucChua { get; set; }

    public string? SoDienThoaiKho { get; set; }

    public int? MaLoaiKho { get; set; }

    public string? MaVungH3 { get; set; }

    public virtual LoaiKho? MaLoaiKhoNavigation { get; set; }

    public virtual ICollection<PhuongTien> PhuongTiens { get; set; } = new List<PhuongTien>();
}
