namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai
{
    public class LoaiKhoModel
    {
        public int MaLoaiKho { get; set; }

        public string TenLoaiKho { get; set; } = null!;

        public string? GhiChu { get; set; }

        public virtual ICollection<LoaiKhoModel> KhoBais { get; set; } = new List<LoaiKhoModel>();
    }
}
