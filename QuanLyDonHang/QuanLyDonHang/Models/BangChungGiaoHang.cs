using System;
using System.Collections.Generic;

namespace QuanLyDonHang.Models;

public partial class BangChungGiaoHang
{
    public int MaPod { get; set; }

    public int MaKienHang { get; set; }

    public DateTime ThoiGian { get; set; }

    public string? UrlChuKy { get; set; }

    public string? UrlAnh { get; set; }

    public string? TenNguoiNhan { get; set; }

    public virtual KienHang MaKienHangNavigation { get; set; } = null!;
}
