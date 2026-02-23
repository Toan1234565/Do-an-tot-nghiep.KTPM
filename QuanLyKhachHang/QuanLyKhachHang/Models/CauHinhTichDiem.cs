using System;
using System.Collections.Generic;

namespace QuanLyKhachHang.Models;

public partial class CauHinhTichDiem
{
    public int Id { get; set; }

    public decimal TyLeTichDiem { get; set; }

    public decimal GiaTriDiem { get; set; }

    public int DiemToiThieuDeDung { get; set; }

    public bool ChoPhepDungDiem { get; set; }

    public DateTime? NgayCapNhat { get; set; }
}
