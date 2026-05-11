namespace QuanLyLoTrinhTheoDoi.Models12.QuanLyLoTrinh.cs
{
    public class PhuongTienTaiXeModels
    {
        public int MaPhuongTien { get; set; }

        public string? LoaiTuyen { get; set; }      

        public int MaNguoiDung { get; set; }       

        public int? MaNguoiDungPhu { get; set; }
        public int? MaCa { get; internal set; }
    }
}
