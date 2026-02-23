namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhuyenMai
{
    public class LoaiKhuyenMaiModels
    {
        public int MaLoaiKm { get; set; }

        public string TenLoai { get; set; } = null!;

        public string? MoTa { get; set; }

        public string? IconUrl { get; set; }

        public bool? TrangThai { get; set; }

        public virtual ICollection<KhuyenMaiModels> KhuyenMais { get; set; } = new List<KhuyenMaiModels>();
    }
}
