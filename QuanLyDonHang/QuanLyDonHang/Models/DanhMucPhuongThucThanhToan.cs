using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class DanhMucPhuongThucThanhToan
{
    public int MaPttt { get; set; }

    public string TenPttt { get; set; } = null!;

    public string? LoaiThanhToan { get; set; }

    public string? MoTa { get; set; }

    public bool? TrangThai { get; set; }

    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
}
