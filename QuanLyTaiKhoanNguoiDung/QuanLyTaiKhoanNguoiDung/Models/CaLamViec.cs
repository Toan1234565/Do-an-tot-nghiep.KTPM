using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class CaLamViec
{
    public int MaCa { get; set; }

    public string? TenCa { get; set; }

    public TimeOnly? GioBatDau { get; set; }

    public TimeOnly? GioKetThuc { get; set; }

    public virtual ICollection<DangKyCaTruc> DangKyCaTrucs { get; set; } = new List<DangKyCaTruc>();
}
