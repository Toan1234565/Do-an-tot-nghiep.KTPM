namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien
{
    public class PhanCongXeModels
    {
        public int MaPhanCong { get; set; }

        public int MaPhuongTien { get; set; }

        public DateTime? NgayBatDauBanGiao { get; set; }

        public DateTime? NgayKetThucDuKien { get; set; }

        public DateTime? NgayTraXeThucTe { get; set; }

        public double? SoKmLucNhan { get; set; }

        public double? SoKmLucTra { get; set; }

        public string? TrangThaiBanGiao { get; set; }

        public string? GhiChu { get; set; }

        public string? LoaiPhanCong { get; set; }

        public int? MaDangKy { get; set; }

        public virtual ICollection<ChiTietNhanSuPhanCongModels> ChiTietNhanSuPhanCongs { get; set; } = new List<ChiTietNhanSuPhanCongModels>();
    }
}
