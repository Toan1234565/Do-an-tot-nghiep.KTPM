using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyTaiKhoanNguoiDung.Models;

namespace QuanLyTaiKhoanNguoiDung;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChucVu> ChucVus { get; set; }

    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Danh_Tinh_Truy_Cap_DB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChucVu>(entity =>
        {
            entity.HasKey(e => e.MaChucVu).HasName("PK__Chuc_Vu__41374AC99F9E1185");

            entity.ToTable("Chuc_Vu");

            entity.Property(e => e.MaChucVu).HasColumnName("ma_chuc_vu");
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.TenChucVu)
                .HasMaxLength(50)
                .HasColumnName("ten_chuc_vu");

            entity.HasOne(d => d.MaVaiTroNavigation).WithMany(p => p.ChucVus)
                .HasForeignKey(d => d.MaVaiTro)
                .HasConstraintName("FK_ChucVu_VaiTro");
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__Nguoi_Du__6781B7B93D33D06A");

            entity.ToTable("Nguoi_Dung");

            entity.Property(e => e.MaNguoiDung)
                .ValueGeneratedNever()
                .HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.BaoHiemXaHoi)
                .HasMaxLength(255)
                .HasColumnName("bao_hiem_xa_hoi");
            entity.Property(e => e.DonViLamViec)
                .HasMaxLength(30)
                .HasColumnName("don_vi_lam_viec");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.GioiTinh)
                .HasMaxLength(5)
                .HasColumnName("gioi_tinh");
            entity.Property(e => e.HoTenNhanVien)
                .HasMaxLength(100)
                .HasColumnName("ho_ten_nhan_vien");
            entity.Property(e => e.MaChucVu).HasColumnName("ma_chuc_vu");
            entity.Property(e => e.MaDiaChi).HasColumnName("ma_dia_chi");
            entity.Property(e => e.NgaySinh).HasColumnName("ngay_sinh");
            entity.Property(e => e.NoiSinh)
                .HasMaxLength(50)
                .HasColumnName("noi_sinh");
            entity.Property(e => e.SoCccd)
                .HasMaxLength(255)
                .HasColumnName("so_cccd");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.SoTaiKhoan)
                .HasMaxLength(255)
                .HasColumnName("so_tai_khoan");
            entity.Property(e => e.TenNganHang)
                .HasMaxLength(20)
                .HasColumnName("ten_ngan_hang");

            entity.HasOne(d => d.MaChucVuNavigation).WithMany(p => p.NguoiDungs)
                .HasForeignKey(d => d.MaChucVu)
                .HasConstraintName("FK_NguoiDung_ChucVu");

            entity.HasOne(d => d.MaNguoiDungNavigation).WithOne(p => p.NguoiDung)
                .HasForeignKey<NguoiDung>(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NguoiDung_TaiKhoan");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__Tai_Khoa__19C32CF794D557E3");

            entity.ToTable("Tai_Khoan");

            entity.HasIndex(e => e.TenDangNhap, "UQ__Tai_Khoa__363698B3642AE707").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Tai_Khoa__AB6E6164CB10A69A").IsUnique();

            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.HoatDong).HasColumnName("hoat_dong");
            entity.Property(e => e.MatKhauHash)
                .HasMaxLength(255)
                .HasColumnName("mat_khau_hash");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.TenDangNhap)
                .HasMaxLength(50)
                .HasColumnName("ten_dang_nhap");
        });

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.HasKey(e => e.MaVaiTro).HasName("PK__Vai_Tro__4AE1754DFA91EA3E");

            entity.ToTable("Vai_Tro");

            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.QuyenHan).HasColumnName("quyen_han");
            entity.Property(e => e.TenVaiTro)
                .HasMaxLength(50)
                .HasColumnName("ten_vai_tro");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
