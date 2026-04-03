namespace QuanLyKho.Models1.QuanLyKho
{
    public class BatchKhoRequest
    {
        public List<int> MaDiaChis { get; set; } = new List<int>();
    }

    public class ToaDoResponseDto
    {
        public int MaDiaChi { get; set; }
        public double? ViDo { get; set; }
        public double? KinhDo { get; set; }
        public string? MaVungH3 { get; set; }
    }
}
