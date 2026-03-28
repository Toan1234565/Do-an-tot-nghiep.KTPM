using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class DonHang
{
    public int MaDonHang { get; set; }

    public int MaKhachHang { get; set; }

    public DateTime ThoiGianTao { get; set; }

    public int MaDiaChiLayHang { get; set; }

    public string? TrangThaiHienTai { get; set; }

    public string? TenDonHang { get; set; }

    public int? MaLoaiDv { get; set; }

    public int? MaHopDongNgoai { get; set; }

    public bool? LaDonGiaoThang { get; set; }

    public string? GhiChuDacBiet { get; set; }

    public int? MaVung { get; set; }

    public int? MaDiaChiNhanHang { get; set; }

    public string? TenNguoiNhan { get; set; }

    public string? SdtNguoiNhan { get; set; }

    public int? MaMucDoDv { get; set; }

    public decimal? TongTienDuKien { get; set; }

    public decimal? TongTienThucTe { get; set; }

    public DateTime? ThoiGianGiaoDuKien { get; set; }

    public int? MaKhuyenMai { get; set; }

    public string? MaVungH3Nhan { get; set; }

    public string? MaVungH3Giao { get; set; }

    public string? TrangThaiThanhToanTong { get; set; }

    public int? MaPttt { get; set; }

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual ICollection<KienHang> KienHangs { get; set; } = new List<KienHang>();

    public virtual DanhMucPhuongThucThanhToan? MaPtttNavigation { get; set; }
}
