namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang
{
    public class DonHangModels
    {
        public int MaDonHang { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime ThoiGianTao { get; set; }

        public int MaDiaChiLayHang { get; set; }

        public string? TrangThaiHienTai { get; set; }

        public string? TenDonHang { get; set; }

        public int? MaLoaiDv { get; set; }

        public int? MaHopDongNgoai { get; set; }

        public bool? LaDonGiaoThang { get; set; }

        public string? GhiChuDacBiet { get; set; }

        public int? MaVung { get; set; }

        public int? MaDiaChiGiaoHang { get; set; }

        public string? TenNguoiNhan { get; set; }

        public string? SdtNguoiNhan { get; set; }

        public decimal? TongTienDuKien { get; set; }

        public decimal? TongTienThucTe { get; set; }

        public int? MaMucDoDv { get; set; }


        public virtual ICollection<KienHangModels> KienHangs { get; set; } = new List<KienHangModels>();
    }
}
