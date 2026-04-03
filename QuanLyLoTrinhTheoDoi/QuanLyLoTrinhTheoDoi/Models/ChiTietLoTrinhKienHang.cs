using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class ChiTietLoTrinhKienHang
{
    public int MaLoTrinh { get; set; }

    public int? MaDonHang { get; set; }

    public string? TrangThaiTrenXe { get; set; }

    public int MaChiTietLoTrinh { get; set; }

    public DateTime? ThoiGianCapNhat { get; set; }

    public virtual LoTrinh MaLoTrinhNavigation { get; set; } = null!;
}
