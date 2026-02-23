using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class SanBay
{
    public int MaSanBay { get; set; }

    public string IataCode { get; set; } = null!;

    public string TenSanBay { get; set; } = null!;

    public int MaDiaChi { get; set; }

    public bool? TrangThai { get; set; }

    public virtual DiaChi MaDiaChiNavigation { get; set; } = null!;
}
