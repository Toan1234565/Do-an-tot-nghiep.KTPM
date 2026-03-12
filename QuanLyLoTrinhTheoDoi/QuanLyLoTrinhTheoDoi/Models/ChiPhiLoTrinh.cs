using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class ChiPhiLoTrinh
{
    public int MaChiPhi { get; set; }

    public int? MaLoTrinh { get; set; }

    public string? LoaiChiPhi { get; set; }

    public decimal? SoTien { get; set; }

    public string? ChungTuKemTheo { get; set; }

    public string? GhiChu { get; set; }

    public virtual LoTrinh? MaLoTrinhNavigation { get; set; }
}
