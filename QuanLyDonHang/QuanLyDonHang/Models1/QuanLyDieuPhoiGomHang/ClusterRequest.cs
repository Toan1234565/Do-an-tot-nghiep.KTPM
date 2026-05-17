namespace QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang
{
    public class ClusterRequest
    {
        public int? MaKhoTrungTam { get; set; }
        public decimal? BanKinhToiDaKm { get; set; } // Dành cho thuật toán nâng cao
                                                     // Cấu hình chặng: "Chờ lấy hàng" hoặc "Chờ trung chuyển" hoặc "Chờ giao hàng"
        public string? TrangThaiDonHang { get; set; }

        // ID kho bấm lệnh điều phối (Ví dụ: Kho tổng 11 muốn lọc đơn tại kho của mình để đi tiếp)
        public int MaKhoQuanLyHienTai { get; set; }

        public int? MinOrdersPerCluster { get; set; }
    }
}
