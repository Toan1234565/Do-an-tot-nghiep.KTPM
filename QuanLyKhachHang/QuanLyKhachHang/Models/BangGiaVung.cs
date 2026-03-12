using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class BangGiaVung
{
    public int MaBangGia { get; set; }

    public string? KhuVucLay { get; set; }

    public string? KhuVucGiao { get; set; }

    public decimal? TrongLuongToiThieuKg { get; set; }

    public decimal? TrongLuongToiDaKg { get; set; }

    public decimal? DonGiaCoBan { get; set; }

    public decimal? PhuPhiMoiKg { get; set; }

    public int? MaBangCu { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public string? LyDoThayDoi { get; set; }

    /// <summary>
    /// 1: Theo Vùng, 2: Theo Km
    /// </summary>
    public int? LoaiTinhGia { get; set; }

    public decimal? DonGiaKm { get; set; }

    public decimal? PhiDungDiem { get; set; }

    public int? KmToiThieu { get; set; }

    public int? MaLoaiHang { get; set; }

    public bool? IsActive { get; set; }
}
