using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class NguoiDung
{
    public int MaNguoiDung { get; set; }

    public string? HoTenNhanVien { get; set; }

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public int? MaChucVu { get; set; }

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? SoCccd { get; set; }

    public string? NoiSinh { get; set; }

    public string? SoTaiKhoan { get; set; }

    public string? TenNganHang { get; set; }

    public string? BaoHiemXaHoi { get; set; }

    public string? DonViLamViec { get; set; }

    public int? MaDiaChi { get; set; }

    public virtual ChucVu? MaChucVuNavigation { get; set; }

    public virtual TaiKhoan MaNguoiDungNavigation { get; set; } = null!;
}
