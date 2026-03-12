namespace QuanLyKho.Models1.QuanLyKho
{
    public class ToaDoDto
    {
        public double ViDo { get; set; }
        public double KinhDo { get; set; }
    }

    public class ToaDoResponseDto
    {
        public int MaDiaChi { get; set; }
        public double? ViDo { get; set; }
        public double? KinhDo { get; set; }
    }
}
