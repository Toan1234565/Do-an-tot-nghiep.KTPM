using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer.cs
{
    public interface INhanVienService
    {
        Task<TenNhanVienModel?> GetTenNhanVienAsync(int maNguoiDung);
    }
    // Đại diện cho Server Địa chỉ
    public interface IDiaChiService
    {
        
        Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi);
    }
    public interface IDonHangService
    {
        Task<ChiTietDonHangLoTrinhModel?> GetChiTietDonHangAsync(int madonhang);
    }
    public interface IPhuongTienServiceClient
    {
        Task<PhuongTienDetailModel?> GetChiTietPhuongTienAsync(int maPhuongTien);
    }
    public interface IKhachHangServiceClient
    {
        Task<KhachHangSummaryDto?> GetChiTietKhachHangAsync(int maKhachHang);
    }

}
