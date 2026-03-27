using QuanLyKho.Models;

namespace QuanLyKho.Models1.QuanLyXe
{
    public class PhuongTienModels
    {
        public int MaPhuongTien { get; set; }

        public string? BienSo { get; set; }

        public int MaLoaiXe { get; set; }

        public string ? TenLoaiXe { get; set; }

        public double? TaiTrongToiDaKg { get; set; }

        public double? TheTichToiDaM3 { get; set; }

        public double? MucTieuHaoNhienLieu { get; set; }

        public string? TrangThai { get; set; }

        public string? TenKho { get; set; }

        public int MaKho {  get; set; }

        public string ? GhiChu { get; set; }

        public double? SoKmHienTai { get; set; }

        public virtual ICollection<LichSuBaoTriModels> LichSuBaoTris { get; set; } = new List<LichSuBaoTriModels>();
        public virtual ICollection<DangKiemModel> DangKiems { get; set; } = new List<DangKiemModel>();
        public List<CanhBaoBaoTriModel> DanhSachCanBaoTri { get; set; } = new List<CanhBaoBaoTriModel>();
    }
}

