using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class KhachHang
{
    public int MaKhachHang { get; set; }

    public string TenCongTy { get; set; } = null!;

    public string? TenLienHe { get; set; }

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public int? MaDiaChiMacDinh { get; set; }

    public virtual ICollection<DiemThuong> DiemThuongs { get; set; } = new List<DiemThuong>();

    public virtual ICollection<HopDongVanChuyen> HopDongVanChuyens { get; set; } = new List<HopDongVanChuyen>();

    public virtual ICollection<LichSuDungMa> LichSuDungMas { get; set; } = new List<LichSuDungMa>();

    public virtual DiaChi? MaDiaChiMacDinhNavigation { get; set; }
}
