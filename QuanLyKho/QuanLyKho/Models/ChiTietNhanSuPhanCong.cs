using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class ChiTietNhanSuPhanCong
{
    public int MaChiTiet { get; set; }

    public int MaPhanCong { get; set; }

    public int MaNguoiDung { get; set; }

    public string? VaiTro { get; set; }

    public string? GhiChuRieng { get; set; }

    public virtual PhanCongXe MaPhanCongNavigation { get; set; } = null!;
}
