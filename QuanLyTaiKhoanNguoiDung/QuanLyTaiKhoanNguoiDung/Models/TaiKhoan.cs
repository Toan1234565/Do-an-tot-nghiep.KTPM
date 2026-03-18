using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class TaiKhoan
{
    public int MaNguoiDung { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public bool? HoatDong { get; set; }

    public virtual NguoiDung? NguoiDung { get; set; }
}
