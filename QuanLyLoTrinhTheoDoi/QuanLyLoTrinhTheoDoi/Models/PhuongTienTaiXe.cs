using System;
using System.Collections.Generic;

namespace QuanLyLoTrinhTheoDoi.Models;

public partial class PhuongTienTaiXe
{
    public int MaPtTx { get; set; }

    public int MaPhuongTien { get; set; }

    public string? LoaiTuyen { get; set; }

    public int? MaCa { get; set; }

    public int MaNguoiDung { get; set; }

    public bool? IsActive { get; set; }

    public int? MaNguoiDungPhu { get; set; }

    public virtual ICollection<LoTrinh> LoTrinhs { get; set; } = new List<LoTrinh>();
}
