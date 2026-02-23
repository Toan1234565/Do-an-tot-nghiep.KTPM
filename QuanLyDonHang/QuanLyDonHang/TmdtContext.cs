using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyDonHang.Models;

namespace QuanLyDonHang;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BangChungGiaoHang> BangChungGiaoHangs { get; set; }

    public virtual DbSet<CapNhatTrangThai> CapNhatTrangThais { get; set; }

    public virtual DbSet<DanhMucLoaiHang> DanhMucLoaiHangs { get; set; }

    public virtual DbSet<DonHang> DonHangs { get; set; }

    public virtual DbSet<KienHang> KienHangs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=DON_HANG;Integrated Security=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BangChungGiaoHang>(entity =>
        {
            entity.HasKey(e => e.MaPod).HasName("PK__Bang_Chu__0A6B0DE1AFE9361B");

            entity.ToTable("Bang_Chung_Giao_Hang");

            entity.HasIndex(e => e.MaKienHang, "UQ__Bang_Chu__C4D5465C13FB0F56").IsUnique();

            entity.Property(e => e.MaPod).HasColumnName("ma_pod");
            entity.Property(e => e.MaKienHang).HasColumnName("ma_kien_hang");
            entity.Property(e => e.TenNguoiNhan)
                .HasMaxLength(100)
                .HasColumnName("ten_nguoi_nhan");
            entity.Property(e => e.ThoiGian)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian");
            entity.Property(e => e.UrlAnh).HasColumnName("url_anh");
            entity.Property(e => e.UrlChuKy).HasColumnName("url_chu_ky");

            entity.HasOne(d => d.MaKienHangNavigation).WithOne(p => p.BangChungGiaoHang)
                .HasForeignKey<BangChungGiaoHang>(d => d.MaKienHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POD_KienHang");
        });

        modelBuilder.Entity<CapNhatTrangThai>(entity =>
        {
            entity.HasKey(e => e.MaCapNhat).HasName("PK__Cap_Nhat__C76BB74AE34AEF1C");

            entity.ToTable("Cap_Nhat_Trang_Thai");

            entity.Property(e => e.MaCapNhat).HasColumnName("ma_cap_nhat");
            entity.Property(e => e.BaoCaoBoi).HasColumnName("bao_cao_boi");
            entity.Property(e => e.KinhDo).HasColumnName("kinh_do");
            entity.Property(e => e.MaKienHang).HasColumnName("ma_kien_hang");
            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.ThoiGian)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian");
            entity.Property(e => e.TrangThaiMoi)
                .HasMaxLength(50)
                .HasColumnName("trang_thai_moi");
            entity.Property(e => e.ViDo).HasColumnName("vi_do");

            entity.HasOne(d => d.MaKienHangNavigation).WithMany(p => p.CapNhatTrangThais)
                .HasForeignKey(d => d.MaKienHang)
                .HasConstraintName("FK_CapNhat_KienHang");
        });

        modelBuilder.Entity<DanhMucLoaiHang>(entity =>
        {
            entity.HasKey(e => e.MaLoaiHang).HasName("PK__Danh_Muc__B553D6004C4393C5");

            entity.ToTable("Danh_Muc_Loai_Hang");

            entity.Property(e => e.MaLoaiHang).HasColumnName("ma_loai_hang");
            entity.Property(e => e.MoTa)
                .HasMaxLength(255)
                .HasColumnName("mo_ta");
            entity.Property(e => e.TenLoaiHang)
                .HasMaxLength(100)
                .HasColumnName("ten_loai_hang");
        });

        modelBuilder.Entity<DonHang>(entity =>
        {
            entity.HasKey(e => e.MaDonHang).HasName("PK__Don_Hang__0246C5EA3FF89D88");

            entity.ToTable("Don_Hang");

            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.GhiChuDacBiet).HasColumnName("ghi_chu_dac_biet");
            entity.Property(e => e.LaDonGiaoThang)
                .HasDefaultValue(false)
                .HasColumnName("la_don_giao_thang");
            entity.Property(e => e.MaDiaChiGiao).HasColumnName("ma_dia_chi_giao");
            entity.Property(e => e.MaDiaChiNhanHang).HasColumnName("ma_dia_chi_nhan_hang");
            entity.Property(e => e.MaHopDongNgoai).HasColumnName("ma_hop_dong_ngoai");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.MaLoaiDv)
                .HasDefaultValue(1)
                .HasColumnName("ma_loai_dv");
            entity.Property(e => e.MaMucDoDv).HasColumnName("ma_muc_do_dv");
            entity.Property(e => e.MaVung).HasColumnName("ma_vung");
            entity.Property(e => e.SdtNguoiNhan)
                .HasMaxLength(20)
                .HasColumnName("sdt_nguoi_nhan");
            entity.Property(e => e.TenDonHang)
                .HasMaxLength(255)
                .HasColumnName("ten_don_hang");
            entity.Property(e => e.TenNguoiNhan)
                .HasMaxLength(100)
                .HasColumnName("ten_nguoi_nhan");
            entity.Property(e => e.ThoiGianTao)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_tao");
            entity.Property(e => e.TongTienDuKien)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("tong_tien_du_kien");
            entity.Property(e => e.TongTienThucTe)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("tong_tien_thuc_te");
            entity.Property(e => e.TrangThaiHienTai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai_hien_tai");
        });

        modelBuilder.Entity<KienHang>(entity =>
        {
            entity.HasKey(e => e.MaKienHang).HasName("PK__Kien_Han__C4D5465D9CCBE235");

            entity.ToTable("Kien_Hang");

            entity.HasIndex(e => e.MaVach, "UQ__Kien_Han__DE70626347024EA8").IsUnique();

            entity.Property(e => e.MaKienHang).HasColumnName("ma_kien_hang");
            entity.Property(e => e.DaThanhToan).HasColumnName("da_thanh_toan");
            entity.Property(e => e.DaThuGom).HasColumnName("da_thu_gom");
            entity.Property(e => e.KhoiLuong).HasColumnName("khoi_luong");
            entity.Property(e => e.MaBangGiaVung).HasColumnName("ma_bang_gia_vung");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaKhoHienTai).HasColumnName("ma_kho_hien_tai");
            entity.Property(e => e.MaLoaiHang).HasColumnName("ma_loai_hang");
            entity.Property(e => e.MaVach)
                .HasMaxLength(50)
                .HasColumnName("ma_vach");
            entity.Property(e => e.SoLuongKienHang).HasColumnName("so_luong_Kien_hang");
            entity.Property(e => e.SoTien)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("so_tien");
            entity.Property(e => e.TheTich).HasColumnName("the_tich");
            entity.Property(e => e.YeuCauBaoQuan)
                .HasMaxLength(50)
                .HasColumnName("yeu_cau_bao_quan");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.KienHangs)
                .HasForeignKey(d => d.MaDonHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KienHang_DonHang");

            entity.HasOne(d => d.MaLoaiHangNavigation).WithMany(p => p.KienHangs)
                .HasForeignKey(d => d.MaLoaiHang)
                .HasConstraintName("FK_KienHang_LoaiHang");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
