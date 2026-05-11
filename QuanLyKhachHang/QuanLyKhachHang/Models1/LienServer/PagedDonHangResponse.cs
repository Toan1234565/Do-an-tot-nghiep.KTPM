namespace QuanLyKhachHang.Models1.LienServer
{
    public class PagedDonHangResponse
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public List<DonHangModels> Data { get; set; }
    }
}
