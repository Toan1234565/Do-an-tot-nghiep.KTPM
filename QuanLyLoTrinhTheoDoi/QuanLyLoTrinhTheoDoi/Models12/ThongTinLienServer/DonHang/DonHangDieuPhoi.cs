namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang
{
    // Request cho API gom nhóm H3
    public class ClusterRequest
    {
        public int? MinOrdersPerCluster { get; set; }
        public string TrangThaiDonHang { get; internal set; }
    }

    // Response trả về từng cụm từ API gom nhóm H3
    public class ClusterResult
    {
        public string MaVungH3 { get; set; } = string.Empty;
        public int SoLuongDonHang { get; set; }
        public int MaDiaChiLayHang { get; set; }
        public int MaDiaChiCum { get; set; }
        public int MaDiaChiNhanHang { get; set; }
        public List<int> DanhSachMaDonHang { get; set; } = new();
        public double TongKhoiLuong { get; set; }
        public double TongTheTich { get; set; }
    }

    // Response tổng thể của API gom nhóm
    public class ClusterResponseModel
    {
        public int TotalClusters { get; set; }
        public int TotalOrders { get; set; }
        public List<ClusterResult> Clusters { get; set; } = new();
    }

    // Request cho API cập nhật trạng thái hàng loạt
    public class UpdateMultiStatusRequest
    {
        public List<int> DanhSachMaDonHang { get; set; } = new();
        public string TrangThaiMoi { get; set; } = string.Empty;
    }

    // Response trả về từ API cập nhật trạng thái
    public class UpdateMultiStatusResponseModel
    {
        public string Message { get; set; } = string.Empty;
        public List<int> UpdatedIds { get; set; } = new();
    }
}
