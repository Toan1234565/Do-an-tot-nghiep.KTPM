using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class KienHang
{
    public int MaKienHang { get; set; }

    public int MaDonHang { get; set; }

    public string? MaVach { get; set; }

    public double? KhoiLuong { get; set; }

    public double? TheTich { get; set; }

    public bool DaThuGom { get; set; }

    public decimal? SoTien { get; set; }

    public int? MaKhoHienTai { get; set; }

    public string? YeuCauBaoQuan { get; set; }

    public int? SoLuongKienHang { get; set; }

    public int? MaLoaiHang { get; set; }

    public int? MaBangGiaVung { get; set; }

    public virtual BangChungGiaoHang? BangChungGiaoHang { get; set; }

    public virtual ICollection<CapNhatTrangThai> CapNhatTrangThais { get; set; } = new List<CapNhatTrangThai>();

    public virtual DonHang MaDonHangNavigation { get; set; } = null!;

    public virtual DanhMucLoaiHang? MaLoaiHangNavigation { get; set; }
}
