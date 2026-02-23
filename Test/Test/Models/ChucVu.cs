using System;
using System.Collections.Generic;

namespace Test.Models;

public partial class ChucVu
{
    public int MaChucVu { get; set; }

    public string? TenChucVu { get; set; }

    public int? MaVaiTro { get; set; }

    public virtual VaiTro? MaVaiTroNavigation { get; set; }

    public virtual ICollection<NguoiDung> NguoiDungs { get; set; } = new List<NguoiDung>();
}
