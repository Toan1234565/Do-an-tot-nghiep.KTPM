using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;

namespace QuanLyLoTrinhTheoDoi.Models12
{
    public class DiemDungModels
    {
       

        public int MaDiaChi { get; set; }

        public int ThuTuDung { get; set; }

        public string? LoaiDung { get; set; }

        public DateTime? EtaKeHoach { get; set; }

        public DateTime? ThoiGianDenThucTe { get; set; }

        public string? MaVungH3 { get; set; }

        public DiaChiModel? DiaChi { get; set; }


    }
}
