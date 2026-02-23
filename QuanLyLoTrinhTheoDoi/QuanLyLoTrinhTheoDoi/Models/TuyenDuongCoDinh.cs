using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class TuyenDuongCoDinh
{
    public int MaTuyen { get; set; }

    public int MaHopDong { get; set; }

    public int MaDiaChiLay { get; set; }

    public int MaDiaChiGiao { get; set; }

    public double? KhoangCachKm { get; set; }

    public int? MaXeDinhDanh { get; set; }
}
