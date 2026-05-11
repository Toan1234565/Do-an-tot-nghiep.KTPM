namespace QuanLyDonHang.Models1.QuanLyMucDoDichVu
{
    public class MucDoDichVuCReate
    {
        public int MaDichVu { get; set; }

        public string TenDichVu { get; set; } = null!;

        public string? ThoiGianCamKet { get; set; }

        public double? HeSoNhiPhan { get; set; }

        public bool? LaCaoCap { get; set; }

        public bool? TrangThai { get; set; }

        public DateOnly? NgayBatDau { get; set; }

        public DateOnly? NgayKetThuc { get; set; }
    }
}
