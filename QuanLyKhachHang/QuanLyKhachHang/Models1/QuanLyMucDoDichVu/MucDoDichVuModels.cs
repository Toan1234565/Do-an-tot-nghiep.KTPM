namespace QuanLyKhachHang.Models1.QuanLyMucDoDichVu
{
    public class MucDoDichVuModels
    {
        public int MaDichVu { get; set; }

        public string? TenDichVu { get; set; }

        public decimal? ThoiGianCamKet { get; set; }

        public decimal? HeSoNhiPhan { get; set; }

        public bool? LaCaoCap { get; set; }
        public bool? TrangThai { get; set; }

        public DateTime? NgayBatDau { get; set; }

        public DateTime? NgayKetThuc { get; set; }
    }
}
