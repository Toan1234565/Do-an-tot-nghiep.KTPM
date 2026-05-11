namespace QuanLyKhachHang.Models1.LienServer
{
    public interface IDonHangService
    {
        // Tên phương thức: GetDanhSachDonHangByKhachHangAsync
        Task<PagedDonHangResponse?> GetDanhSachDonHangByKhachHangAsync(int maKhachHang, int page = 1, int pageSize = 10);
    }
}