using QuanLyLoTrinhTheoDoi.Models;

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

       

        public virtual ICollection<SuCoModels> SuCos { get; set; } = new List<SuCoModels>();
    }
}
