using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class PhuongTien
{
    public int MaPhuongTien { get; set; }

    public string? BienSo { get; set; }

    public int MaLoaiXe { get; set; }

    public double? TaiTrongToiDaKg { get; set; }

    public double? TheTichToiDaM3 { get; set; }

    public double? MucTieuHaoNhienLieu { get; set; }

    public string? TrangThai { get; set; }

    public int? MaKho { get; set; }

    public double? SoKmHienTai { get; set; }

    public virtual ICollection<DangKiem> DangKiems { get; set; } = new List<DangKiem>();

    public virtual ICollection<LichSuBaoTri> LichSuBaoTris { get; set; } = new List<LichSuBaoTri>();

    public virtual KhoBai? MaKhoNavigation { get; set; }

    public virtual LoaiXe MaLoaiXeNavigation { get; set; } = null!;

    public virtual ICollection<PhanCongXe> PhanCongXes { get; set; } = new List<PhanCongXe>();
}
