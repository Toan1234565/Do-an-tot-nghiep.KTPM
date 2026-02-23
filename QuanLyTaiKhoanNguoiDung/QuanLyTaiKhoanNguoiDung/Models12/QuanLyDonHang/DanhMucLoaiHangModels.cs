namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang
{
    public class DanhMucLoaiHangModels
    {
        public int MaLoaiHang { get; set; }

        public string? TenLoaiHang { get; set; }

        public string? MoTa { get; set; }

        public virtual ICollection<KienHangModels> KienHangs { get; set; } = new List<KienHangModels>();
    }
}
