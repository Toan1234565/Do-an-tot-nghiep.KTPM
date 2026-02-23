using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class LoaiSuCo
{
    public int MaLoaiSuCo { get; set; }

    public string TenLoaiSuCo { get; set; } = null!;

    public int? MucDoNghiemTrong { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<SuCo> SuCos { get; set; } = new List<SuCo>();
}
