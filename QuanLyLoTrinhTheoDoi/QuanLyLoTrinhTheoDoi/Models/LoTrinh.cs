using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class LoTrinh
{
    public int MaLoTrinh { get; set; }

    public DateTime? ThoiGianBatDauKeHoach { get; set; }

    public DateTime? ThoiGianBatDauThucTe { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public DateTime? ThoiGianKetThucThucTe { get; set; }

    public int? MaKhoQuanLy { get; set; }

    public bool? LoTrinhTuyen { get; set; }

    public int? MaPtTx { get; set; }

    public double? TongKhoiLuongKg { get; set; }

    public virtual ICollection<ChiPhiLoTrinh> ChiPhiLoTrinhs { get; set; } = new List<ChiPhiLoTrinh>();

    public virtual ICollection<ChiTietLoTrinhKienHang> ChiTietLoTrinhKienHangs { get; set; } = new List<ChiTietLoTrinhKienHang>();

    public virtual ICollection<DiemDung> DiemDungs { get; set; } = new List<DiemDung>();

    public virtual ICollection<LichSuHanhTrinhDonHang> LichSuHanhTrinhDonHangs { get; set; } = new List<LichSuHanhTrinhDonHang>();

    public virtual PhuongTienTaiXe? MaPtTxNavigation { get; set; }

    public virtual ICollection<SuCo> SuCos { get; set; } = new List<SuCo>();
}
