using QuanLyTaiKhoanNguoiDung.Models12.QuanLyLoTrinhTheoDoi;

namespace QuanLyLoTrinhTheoDoi.Models12
{
    public class LoTrinhModels
    {
        public int MaLoTrinh { get; set; }

        public int? MaNguoiDung { get; set; }

        public int? MaPhuongTien { get; set; }

        public DateTime? ThoiGianBatDauKeHoach { get; set; }

        public DateTime? ThoiGianBatDauThucTe { get; set; }

        public string? TrangThai { get; set; }

       

        public virtual ICollection<SuCoModels> SuCos { get; set; } = new List<SuCoModels>();
    }
}
