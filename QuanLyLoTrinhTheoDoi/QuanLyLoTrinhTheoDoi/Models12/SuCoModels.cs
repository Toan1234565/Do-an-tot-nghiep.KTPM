using QuanLyLoTrinhTheoDoi.Models;

namespace QuanLyLoTrinhTheoDoi.Models12
{
    public class SuCoModels
    {
        public int MaSuCo { get; set; }

        public int MaLoTrinh { get; set; }

      
        public string? MoTa { get; set; }

        public DateTime? ThoiGianBaoCao { get; set; }

        public DateTime? ThoiGianXuLy { get; set; }

        public string? TrangThai { get; set; }

        public int? MaLoaiSuCo { get; set; }

        public string? UrlHinhAnhSuCo { get; set; }

        public string? UrlVideoSuCo { get; set; }

        public double? ViDo { get; set; }

        public double? KinhDo { get; set; }

        public string? DiaChiCuThe { get; set; }
        public string? GhiChuTuChoi { get; set; }

        public virtual LoTrinhModels? MaLoTrinhNavigation { get; set; } = null!;

        public virtual LoaiSuCoModels? MaLoaiSuCoNavigation { get; set; }
    }
}
