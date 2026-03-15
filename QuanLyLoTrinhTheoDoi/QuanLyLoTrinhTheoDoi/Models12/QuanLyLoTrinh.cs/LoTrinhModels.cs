using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.QuanLyLoTrinh.cs;

namespace QuanLyLoTrinhTheoDoi.Models12
{
    public class LoTrinhModels
    {
        public int MaLoTrinh { get; set; }

        public int? MaTaiXeChinh { get; set; }

        public int? MaTaiXePhu { get; set; }

        public int? MaPhuongTien { get; set; }

        public DateTime? ThoiGianBatDauKeHoach { get; set; }

        public DateTime? ThoiGianBatDauThucTe { get; set; }

        public string? TrangThai { get; set; }

        public int? TongSoDonHang { get; set; }
        public int? TongSoDiemDung { get; set; }

        public virtual ICollection<ChiPhiLoTrinhModels> ChiPhiLoTrinhs { get; set; } = new List<ChiPhiLoTrinhModels>();

        public virtual ICollection<ChiTietLoTrinhModels> ChiTietLoTrinhKienHangs { get; set; } = new List<ChiTietLoTrinhModels>();

        public virtual ICollection<DiemDungModels> DiemDungs { get; set; } = new List<DiemDungModels>();

        public virtual ICollection<SuCoModels> SuCos { get; set; } = new List<SuCoModels>();


    }
}
