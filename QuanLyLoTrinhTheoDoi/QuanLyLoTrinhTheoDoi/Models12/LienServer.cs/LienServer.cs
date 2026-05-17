using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DiaChi;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.TaiXe;
using System.Threading.Tasks;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer.cs
{
    public interface INhanVienService
    {
        Task<TenNhanVienModel?> GetTenNhanVienAsync(int maNguoiDung);

        //Kiểm tra xem tài xế có tồn tại và hợp lệ bên Server Người Dùng không
        Task<bool> KiemTraTaiXeTonTaiAsync(int maNguoiDung);

        // Cập nhật trạng thái gán (Trống/Đang bận) cho tài xế
        Task<bool> CapNhatTrangThaiTaiXeAsync(int maNguoiDung, bool trangThai);

        Task<UpdateTrangThaiTaiXeResponse?> UpdateTrangThaiTaiXeAsync(UpdateTaiXeTrangTai model);
        Task<DriverStatusResponseDto?> CheckDriverStatusAsync(int maNguoiDung);
        Task<List<CaLamViecModels>?> GetDanhSachCaLamAsync();
    }
    // Đại diện cho Server Địa chỉ
    public interface IDiaChiService
    {
        
        Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi);
        Task<List<ToaDoDiaChiResponseDto>?> GetToaDoDanhSachAsync(List<int> maDiaChis);
    }

    public interface IDonHangService
    {
        Task<ChiTietDonHangLoTrinhModel?> GetChiTietDonHangAsync(int madonhang);
        Task<ClusterResponseModel?> TuDongGomNhomDonHangAsync(ClusterRequest request);
        Task<UpdateMultiStatusResponseModel?> CapNhatTrangThaiNhieuDonHangAsync(UpdateMultiStatusRequest request);
        Task<DonHangViTriDto?> GetViTriHienTaiDonHangAsync(int maDonHang);
        Task<ThongTinGiaoHangDto?> GetThongTinGiaoHangAsync(int? maDonHang);
    }

    public interface IPhuongTienServiceClient
    {
        Task<PhuongTienDetailModel?> GetChiTietPhuongTienAsync(int maPhuongTien);

        // Thay đổi tham số từ soCaMoi thành maCa và trangThai
        Task<bool> CapNhatTrangThaiGanXeAsync(int maPhuongTien, int maCa, bool trangThai);

        Task<List<PhuongTienDTO>?> GetXeSanSangDieuPhoiAsync(double khoiLuongHang, int maKho);
        Task<UpdateTrangThaiXeResponse?> UpdateTrangThaiXeAsync(int maPhuongTien, UpdateTrangThaiXeDto model);

        Task<PhuongTienPagedResponse?> GetDanhSachXeTheoKhoKhoiLuongAsync(int? maKho, double? khoiLuongCan, int page = 1);
    }

    public interface IKhachHangServiceClient
    {
        Task<KhachHangSummaryDto?> GetChiTietKhachHangAsync(int maKhachHang);
    }

    public interface IKhoBaiService
    {
        Task<Dictionary<int, KhoTimDuocDto>?> TimKhoTheoLoAsync(BatchKhoRequest request);
        Task<KhoBaiDetailModel?> GetChiTietKhoBaiAsync(int maKho);
    }

}
