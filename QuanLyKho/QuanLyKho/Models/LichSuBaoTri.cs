using System;
using System.Collections.Generic;

namespace QuanLyKho.Models;

public partial class LichSuBaoTri
{
    public int MaBanGhi { get; set; }

    public int MaPhuongTien { get; set; }

    public decimal? ChiPhi { get; set; }

    public DateOnly? Ngay { get; set; }

    public double? SoKmThucTe { get; set; }

    public int? MaDinhMuc { get; set; }

    public virtual DinhMucBaoTri? MaDinhMucNavigation { get; set; }

    public virtual PhuongTien MaPhuongTienNavigation { get; set; } = null!;
}
