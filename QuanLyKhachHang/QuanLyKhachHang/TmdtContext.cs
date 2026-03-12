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

    public virtual DbSet<BangGiaVung> BangGiaVungs { get; set; }

    public virtual DbSet<CauHinhTichDiem> CauHinhTichDiems { get; set; }

    public virtual DbSet<DiaChi> DiaChis { get; set; }

    public virtual DbSet<DiemThuong> DiemThuongs { get; set; }

    public virtual DbSet<HopDongVanChuyen> HopDongVanChuyens { get; set; }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<KhuyenMai> KhuyenMais { get; set; }

    public virtual DbSet<LichSuDungMa> LichSuDungMas { get; set; }

    public virtual DbSet<LoaiDichVu> LoaiDichVus { get; set; }

    public virtual DbSet<LoaiKhuyenMai> LoaiKhuyenMais { get; set; }

    public virtual DbSet<MucDoDichVu> MucDoDichVus { get; set; }

    public virtual DbSet<SanBay> SanBays { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Khach_Hang_Gia_Cuoc_DB;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BangGiaVung>(entity =>
        {
            entity.HasKey(e => e.MaBangGia).HasName("PK__Bang_Gia__6A3C134E6F3432A5");

            entity.ToTable("Bang_Gia_Vung");

            entity.Property(e => e.MaBangGia).HasColumnName("ma_bang_gia");
            entity.Property(e => e.DonGiaCoBan)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("don_gia_co_ban");
            entity.Property(e => e.DonGiaKm)
                .HasDefaultValue(0m)
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
            entity.Property(e => e.KmToiThieu)
                .HasDefaultValue(0)
                .HasColumnName("km_toi_thieu");
            entity.Property(e => e.LoaiTinhGia)
                .HasDefaultValue(1)
                .HasComment("1: Theo Vùng, 2: Theo Km")
                .HasColumnName("loai_tinh_gia");
            entity.Property(e => e.LyDoThayDoi)
                .HasMaxLength(255)
                .HasColumnName("ly_do_thay_doi");
            entity.Property(e => e.MaBangCu).HasColumnName("ma_bang_cu");
            entity.Property(e => e.MaLoaiHang).HasColumnName("ma_loai_hang");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_cap_nhat");
            entity.Property(e => e.PhiDungDiem)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("phi_dung_diem");
            entity.Property(e => e.PhuPhiMoiKg)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("phu_phi_moi_kg");
            entity.Property(e => e.TrongLuongToiDaKg)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("trong_luong_toi_da_kg");
            entity.Property(e => e.TrongLuongToiThieuKg)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("trong_luong_toi_thieu_kg");
        });

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
            entity.Property(e => e.MaBuuDien)
                .HasMaxLength(20)
                .HasColumnName("ma_buu_dien");
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

        modelBuilder.Entity<LoaiDichVu>(entity =>
        {
            entity.HasKey(e => e.MaLoaiDv).HasName("PK__Loai_Dic__1E1F8AFAC4B36FE2");

            entity.ToTable("Loai_Dich_Vu");

            entity.Property(e => e.MaLoaiDv).HasColumnName("ma_loai_dv");
            entity.Property(e => e.CoPhaiGiaoThang)
                .HasDefaultValue(false)
                .HasColumnName("co_phai_giao_thang");
            entity.Property(e => e.MoTa)
                .HasMaxLength(255)
                .HasColumnName("mo_ta");
            entity.Property(e => e.TenLoaiDv)
                .HasMaxLength(100)
                .HasColumnName("ten_loai_dv");
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

        modelBuilder.Entity<MucDoDichVu>(entity =>
        {
            entity.HasKey(e => e.MaDichVu).HasName("PK__Muc_Do_D__5ADDD345467294D6");

            entity.ToTable("Muc_Do_Dich_Vu");

            entity.Property(e => e.MaDichVu).HasColumnName("ma_dich_vu");
            entity.Property(e => e.HeSoNhiPhan)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("he_so_nhi_phan");
            entity.Property(e => e.LaCaoCap).HasColumnName("la_cao_cap");
            entity.Property(e => e.MaBangCu).HasColumnName("ma_bang_cu");
            entity.Property(e => e.NgayBatDau)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngay_bat_dau");
            entity.Property(e => e.NgayKetThuc)
                .HasColumnType("datetime")
                .HasColumnName("ngay_ket_thuc");
            entity.Property(e => e.TenDichVu)
                .HasMaxLength(50)
                .HasColumnName("ten_dich_vu");
            entity.Property(e => e.ThoiGianCamKet)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("thoi_gian_cam_ket");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai");
        });

        modelBuilder.Entity<SanBay>(entity =>
        {
            entity.HasKey(e => e.MaSanBay).HasName("PK__San_Bay__8A2061A928CE46C1");

            entity.ToTable("San_Bay");

            entity.HasIndex(e => e.IataCode, "UQ__San_Bay__1B78975C788BDC5F").IsUnique();

            entity.Property(e => e.MaSanBay).HasColumnName("ma_san_bay");
            entity.Property(e => e.IataCode)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasColumnName("iata_code");
            entity.Property(e => e.MaDiaChi).HasColumnName("ma_dia_chi");
            entity.Property(e => e.TenSanBay)
                .HasMaxLength(255)
                .HasColumnName("ten_san_bay");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaDiaChiNavigation).WithMany(p => p.SanBays)
                .HasForeignKey(d => d.MaDiaChi)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SanBay_DiaChi");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
