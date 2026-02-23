using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class CapNhatTrangThai
{
    public int MaCapNhat { get; set; }

    public int? MaKienHang { get; set; }

    public int? MaLoTrinh { get; set; }

    public string TrangThaiMoi { get; set; } = null!;

    public DateTime ThoiGian { get; set; }

    public int? BaoCaoBoi { get; set; }

    public double? ViDo { get; set; }

    public double? KinhDo { get; set; }

    public virtual KienHang? MaKienHangNavigation { get; set; }
}
