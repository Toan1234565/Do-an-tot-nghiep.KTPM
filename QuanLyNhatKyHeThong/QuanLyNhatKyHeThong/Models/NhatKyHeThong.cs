using System;
using System.Collections.Generic;

namespace QuanLyNhatKyHeThong.Models;

public partial class NhatKyHeThong
{
    public long MaNhatKy { get; set; }

    public string? TenDichVu { get; set; }

    public string? LoaiThaoTac { get; set; }

    public string? MaDoiTuong { get; set; }

    public string? TenBangLienQuan { get; set; }

    public string? DuLieuCu { get; set; }

    public string? DuLieuMoi { get; set; }

    public string? NguoiThucHien { get; set; }

    public DateTime? ThoiGianThucHien { get; set; }

    public string? DiaChiIp { get; set; }

    public bool? TrangThaiThaoTac { get; set; }
}
