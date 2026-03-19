namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec
{
    public class DangKyCaTrucViewModel
    {
        public int MaDangKy { get; set; }
        public string? TenTaiXe { get; set; }
        public int? MaCa { get; set; }
        public string? TenCa { get; set; }
        public DateOnly NgayTruc { get; set; }
        public string? TrangThai { get; set; }
        public virtual CaLamViecModels? MaCaNavigation { get; set; }

        // --- Các trường AI bổ sung ---
        public double AI_Score { get; set; } // Điểm từ 0 đến 100
        public string? AI_Recommendation { get; set; } // Gợi ý: "Khuyên dùng", "Cảnh báo", "Từ chối"
        public List<string>? AI_Reasons { get; set; } // Lý do AI đưa ra quyết định
    }
}
