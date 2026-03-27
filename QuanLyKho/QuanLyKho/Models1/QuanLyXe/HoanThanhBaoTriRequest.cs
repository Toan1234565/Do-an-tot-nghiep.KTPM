namespace QuanLyKho.Models1.QuanLyXe
{
    public class HoanThanhBaoTriRequest
    {
        public string? BienSoXe { get; set; } 
        public int MaPhuongTien { get; set; }
        public DateTime NgayBaoTri { get; set; }
        public double SoKmThucTe { get; set; }
        public string? GhiChu { get; set; }
        public List<ChiPhiHangMuc>? ChiPhiChiTiet { get; set; }
    }

    public class ChiPhiHangMuc
    {
        public int MaDinhMuc { get; set; }
        public decimal ChiPhi { get; set; }
    }
}
