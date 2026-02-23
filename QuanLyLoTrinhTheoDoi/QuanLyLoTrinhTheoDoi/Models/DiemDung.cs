using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class DiemDung
{
    public int MaDiemDung { get; set; }

    public int MaLoTrinh { get; set; }

    public int MaDiaChi { get; set; }

    public int ThuTuDung { get; set; }

    public string? LoaiDung { get; set; }

    public DateTime? EtaKeHoach { get; set; }

    public DateTime? ThoiGianDenThucTe { get; set; }

    public virtual LoTrinh MaLoTrinhNavigation { get; set; } = null!;
}
