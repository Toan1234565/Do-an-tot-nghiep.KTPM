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

    public virtual DbSet<BangGiaVung> BangGiaVungs { get; set; }

    public virtual DbSet<DanhMucLoaiHang> DanhMucLoaiHangs { get; set; }

    public virtual DbSet<DanhMucPhuongThucThanhToan> DanhMucPhuongThucThanhToans { get; set; }

    public virtual DbSet<DonHang> DonHangs { get; set; }

    public virtual DbSet<HoaDon> HoaDons { get; set; }

    public virtual DbSet<KienHang> KienHangs { get; set; }

    public virtual DbSet<MucDoDichVu> MucDoDichVus { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=DON_HANG;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BangChungGiaoHang>(entity =>
        {
            entity.HasKey(e => e.MaPod).HasName("PK__Bang_Chu__0A6B0DE1AFE9361B");

            entity.ToTable("Bang_Chung_Giao_Hang");

            entity.Property(e => e.MaPod).HasColumnName("ma_pod");
            entity.Property(e => e.TenNguoiNhan)
                .HasMaxLength(100)
                .HasColumnName("ten_nguoi_nhan");
            entity.Property(e => e.ThoiGian)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian");
            entity.Property(e => e.UrlAnh).HasColumnName("url_anh");
            entity.Property(e => e.UrlChuKy).HasColumnName("url_chu_ky");
        });

        modelBuilder.Entity<BangGiaVung>(entity =>
        {
            entity.HasKey(e => e.MaBangGia).HasName("PK__Bang_Gia__6A3C134E27CBD90F");

            entity.ToTable("Bang_Gia_Vung");

            entity.HasIndex(e => new { e.KhuVucLay, e.KhuVucGiao }, "IX_BangGiaVung_KhuVuc");

            entity.HasIndex(e => e.LoaiTinhGia, "IX_BangGiaVung_LoaiTinhGia");

            entity.HasIndex(e => new { e.IsActive, e.NgayCapNhat }, "IX_BangGiaVung_NgayCapNhat_IsActive").IsDescending(false, true);

            entity.HasIndex(e => new { e.IsActive, e.NgayCapNhat }, "IX_BangGiaVung_Optimized").IsDescending(false, true);

            entity.Property(e => e.MaBangGia).HasColumnName("ma_bang_gia");
            entity.Property(e => e.DonGiaCoBan)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("don_gia_co_ban");
            entity.Property(e => e.DonGiaKm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("don_gia_km");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.KhuVucGiao)
                .HasMaxLength(100)
                .HasColumnName("khu_vuc_giao");
            entity.Property(e => e.KhuVucLay)
                .HasMaxLength(100)
                .HasColumnName("khu_vuc_lay");
            entity.Property(e => e.KmToiThieu).HasColumnName("km_toi_thieu");
            entity.Property(e => e.LoaiTinhGia).HasColumnName("loai_tinh_gia");
            entity.Property(e => e.LyDoThayDoi).HasColumnName("ly_do_thay_doi");
            entity.Property(e => e.MaBangCu).HasColumnName("ma_bang_cu");
            entity.Property(e => e.MaLoaiHang).HasColumnName("ma_loai_hang");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_cap_nhat");
            entity.Property(e => e.PhiDungDiem)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("phi_dung_diem");
            entity.Property(e => e.PhuPhiMoiKg)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("phu_phi_moi_kg");
            entity.Property(e => e.TrongLuongToiDaKg)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("trong_luong_toi_da_kg");
            entity.Property(e => e.TrongLuongToiThieuKg)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("trong_luong_toi_thieu_kg");

            entity.HasOne(d => d.MaLoaiHangNavigation).WithMany(p => p.BangGiaVungs)
                .HasForeignKey(d => d.MaLoaiHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BangGia_LoaiHang");
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

        modelBuilder.Entity<DanhMucPhuongThucThanhToan>(entity =>
        {
            entity.HasKey(e => e.MaPttt).HasName("PK__Danh_Muc__B30A28028C18A566");

            entity.ToTable("Danh_Muc_Phuong_Thuc_Thanh_Toan");

            entity.Property(e => e.MaPttt).HasColumnName("MaPTTT");
            entity.Property(e => e.LoaiThanhToan).HasMaxLength(50);
            entity.Property(e => e.TenPttt)
                .HasMaxLength(100)
                .HasColumnName("TenPTTT");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DonHang>(entity =>
        {
            entity.HasKey(e => e.MaDonHang).HasName("PK__Don_Hang__0246C5EA3FF89D88");

            entity.ToTable("Don_Hang");

            entity.HasIndex(e => e.MaKhachHang, "IX_DonHang_MaKhachHang");

            entity.HasIndex(e => new { e.ThoiGianTao, e.TrangThaiHienTai }, "IX_DonHang_Optimized").IsDescending(true, false);

            entity.HasIndex(e => e.TenDonHang, "IX_DonHang_TenDonHang");

            entity.HasIndex(e => e.ThoiGianTao, "IX_DonHang_ThoiGianTao");

            entity.HasIndex(e => new { e.ThoiGianTao, e.TrangThaiHienTai }, "IX_DonHang_ThoiGian_TrangThai").IsDescending(true, false);

            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.GhiChuDacBiet).HasColumnName("ghi_chu_dac_biet");
            entity.Property(e => e.MaDiaChiLayHang).HasColumnName("ma_dia_chi_lay_hang");
            entity.Property(e => e.MaDiaChiNhanHang).HasColumnName("ma_dia_chi_nhan_hang");
            entity.Property(e => e.MaHopDongNgoai).HasColumnName("ma_hop_dong_ngoai");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.MaKhoHienTai).HasColumnName("ma_kho_hien_tai");
            entity.Property(e => e.MaKhuyenMai).HasColumnName("ma_khuyen_mai");
            entity.Property(e => e.MaMucDoDv).HasColumnName("ma_muc_do_dv");
            entity.Property(e => e.MaPttt).HasColumnName("MaPTTT");
            entity.Property(e => e.MaVungH3Giao)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaVungH3_Giao");
            entity.Property(e => e.MaVungH3Nhan)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaVungH3_Nhan");
            entity.Property(e => e.SdtNguoiNhan)
                .HasMaxLength(20)
                .HasColumnName("sdt_nguoi_nhan");
            entity.Property(e => e.TenDonHang)
                .HasMaxLength(255)
                .HasColumnName("ten_don_hang");
            entity.Property(e => e.TenNguoiNhan)
                .HasMaxLength(100)
                .HasColumnName("ten_nguoi_nhan");
            entity.Property(e => e.ThoiGianGiaoDuKien).HasColumnType("datetime");
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
            entity.Property(e => e.TrangThaiThanhToanTong)
                .HasMaxLength(50)
                .HasDefaultValue("Chưa thanh toán");

            entity.HasOne(d => d.MaMucDoDvNavigation).WithMany(p => p.DonHangs)
                .HasForeignKey(d => d.MaMucDoDv)
                .HasConstraintName("FK_DonHang_MucDoDichVu");

            entity.HasOne(d => d.MaPtttNavigation).WithMany(p => p.DonHangs)
                .HasForeignKey(d => d.MaPttt)
                .HasConstraintName("FK_DonHang_DanhMucPTTT");
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.HasKey(e => e.MaHoaDon).HasName("PK__HoaDon__835ED13B2D60DA49");

            entity.ToTable("HoaDon");

            entity.Property(e => e.MaGiaoDichNgoai)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MaPttt).HasColumnName("MaPTTT");
            entity.Property(e => e.NgayThanhToan)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTienThanhToan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThaiThanhToan).HasMaxLength(50);

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaDonHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HoaDon_DonHang");

            entity.HasOne(d => d.MaPtttNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaPttt)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HoaDon_PTTT");
        });

        modelBuilder.Entity<KienHang>(entity =>
        {
            entity.HasKey(e => e.MaKienHang).HasName("PK__Kien_Han__C4D5465D9CCBE235");

            entity.ToTable("Kien_Hang");

            entity.HasIndex(e => e.MaVach, "UQ__Kien_Han__DE70626347024EA8").IsUnique();

            entity.Property(e => e.MaKienHang).HasColumnName("ma_kien_hang");
            entity.Property(e => e.KhoiLuong).HasColumnName("khoi_luong");
            entity.Property(e => e.MaBangGiaVung).HasColumnName("ma_bang_gia_vung");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
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

            entity.HasOne(d => d.MaBangGiaVungNavigation).WithMany(p => p.KienHangs)
                .HasForeignKey(d => d.MaBangGiaVung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KienHang_BangGia");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.KienHangs)
                .HasForeignKey(d => d.MaDonHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KienHang_DonHang");
        });

        modelBuilder.Entity<MucDoDichVu>(entity =>
        {
            entity.HasKey(e => e.MaDichVu).HasName("PK__Muc_Do_D__5ADDD34509111026");

            entity.ToTable("Muc_Do_Dich_Vu");

            entity.Property(e => e.MaDichVu).HasColumnName("ma_dich_vu");
            entity.Property(e => e.HeSoNhiPhan).HasColumnName("he_so_nhi_phan");
            entity.Property(e => e.LaCaoCap).HasColumnName("la_cao_cap");
            entity.Property(e => e.MaBangCu).HasColumnName("ma_bang_cu");
            entity.Property(e => e.NgayBatDau)
                .HasColumnType("datetime")
                .HasColumnName("ngay_bat_dau");
            entity.Property(e => e.NgayKetThuc)
                .HasColumnType("datetime")
                .HasColumnName("ngay_ket_thuc");
            entity.Property(e => e.TenDichVu)
                .HasMaxLength(255)
                .HasColumnName("ten_dich_vu");
            entity.Property(e => e.ThoiGianCamKet)
                .HasMaxLength(100)
                .HasColumnName("thoi_gian_cam_ket");
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
