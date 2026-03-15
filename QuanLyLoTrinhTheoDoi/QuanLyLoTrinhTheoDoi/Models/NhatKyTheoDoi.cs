using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class NhatKyTheoDoi
{
    public int MaNhatKy { get; set; }

    public int? MaTaiXe { get; set; }

    public double ViDo { get; set; }

    public double KinhDo { get; set; }

    public DateTime ThoiGian { get; set; }

    public double? TocDoKmh { get; set; }

    public string? MaVungH3 { get; set; }
}
