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

    public virtual DbSet<CaLamViec> CaLamViecs { get; set; }

    public virtual DbSet<ChucVu> ChucVus { get; set; }

    public virtual DbSet<DangKyCaTruc> DangKyCaTrucs { get; set; }

    public virtual DbSet<LichSuViPham> LichSuViPhams { get; set; }

    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<TaiXe> TaiXes { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Danh_Tinh_Truy_Cap_DB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaLamViec>(entity =>
        {
            entity.HasKey(e => e.MaCa).HasName("PK__Ca_Lam_V__0FE176ED17721A7D");

            entity.ToTable("Ca_Lam_Viec");

            entity.Property(e => e.MaCa).HasColumnName("ma_ca");
            entity.Property(e => e.GioBatDau).HasColumnName("gio_bat_dau");
            entity.Property(e => e.GioKetThuc).HasColumnName("gio_ket_thuc");
            entity.Property(e => e.TenCa)
                .HasMaxLength(50)
                .HasColumnName("ten_ca");
        });

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

        modelBuilder.Entity<DangKyCaTruc>(entity =>
        {
            entity.HasKey(e => e.MaDangKy).HasName("PK__DangKyCa__BC05F6F15CC1B50E");

            entity.ToTable("DangKyCaTruc");

            entity.Property(e => e.MaDangKy).HasColumnName("ma_dang_ky");
            entity.Property(e => e.MaCa).HasColumnName("ma_ca");
            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.NgayTruc).HasColumnName("ngay_truc");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaCaNavigation).WithMany(p => p.DangKyCaTrucs)
                .HasForeignKey(d => d.MaCa)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKy_Ca");

            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.DangKyCaTrucs)
                .HasForeignKey(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKyCaTruc_NguoiDung");

            entity.HasOne(d => d.MaNguoiDung1).WithMany(p => p.DangKyCaTrucs)
                .HasForeignKey(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKy_TaiXe");
        });

        modelBuilder.Entity<LichSuViPham>(entity =>
        {
            entity.HasKey(e => e.MaViPham).HasName("PK__LichSuVi__F1921D89CDDE579B");

            entity.ToTable("LichSuViPham");

            entity.Property(e => e.HinhThucXuLy).HasMaxLength(100);
            entity.Property(e => e.LoaiViPham).HasMaxLength(255);
            entity.Property(e => e.MucPhat).HasColumnType("money");
            entity.Property(e => e.NgayViPham)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThaiXuLy)
                .HasMaxLength(50)
                .HasDefaultValue("Chưa xử lý");

            entity.HasOne(d => d.MaTaiXeNavigation).WithMany(p => p.LichSuViPhams)
                .HasForeignKey(d => d.MaTaiXe)
                .HasConstraintName("FK_ViPham_TaiXe");

            entity.HasOne(d => d.NguoiLapBienBanNavigation).WithMany(p => p.LichSuViPhams)
                .HasForeignKey(d => d.NguoiLapBienBan)
                .HasConstraintName("FK_ViPham_QuanLy");
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
            entity.Property(e => e.MaKho).HasColumnName("ma_kho");
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

            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.HoatDong).HasColumnName("hoat_dong");
            entity.Property(e => e.MatKhauHash)
                .HasMaxLength(255)
                .HasColumnName("mat_khau_hash");
            entity.Property(e => e.TenDangNhap)
                .HasMaxLength(50)
                .HasColumnName("ten_dang_nhap");
        });

        modelBuilder.Entity<TaiXe>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__TaiXe__19C32CF7F6D5092D");

            entity.ToTable("TaiXe");

            entity.HasIndex(e => e.SoBangLai, "UQ__TaiXe__81406DF39CD14C55").IsUnique();

            entity.Property(e => e.MaNguoiDung)
                .ValueGeneratedNever()
                .HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.AnhBangLaiSau).HasColumnName("anh_bang_lai_sau");
            entity.Property(e => e.AnhBangLaiTruoc).HasColumnName("anh_bang_lai_truoc");
            entity.Property(e => e.DiemUyTin)
                .HasDefaultValue(5.0m)
                .HasColumnType("decimal(3, 2)")
                .HasColumnName("diem_uy_tin");
            entity.Property(e => e.KinhNghiemNam)
                .HasDefaultValue(0)
                .HasColumnName("kinh_nghiem_nam");
            entity.Property(e => e.LoaiBangLai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("loai_bang_lai");
            entity.Property(e => e.NgayCapBang).HasColumnName("ngay_cap_bang");
            entity.Property(e => e.NgayHetHanBang).HasColumnName("ngay_het_han_bang");
            entity.Property(e => e.SoBangLai)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("so_bang_lai");
            entity.Property(e => e.TrangThaiHoatDong)
                .HasMaxLength(50)
                .HasDefaultValue("Sẵn sàng")
                .HasColumnName("trang_thai_hoat_dong");

            entity.HasOne(d => d.MaNguoiDungNavigation).WithOne(p => p.TaiXe)
                .HasForeignKey<TaiXe>(d => d.MaNguoiDung)
                .HasConstraintName("FK_TaiXe_NguoiDung");
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
