using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class DangKyCaTruc
{
    public int MaDangKy { get; set; }

    public int MaTaiXe { get; set; }

    public int MaCa { get; set; }

    public DateOnly NgayTruc { get; set; }

    public string? TrangThai { get; set; }

    public virtual CaLamViec MaCaNavigation { get; set; } = null!;

    public virtual TaiXe MaTaiXeNavigation { get; set; } = null!;
}
