using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class ChiTietLenhXuat
{
    public int MaChiTietLenhXuat { get; set; }

    public int MaLenhXuat { get; set; }

    public int MaKienHang { get; set; }

    public string? ViTriKhuVuc { get; set; }

    public bool? TrangThaiKiemDem { get; set; }

    public virtual LenhXuatKho MaLenhXuatNavigation { get; set; } = null!;
}
