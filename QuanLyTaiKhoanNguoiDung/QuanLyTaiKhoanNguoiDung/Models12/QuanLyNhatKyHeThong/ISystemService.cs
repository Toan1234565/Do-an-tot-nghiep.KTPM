namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong
{
    public interface ISystemService
    {
        // Hàm dùng chung để ghi log và reset cache
        Task GhiLogVaResetCacheAsync(string dichVu, string thaoTac, string bang, string maDoiTuong, object dataCu, object dataMoi);

        // Hàm lấy ID người dùng đang đăng nhập
        int? GetCurrentUserId();
    }
}
