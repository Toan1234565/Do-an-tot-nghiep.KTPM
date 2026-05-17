namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien
{
    // Đối tượng bọc dữ liệu phân trang trả về từ API phương tiện
    public class PhuongTienPagedResponse
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public List<PhuongTienListModel> Data { get; set; } = new List<PhuongTienListModel>();
    }

    // Đối tượng chi tiết từng chiếc xe trong danh sách
    public class PhuongTienListModel
    {
        public int MaPhuongTien { get; set; }
        public string BienSo { get; set; } = string.Empty;
        public int? MaLoaiXe { get; set; }
        public string TenLoaiXe { get; set; } = "N/A";
        public double? TaiTrongToiDaKg { get; set; }
        public double? TheTichToiDaM3 { get; set; }
        public double? MucTieuHaoNhienLieu { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public int? MaKho { get; set; }
        public string TenKho { get; set; } = "Chưa xác định";
    }
}
