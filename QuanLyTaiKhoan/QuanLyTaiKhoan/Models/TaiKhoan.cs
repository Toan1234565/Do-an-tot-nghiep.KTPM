using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoan.Models;

public partial class TaiKhoan
{
    public int MaNguoiDung { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public bool? HoatDong { get; set; }

    public virtual NguoiDung? NguoiDung { get; set; }
}
