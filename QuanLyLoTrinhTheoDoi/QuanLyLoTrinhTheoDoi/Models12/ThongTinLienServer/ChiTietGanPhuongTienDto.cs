namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer
{
    public class ChiTietGanPhuongTienDto
    {
        public int MaPtTx { get; set; }
        public int MaPhuongTien { get; set; }
        public string BienSo { get; set; }
        public string TenLoaiXe { get; set; }
        public int MaCa { get; set; }
        public string LoaiTuyen { get; set; }

        // Thông tin tài xế chính
        public int MaTaiXeChinh { get; set; }
        public string TenTaiXeChinh { get; set; }

        // Thông tin tài xế phụ (nếu có)
        public int? MaTaiXePhu { get; set; }
        public string TenTaiXePhu { get; set; }

        public bool? IsActive { get; set; }
    }
}
