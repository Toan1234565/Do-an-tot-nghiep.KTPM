namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer
{
    public class CapNhatGanPhuongTienRequest
    {
        public int MaPhuongTien { get; set; }
        public int MaNguoiDung { get; set; }
        public int? MaNguoiDungPhu { get; set; }
        public int? MaCa { get; set; }
        public string LoaiTuyen { get; set; }
    }
}
