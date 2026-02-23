namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhuyenMai
{
    public class KhuyenMaiModels
    {
        public int MaKhuyenMai { get; set; }

        public string CodeKhuyenMai { get; set; } = null!;

        public string? TenChuongTrinh { get; set; }

        public int MaLoaiKm { get; set; }

        public string? KieuGiamGia { get; set; }

        public decimal GiaTriGiam { get; set; }

        public decimal? GiamToiDa { get; set; }

        public DateTime? NgayBatDau { get; set; }

        public DateTime? NgayKetThuc { get; set; }

        public int? SoLuongToiDa { get; set; }

        public int? SoLuongDaDung { get; set; }

        public decimal? DonHangToiThieu { get; set; }

        public bool? TrangThai { get; set; }

        public virtual ICollection<LichSuDungMaModels> LichSuDungMas { get; set; } = new List<LichSuDungMaModels>();

        public virtual LoaiKhuyenMaiModels MaLoaiKmNavigation { get; set; } = null!;
    }
}

