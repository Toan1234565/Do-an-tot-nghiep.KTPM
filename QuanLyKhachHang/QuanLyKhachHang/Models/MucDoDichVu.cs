using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class MucDoDichVu
{
    public int MaDichVu { get; set; }

    public string? TenDichVu { get; set; }

    public decimal? ThoiGianCamKet { get; set; }

    public decimal? HeSoNhiPhan { get; set; }

    public bool? LaCaoCap { get; set; }

    public bool? TrangThai { get; set; }

    public DateTime? NgayBatDau { get; set; }

    public DateTime? NgayKetThuc { get; set; }

    public int? MaBangCu { get; set; }
}
