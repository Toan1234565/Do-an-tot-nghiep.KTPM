using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CauHinhTichDiem> CauHinhTichDiems { get; set; }

    public virtual DbSet<DiaChi> DiaChis { get; set; }

    public virtual DbSet<DiemThuong> DiemThuongs { get; set; }

    public virtual DbSet<HopDongVanChuyen> HopDongVanChuyens { get; set; }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<KhuyenMai> KhuyenMais { get; set; }

    public virtual DbSet<LichSuDungMa> LichSuDungMas { get; set; }

    public virtual DbSet<LoaiKhuyenMai> LoaiKhuyenMais { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Khach_Hang_Gia_Cuoc_DB;Integrated Security=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CauHinhTichDiem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CauHinhT__3214EC073C777179");

            entity.ToTable("CauHinhTichDiem");

            entity.Property(e => e.ChoPhepDungDiem).HasDefaultValue(true);
            entity.Property(e => e.DiemToiThieuDeDung).HasDefaultValue(10);
            entity.Property(e => e.GiaTriDiem)
                .HasDefaultValue(1000m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TyLeTichDiem)
                .HasDefaultValue(10000m)
                .HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<DiaChi>(entity =>
        {
            entity.HasKey(e => e.MaDiaChi).HasName("PK__Dia_Chi__804398590B7D5085");

            entity.ToTable("Dia_Chi");

            entity.HasIndex(e => e.MaVungH3, "IX_DiaChi_H3");

            entity.Property(e => e.MaDiaChi).HasColumnName("ma_dia_chi");
            entity.Property(e => e.Duong)
                .HasMaxLength(255)
                .HasColumnName("duong");
            entity.Property(e => e.KinhDo).HasColumnName("kinh_do");
            entity.Property(e => e.MaVungH3)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ma_vung_h3");
            entity.Property(e => e.Phuong)
                .HasMaxLength(100)
                .HasColumnName("phuong");
            entity.Property(e => e.ThanhPho)
                .HasMaxLength(100)
                .HasColumnName("thanh_pho");
            entity.Property(e => e.ViDo).HasColumnName("vi_do");
        });

        modelBuilder.Entity<DiemThuong>(entity =>
        {
            entity.HasKey(e => e.MaDiem).HasName("PK__Diem_Thu__8CA8330D5F7A3ECC");

            entity.ToTable("Diem_Thuong");

            entity.Property(e => e.MaDiem).HasColumnName("ma_diem");
            entity.Property(e => e.DiemDaDung)
                .HasDefaultValue(0)
                .HasColumnName("diem_da_dung");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.NgayCapNhatCuoi)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_cap_nhat_cuoi");
            entity.Property(e => e.TongDiemTichLuy)
                .HasDefaultValue(0)
                .HasColumnName("tong_diem_tich_luy");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.DiemThuongs)
                .HasForeignKey(d => d.MaKhachHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiemThuong_KhachHang");
        });

        modelBuilder.Entity<HopDongVanChuyen>(entity =>
        {
            entity.HasKey(e => e.MaHopDong).HasName("PK__Hop_Dong__D499F6F439BBBC3D");

            entity.ToTable("Hop_Dong_Van_Chuyen");

            entity.Property(e => e.MaHopDong).HasColumnName("ma_hop_dong");
            entity.Property(e => e.FileHopDong).HasColumnName("file_hop_dong");
            entity.Property(e => e.LoaiHangHoa)
                .HasMaxLength(100)
                .HasColumnName("loai_hang_hoa");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.NgayHetHan)
                .HasColumnType("datetime")
                .HasColumnName("ngay_het_han");
            entity.Property(e => e.NgayKy)
                .HasColumnType("datetime")
                .HasColumnName("ngay_ky");
            entity.Property(e => e.TenFileGoc)
                .HasMaxLength(255)
                .HasColumnName("ten_file_goc");
            entity.Property(e => e.TenHopDong)
                .HasMaxLength(200)
                .HasColumnName("ten_hop_dong");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.HopDongVanChuyens)
                .HasForeignKey(d => d.MaKhachHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HD_KhachHang");
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.HasKey(e => e.MaKhachHang).HasName("PK__Khach_Ha__C9817AF66DEB8FD3");

            entity.ToTable("Khach_Hang");

            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.MaDiaChiMacDinh).HasColumnName("ma_dia_chi_mac_dinh");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.TenCongTy)
                .HasMaxLength(255)
                .HasColumnName("ten_cong_ty");
            entity.Property(e => e.TenLienHe)
                .HasMaxLength(100)
                .HasColumnName("ten_lien_he");

            entity.HasOne(d => d.MaDiaChiMacDinhNavigation).WithMany(p => p.KhachHangs)
                .HasForeignKey(d => d.MaDiaChiMacDinh)
                .HasConstraintName("FK_KhachHang_DiaChi");
        });

        modelBuilder.Entity<KhuyenMai>(entity =>
        {
            entity.HasKey(e => e.MaKhuyenMai).HasName("PK__Khuyen_M__01A88CB313349CC3");

            entity.ToTable("Khuyen_Mai");

            entity.HasIndex(e => e.CodeKhuyenMai, "UQ__Khuyen_M__D029508ECE338D78").IsUnique();

            entity.Property(e => e.MaKhuyenMai).HasColumnName("ma_khuyen_mai");
            entity.Property(e => e.CodeKhuyenMai)
                .HasMaxLength(50)
                .HasColumnName("code_khuyen_mai");
            entity.Property(e => e.DonHangToiThieu)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("don_hang_toi_thieu");
            entity.Property(e => e.GiaTriGiam)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("gia_tri_giam");
            entity.Property(e => e.GiamToiDa)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("giam_toi_da");
            entity.Property(e => e.KieuGiamGia)
                .HasMaxLength(20)
                .HasColumnName("kieu_giam_gia");
            entity.Property(e => e.MaLoaiKm).HasColumnName("ma_loai_km");
            entity.Property(e => e.NgayBatDau)
                .HasColumnType("datetime")
                .HasColumnName("ngay_bat_dau");
            entity.Property(e => e.NgayKetThuc)
                .HasColumnType("datetime")
                .HasColumnName("ngay_ket_thuc");
            entity.Property(e => e.SoLuongDaDung)
                .HasDefaultValue(0)
                .HasColumnName("so_luong_da_dung");
            entity.Property(e => e.SoLuongToiDa).HasColumnName("so_luong_toi_da");
            entity.Property(e => e.TenChuongTrinh)
                .HasMaxLength(255)
                .HasColumnName("ten_chuong_trinh");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaLoaiKmNavigation).WithMany(p => p.KhuyenMais)
                .HasForeignKey(d => d.MaLoaiKm)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KhuyenMai_LoaiKM");
        });

        modelBuilder.Entity<LichSuDungMa>(entity =>
        {
            entity.HasKey(e => e.MaLichSu).HasName("PK__Lich_Su___4C9D7F29769FB42D");

            entity.ToTable("Lich_Su_Dung_Ma");

            entity.Property(e => e.MaLichSu).HasColumnName("ma_lich_su");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.MaKhuyenMai).HasColumnName("ma_khuyen_mai");
            entity.Property(e => e.NgaySuDung)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_su_dung");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.LichSuDungMas)
                .HasForeignKey(d => d.MaKhachHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LichSu_KhachHang");

            entity.HasOne(d => d.MaKhuyenMaiNavigation).WithMany(p => p.LichSuDungMas)
                .HasForeignKey(d => d.MaKhuyenMai)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LichSu_KhuyenMai");
        });

        modelBuilder.Entity<LoaiKhuyenMai>(entity =>
        {
            entity.HasKey(e => e.MaLoaiKm).HasName("PK__Loai_Khu__1EE19B0D076E3A44");

            entity.ToTable("Loai_Khuyen_Mai");

            entity.Property(e => e.MaLoaiKm).HasColumnName("ma_loai_km");
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.MoTa).HasColumnName("mo_ta");
            entity.Property(e => e.TenLoai)
                .HasMaxLength(100)
                .HasColumnName("ten_loai");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
