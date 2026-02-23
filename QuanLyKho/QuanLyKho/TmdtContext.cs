using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Models;

namespace QuanLyKho;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DangKiem> DangKiems { get; set; }

    public virtual DbSet<DinhMucBaoTri> DinhMucBaoTris { get; set; }

    public virtual DbSet<KhoBai> KhoBais { get; set; }

    public virtual DbSet<LichSuBaoTri> LichSuBaoTris { get; set; }

    public virtual DbSet<LoaiKho> LoaiKhos { get; set; }

    public virtual DbSet<LoaiXe> LoaiXes { get; set; }

    public virtual DbSet<PhuongTien> PhuongTiens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Tai_San_Kho_Bai_DB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DangKiem>(entity =>
        {
            entity.HasKey(e => e.IdDangKiem).HasName("PK__Dang_Kie__51FB7C9F67B5F6AA");

            entity.ToTable("Dang_Kiem");

            entity.Property(e => e.IdDangKiem).HasColumnName("id_dang_kiem");
            entity.Property(e => e.DonViKiemDinh)
                .HasMaxLength(255)
                .HasColumnName("don_vi_kiem_dinh");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.HinhAnhDangKiem).HasColumnName("hinh_anh_dang_kiem");
            entity.Property(e => e.MaPhuongTien).HasColumnName("ma_phuong_tien");
            entity.Property(e => e.NgayHetHan).HasColumnName("ngay_het_han");
            entity.Property(e => e.NgayKiemDinh).HasColumnName("ngay_kiem_dinh");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.PhiDuongBoDenNgay).HasColumnName("phi_duong_bo_den_ngay");
            entity.Property(e => e.SoSeriGiayPhep)
                .HasMaxLength(50)
                .HasColumnName("so_seri_giay_phep");
            entity.Property(e => e.SoTemKiemDinh)
                .HasMaxLength(50)
                .HasColumnName("so_tem_kiem_dinh");

            entity.HasOne(d => d.MaPhuongTienNavigation).WithMany(p => p.DangKiems)
                .HasForeignKey(d => d.MaPhuongTien)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKiem_PhuongTien");
        });

        modelBuilder.Entity<DinhMucBaoTri>(entity =>
        {
            entity.HasKey(e => e.MaDinhMuc).HasName("PK__Dinh_Muc__97FEC21D6C374292");

            entity.ToTable("Dinh_Muc_Bao_Tri");

            entity.Property(e => e.MaDinhMuc).HasColumnName("ma_dinh_muc");
            entity.Property(e => e.DinhMucKm).HasColumnName("dinh_muc_km");
            entity.Property(e => e.DinhMucThang).HasColumnName("dinh_muc_thang");
            entity.Property(e => e.MaLoaiXe).HasColumnName("ma_loai_xe");
            entity.Property(e => e.TenHangMuc)
                .HasMaxLength(100)
                .HasColumnName("ten_hang_muc");

            entity.HasOne(d => d.MaLoaiXeNavigation).WithMany(p => p.DinhMucBaoTris)
                .HasForeignKey(d => d.MaLoaiXe)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DinhMuc_LoaiXe");
        });

        modelBuilder.Entity<KhoBai>(entity =>
        {
            entity.HasKey(e => e.MaKho).HasName("PK__Kho_Bai__0A2E13A417B8BBDC");

            entity.ToTable("Kho_Bai");

            entity.Property(e => e.MaKho).HasColumnName("ma_kho");
            entity.Property(e => e.DienTichM2)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("dien_tich_m2");
            entity.Property(e => e.DungTichM3).HasColumnName("dung_tich_m3");
            entity.Property(e => e.MaDiaChi).HasColumnName("ma_dia_chi");
            entity.Property(e => e.MaLoaiKho).HasColumnName("ma_loai_kho");
            entity.Property(e => e.MaQuanLy).HasColumnName("ma_quan_ly");
            entity.Property(e => e.SoDienThoaiKho)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("so_dien_thoai_kho");
            entity.Property(e => e.SucChua)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("suc_chua");
            entity.Property(e => e.TenKhoBai)
                .HasMaxLength(30)
                .HasColumnName("ten_kho_bai");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaLoaiKhoNavigation).WithMany(p => p.KhoBais)
                .HasForeignKey(d => d.MaLoaiKho)
                .HasConstraintName("FK_KhoBai_LoaiKho");
        });

        modelBuilder.Entity<LichSuBaoTri>(entity =>
        {
            entity.HasKey(e => e.MaBanGhi).HasName("PK__Lich_Su___C52FD8AD59376ACA");

            entity.ToTable("Lich_Su_Bao_Tri");

            entity.Property(e => e.MaBanGhi).HasColumnName("ma_ban_ghi");
            entity.Property(e => e.ChiPhi)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("chi_phi");
            entity.Property(e => e.MaDinhMuc).HasColumnName("ma_dinh_muc");
            entity.Property(e => e.MaPhuongTien).HasColumnName("ma_phuong_tien");
            entity.Property(e => e.Ngay).HasColumnName("ngay");
            entity.Property(e => e.SoKmThucTe)
                .HasDefaultValue(0.0)
                .HasColumnName("so_km_thuc_te");

            entity.HasOne(d => d.MaDinhMucNavigation).WithMany(p => p.LichSuBaoTris)
                .HasForeignKey(d => d.MaDinhMuc)
                .HasConstraintName("FK_LichSu_DinhMuc");

            entity.HasOne(d => d.MaPhuongTienNavigation).WithMany(p => p.LichSuBaoTris)
                .HasForeignKey(d => d.MaPhuongTien)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BaoTri_PhuongTien");
        });

        modelBuilder.Entity<LoaiKho>(entity =>
        {
            entity.HasKey(e => e.MaLoaiKho).HasName("PK__Loai_Kho__EE258B59E754B0AA");

            entity.ToTable("Loai_Kho");

            entity.Property(e => e.MaLoaiKho).HasColumnName("ma_loai_kho");
            entity.Property(e => e.GhiChu)
                .HasMaxLength(255)
                .HasColumnName("ghi_chu");
            entity.Property(e => e.TenLoaiKho)
                .HasMaxLength(100)
                .HasColumnName("ten_loai_kho");
        });

        modelBuilder.Entity<LoaiXe>(entity =>
        {
            entity.HasKey(e => e.MaLoaiXe).HasName("PK__Loai_Xe__1E1E213AC0E67575");

            entity.ToTable("Loai_Xe");

            entity.Property(e => e.MaLoaiXe).HasColumnName("ma_loai_xe");
            entity.Property(e => e.TenLoai)
                .HasMaxLength(50)
                .HasColumnName("ten_loai");
        });

        modelBuilder.Entity<PhuongTien>(entity =>
        {
            entity.HasKey(e => e.MaPhuongTien).HasName("PK__Phuong_T__2CDFA6B35491CF4F");

            entity.ToTable("Phuong_Tien");

            entity.HasIndex(e => e.BienSo, "UQ__Phuong_T__53E92E58B382F698").IsUnique();

            entity.Property(e => e.MaPhuongTien).HasColumnName("ma_phuong_tien");
            entity.Property(e => e.BienSo)
                .HasMaxLength(50)
                .HasColumnName("bien_so");
            entity.Property(e => e.MaKho).HasColumnName("ma_kho");
            entity.Property(e => e.MaLoaiXe).HasColumnName("ma_loai_xe");
            entity.Property(e => e.MucTieuHaoNhienLieu).HasColumnName("muc_tieu_hao_nhien_lieu");
            entity.Property(e => e.SoKmHienTai)
                .HasDefaultValue(0.0)
                .HasColumnName("so_km_hien_tai");
            entity.Property(e => e.TaiTrongToiDaKg).HasColumnName("tai_trong_toi_da_kg");
            entity.Property(e => e.TheTichToiDaM3).HasColumnName("the_tich_toi_da_m3");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaKhoNavigation).WithMany(p => p.PhuongTiens)
                .HasForeignKey(d => d.MaKho)
                .HasConstraintName("FK_PhuongTien_KhoBai");

            entity.HasOne(d => d.MaLoaiXeNavigation).WithMany(p => p.PhuongTiens)
                .HasForeignKey(d => d.MaLoaiXe)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PhuongTien_LoaiXe");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
