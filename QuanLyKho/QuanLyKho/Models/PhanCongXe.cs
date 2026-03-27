using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class PhanCongXe
{
    public int MaPhanCong { get; set; }

    public int MaPhuongTien { get; set; }

    public DateTime? NgayBatDauBanGiao { get; set; }

    public DateTime? NgayKetThucDuKien { get; set; }

    public DateTime? NgayTraXeThucTe { get; set; }

    public double? SoKmLucNhan { get; set; }

    public double? SoKmLucTra { get; set; }

    public string? TrangThaiBanGiao { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<ChiTietNhanSuPhanCong> ChiTietNhanSuPhanCongs { get; set; } = new List<ChiTietNhanSuPhanCong>();

    public virtual PhuongTien MaPhuongTienNavigation { get; set; } = null!;
}
