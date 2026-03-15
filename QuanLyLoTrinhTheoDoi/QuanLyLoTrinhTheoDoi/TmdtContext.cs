using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;

namespace QuanLyLoTrinhTheoDoi;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChiPhiLoTrinh> ChiPhiLoTrinhs { get; set; }

    public virtual DbSet<ChiTietLoTrinhKienHang> ChiTietLoTrinhKienHangs { get; set; }

    public virtual DbSet<DiemDung> DiemDungs { get; set; }

    public virtual DbSet<LoTrinh> LoTrinhs { get; set; }

    public virtual DbSet<LoaiSuCo> LoaiSuCos { get; set; }

    public virtual DbSet<NhatKyTheoDoi> NhatKyTheoDois { get; set; }

    public virtual DbSet<SuCo> SuCos { get; set; }

    public virtual DbSet<TuyenDuongCoDinh> TuyenDuongCoDinhs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Lo_Trinh_Theo_Doi_DB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChiPhiLoTrinh>(entity =>
        {
            entity.HasKey(e => e.MaChiPhi).HasName("PK__ChiPhiLo__6FA160B0BC6F4CF6");

            entity.ToTable("ChiPhiLoTrinh");

            entity.Property(e => e.MaChiPhi).HasColumnName("ma_chi_phi");
            entity.Property(e => e.ChungTuKemTheo).HasColumnName("chung_tu_kem_theo");
            entity.Property(e => e.LoaiChiPhi)
                .HasMaxLength(100)
                .HasColumnName("loai_chi_phi");
            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.SoTien)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("so_tien");

            entity.HasOne(d => d.MaLoTrinhNavigation).WithMany(p => p.ChiPhiLoTrinhs)
                .HasForeignKey(d => d.MaLoTrinh)
                .HasConstraintName("FK_ChiPhi_LoTrinh");
        });

        modelBuilder.Entity<ChiTietLoTrinhKienHang>(entity =>
        {
            entity.HasKey(e => e.MaChiTietLoTrinh).HasName("PK_ChiTietLoTrinh");

            entity.ToTable("Chi_Tiet_Lo_Trinh_KienHang");

            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.TrangThaiTrenXe)
                .HasMaxLength(50)
                .HasColumnName("trang_thai_tren_xe");

            entity.HasOne(d => d.MaLoTrinhNavigation).WithMany(p => p.ChiTietLoTrinhKienHangs)
                .HasForeignKey(d => d.MaLoTrinh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CT_LoTrinh");
        });

        modelBuilder.Entity<DiemDung>(entity =>
        {
            entity.HasKey(e => e.MaDiemDung).HasName("PK__Diem_Dun__ACAC6AAF24ED52C0");

            entity.ToTable("Diem_Dung");

            entity.Property(e => e.MaDiemDung).HasColumnName("ma_diem_dung");
            entity.Property(e => e.EtaKeHoach)
                .HasColumnType("datetime")
                .HasColumnName("eta_ke_hoach");
            entity.Property(e => e.LoaiDung)
                .HasMaxLength(50)
                .HasColumnName("loai_dung");
            entity.Property(e => e.MaDiaChi).HasColumnName("ma_dia_chi");
            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.MaVungH3)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ma_vung_h3");
            entity.Property(e => e.ThoiGianDenThucTe)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_den_thuc_te");
            entity.Property(e => e.ThuTuDung).HasColumnName("thu_tu_dung");

            entity.HasOne(d => d.MaLoTrinhNavigation).WithMany(p => p.DiemDungs)
                .HasForeignKey(d => d.MaLoTrinh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiemDung_LoTrinh");
        });

        modelBuilder.Entity<LoTrinh>(entity =>
        {
            entity.HasKey(e => e.MaLoTrinh).HasName("PK__Lo_Trinh__FD9AB53F3322776C");

            entity.ToTable("Lo_Trinh");

            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.MaPhuongTien).HasColumnName("ma_phuong_tien");
            entity.Property(e => e.MaTaiXeChinh).HasColumnName("ma_tai_xe_chinh");
            entity.Property(e => e.MaTaiXePhu).HasColumnName("ma_tai_xe_phu");
            entity.Property(e => e.ThoiGianBatDauKeHoach)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_bat_dau_ke_hoach");
            entity.Property(e => e.ThoiGianBatDauThucTe)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_bat_dau_thuc_te");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");
        });

        modelBuilder.Entity<LoaiSuCo>(entity =>
        {
            entity.HasKey(e => e.MaLoaiSuCo).HasName("PK__Loai_Su___13F685C9FD3707A6");

            entity.ToTable("Loai_Su_Co");

            entity.Property(e => e.MaLoaiSuCo).HasColumnName("ma_loai_su_co");
            entity.Property(e => e.GhiChu)
                .HasMaxLength(255)
                .HasColumnName("ghi_chu");
            entity.Property(e => e.MucDoNghiemTrong)
                .HasDefaultValue(1)
                .HasColumnName("muc_do_nghiem_trong");
            entity.Property(e => e.TenLoaiSuCo)
                .HasMaxLength(100)
                .HasColumnName("ten_loai_su_co");
        });

        modelBuilder.Entity<NhatKyTheoDoi>(entity =>
        {
            entity.HasKey(e => e.MaNhatKy).HasName("PK__Nhat_Ky___DBCACAC144503275");

            entity.ToTable("Nhat_Ky_Theo_Doi");

            entity.Property(e => e.MaNhatKy).HasColumnName("ma_nhat_ky");
            entity.Property(e => e.KinhDo).HasColumnName("kinh_do");
            entity.Property(e => e.MaTaiXe).HasColumnName("ma_tai_xe");
            entity.Property(e => e.MaVungH3)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ThoiGian)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian");
            entity.Property(e => e.TocDoKmh).HasColumnName("toc_do_kmh");
            entity.Property(e => e.ViDo).HasColumnName("vi_do");
        });

        modelBuilder.Entity<SuCo>(entity =>
        {
            entity.HasKey(e => e.MaSuCo).HasName("PK__Su_Co__83F4D0983B987D91");

            entity.ToTable("Su_Co");

            entity.Property(e => e.MaSuCo).HasColumnName("ma_su_co");
            entity.Property(e => e.DiaChiCuThe)
                .HasMaxLength(255)
                .HasColumnName("dia_chi_cu_the");
            entity.Property(e => e.GhiChuTuChoi).HasColumnName("ghi_chu_tu_choi");
            entity.Property(e => e.KinhDo).HasColumnName("kinh_do");
            entity.Property(e => e.MaLoTrinh).HasColumnName("ma_lo_trinh");
            entity.Property(e => e.MaLoaiSuCo).HasColumnName("ma_loai_su_co");
            entity.Property(e => e.MoTa).HasColumnName("mo_ta");
            entity.Property(e => e.ThoiGianBaoCao)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_bao_cao");
            entity.Property(e => e.ThoiGianXuLy)
                .HasColumnType("datetime")
                .HasColumnName("thoi_gian_xu_ly");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasColumnName("trang_thai");
            entity.Property(e => e.UrlHinhAnhSuCo).HasColumnName("url_hinh_anh_su_co");
            entity.Property(e => e.UrlVideoSuCo).HasColumnName("url_video_su_co");
            entity.Property(e => e.ViDo).HasColumnName("vi_do");

            entity.HasOne(d => d.MaLoTrinhNavigation).WithMany(p => p.SuCos)
                .HasForeignKey(d => d.MaLoTrinh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SuCo_LoTrinh");

            entity.HasOne(d => d.MaLoaiSuCoNavigation).WithMany(p => p.SuCos)
                .HasForeignKey(d => d.MaLoaiSuCo)
                .HasConstraintName("FK_SuCo_LoaiSuCo");
        });

        modelBuilder.Entity<TuyenDuongCoDinh>(entity =>
        {
            entity.HasKey(e => e.MaTuyen).HasName("PK__Tuyen_Du__8EF30D64769346F6");

            entity.ToTable("Tuyen_Duong_Co_Dinh");

            entity.Property(e => e.MaTuyen).HasColumnName("ma_tuyen");
            entity.Property(e => e.KhoangCachKm).HasColumnName("khoang_cach_km");
            entity.Property(e => e.MaDiaChiGiao).HasColumnName("ma_dia_chi_giao");
            entity.Property(e => e.MaDiaChiLay).HasColumnName("ma_dia_chi_lay");
            entity.Property(e => e.MaHopDong).HasColumnName("ma_hop_dong");
            entity.Property(e => e.MaXeDinhDanh).HasColumnName("ma_xe_dinh_danh");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
