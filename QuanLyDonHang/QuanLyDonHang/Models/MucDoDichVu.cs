using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class MucDoDichVu
{
    public int MaDichVu { get; set; }

    public string TenDichVu { get; set; } = null!;

    public string? ThoiGianCamKet { get; set; }

    public double? HeSoNhiPhan { get; set; }

    public bool? LaCaoCap { get; set; }

    public bool? TrangThai { get; set; }

    public DateTime? NgayBatDau { get; set; }

    public DateTime? NgayKetThuc { get; set; }

    public int? MaBangCu { get; set; }

    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
}
