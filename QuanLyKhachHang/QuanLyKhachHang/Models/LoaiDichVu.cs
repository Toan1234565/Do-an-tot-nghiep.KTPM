using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class LoaiDichVu
{
    public int MaLoaiDv { get; set; }

    public string? TenLoaiDv { get; set; }

    public string? MoTa { get; set; }

    public bool? CoPhaiGiaoThang { get; set; }
}
