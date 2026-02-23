using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoan.Models;

public partial class VaiTro
{
    public int MaVaiTro { get; set; }

    public string? TenVaiTro { get; set; }

    public string? QuyenHan { get; set; }

    public virtual ICollection<ChucVu> ChucVus { get; set; } = new List<ChucVu>();
}
