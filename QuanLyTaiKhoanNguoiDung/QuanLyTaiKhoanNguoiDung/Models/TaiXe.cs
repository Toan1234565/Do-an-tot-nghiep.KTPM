using System;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Models;

public partial class TaiXe
{
    public int MaNguoiDung { get; set; }

    public string SoBangLai { get; set; } = null!;

    public string LoaiBangLai { get; set; } = null!;

    public DateOnly? NgayCapBang { get; set; }

    public DateOnly NgayHetHanBang { get; set; }

    public int? KinhNghiemNam { get; set; }

    public string? TrangThaiHoatDong { get; set; }

    public decimal? DiemUyTin { get; set; }

    public string? AnhBangLaiTruoc { get; set; }

    public string? AnhBangLaiSau { get; set; }


    public virtual ICollection<LichSuViPham> LichSuViPhams { get; set; } = new List<LichSuViPham>();

    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;
}
