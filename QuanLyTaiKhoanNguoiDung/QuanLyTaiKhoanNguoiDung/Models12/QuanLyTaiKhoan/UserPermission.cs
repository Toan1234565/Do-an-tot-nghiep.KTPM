namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyTaiKhoan
{
    public class UserPermission
    {
        public int UserId { get; set; }
        public string HoTen { get; set; } = "";
        public int? MaKho { get; set; }
        public bool IsQuanLyTong { get; set; }
        public bool IsQuanLyKho { get; set; }
        public string TenVaiTro { get; set; } = "";

        // Trả về mã kho cuối cùng sau khi đã áp dụng logic phân quyền
        public int? GetFinalMaKho(int? maKhoRequest)
        {
            if (IsQuanLyTong) return maKhoRequest ?? 11; // Admin chọn kho nào lấy kho đó
            return MaKho; // Quản lý kho chỉ được xem kho của mình
        }
    }
}
