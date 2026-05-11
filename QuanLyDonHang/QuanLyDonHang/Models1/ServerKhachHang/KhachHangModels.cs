namespace QuanLyDonHang.Models1.ServerKhachHang
{
    public class KhachHangModels
    {
        public int MaKhachHang { get; set; }

        public string TenCongTy { get; set; } = null!;

        public string? TenLienHe { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }


        public virtual DiaChiModel? MaDiaChiMacDinhNavigation { get; set; }
    }
}
