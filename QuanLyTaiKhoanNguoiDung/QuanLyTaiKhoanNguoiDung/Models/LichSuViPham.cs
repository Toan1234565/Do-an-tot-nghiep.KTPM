using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class LichSuViPham
{
    public int MaViPham { get; set; }

    public int MaTaiXe { get; set; }

    public DateTime? NgayViPham { get; set; }

    public string LoaiViPham { get; set; } = null!;

    public string? MoTaChiTiet { get; set; }

    public decimal? MucPhat { get; set; }

    public string? HinhThucXuLy { get; set; }

    public string? TrangThaiXuLy { get; set; }

    public int? NguoiLapBienBan { get; set; }

    public virtual TaiXe MaTaiXeNavigation { get; set; } = null!;

    public virtual NguoiDung? NguoiLapBienBanNavigation { get; set; }
}
