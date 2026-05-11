namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class CaTrucConfig
    {
        public int MaCa { get; set; }
        public string TenCa { get; set; }
        public TimeOnly GioBatDau { get; set; }
        public TimeOnly GioKetThuc { get; set; }
        public int Priority { get; set; } // Độ ưu tiên để xử lý trùng giờ
    }
}
