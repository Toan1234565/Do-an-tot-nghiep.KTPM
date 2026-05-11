using QuanLyDonHang.Models1.ServerKhachHang;

namespace QuanLyDonHang.Models1
{
    namespace QuanLyDonHang.Models1
    {
        // Đại diện cho Server Khách hàng
        public interface IKhachHangService
        {
            Task<int> CheckSoDienThoaiAsync(string sdt, string ten, dynamic diaChi);
            Task<(decimal soTienGiam, int? maKhuyenMai)> ApDungKhuyenMaiAsync(string code, decimal tongTien, int maKhachHang);
            Task<KhachHangModels?> GetChiTietKhachHangAsync(int maKhachHang);
        }

        // Đại diện cho Server Địa chỉ
        public interface IDiaChiService
        {
            Task<(int maDiaChi, string maVungH3)> CheckDiaChiAsync(dynamic diaChiReq);
            Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi);
        }

        // Đại diện cho Server Kho bãi
        public interface IKhoBaiService
        {
            Task<int?> TimKhoGanNhatAsync(int maDiaChi);
        }
    }
}
