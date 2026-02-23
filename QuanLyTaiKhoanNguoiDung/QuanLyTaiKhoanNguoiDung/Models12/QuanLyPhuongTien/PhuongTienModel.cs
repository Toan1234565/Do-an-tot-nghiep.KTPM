using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien
{
    public class PhuongTienModel
    {
        public int MaPhuongTien { get; set; }

        public string? BienSo { get; set; }

        public int MaLoaiXe { get; set; }

        public string? TenLoaiXe { get; set; }

        public double? TaiTrongToiDaKg { get; set; }

        public double? TheTichToiDaM3 { get; set; }

        public double? MucTieuHaoNhienLieu { get; set; }

        public string? TrangThai { get; set; }

        public int? MaKho { get; set; }

        public string? TenKho { get; set; }
        public string? GhiChu { get; set; }
        public double? SoKmHienTai { get; set; }

        
        public double KmDaDiKeTuLanCuoi { get; set; }
        public double KmDinhMuc { get; set; }
        public DateOnly? NgayBaoTriGanNhat { get; set; }
        public DateOnly? NgayDuKienTiepTheo { get; set; }

        // Thuộc tính tính toán để hiển thị
        public string GhiChu1
        {
            get
            {
                if (KmDinhMuc - KmDaDiKeTuLanCuoi <= 500)
                    return $"Sắp chạm mốc {KmDinhMuc}km (Còn {KmDinhMuc - KmDaDiKeTuLanCuoi}km)";
                if (NgayDuKienTiepTheo.HasValue)
                    return $"Hạn định kỳ: {NgayDuKienTiepTheo.Value:dd/MM/yyyy}";
                return "Đến hạn bảo trì";
            }
        }
        public virtual LoaiXeModels MaLoaiXeNavigation { get; set; } = null!;
        public virtual QuanLyKhobaiModels? MaKhoNavigation { get; set; }
        public virtual ICollection<LichSuBaoTri> LichSuBaoTris { get; set; } = new List<LichSuBaoTri>();
        public virtual ICollection<DangKiemModel> DangKiems { get; set; } = new List<DangKiemModel>();

        public List<CanhBaoBaoTriModels> DanhSachCanBaoTri { get; set; } = new List<CanhBaoBaoTriModels>();
    }
}
