namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang
{
    public class HopDongVanChuyenModels
    {
        public int MaHopDong { get; set; }

        public string? TenHopDong { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime? NgayKy { get; set; }

        public DateTime? NgayHetHan { get; set; }

        public string? LoaiHangHoa { get; set; }

        public string? TrangThai { get; set; }

        public byte[]? FileHopDong { get; set; }

        public string? TenFileGoc { get; set; }

        public bool HasFile { get; set; }

        public virtual KhachHangModels? MaKhachHangNavigation { get; set; } 
    }
}
