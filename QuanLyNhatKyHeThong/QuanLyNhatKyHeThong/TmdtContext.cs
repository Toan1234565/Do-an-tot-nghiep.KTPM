using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyNhatKyHeThong.Models;

namespace QuanLyNhatKyHeThong;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<NhatKyHeThong> NhatKyHeThongs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=Nhat_Ky_He_Thong;Integrated Security=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NhatKyHeThong>(entity =>
        {
            entity.HasKey(e => e.MaNhatKy).HasName("PK__NhatKyHe__E42EF42E33A9C420");

            entity.ToTable("NhatKyHeThong");

            entity.HasIndex(e => e.MaDoiTuong, "IX_NhatKy_MaDoiTuong");

            entity.HasIndex(e => e.NguoiThucHien, "IX_NhatKy_NguoiThucHien");

            entity.HasIndex(e => e.ThoiGianThucHien, "IX_NhatKy_ThoiGian");

            entity.Property(e => e.DiaChiIp)
                .HasMaxLength(50)
                .HasColumnName("DiaChiIP");
            entity.Property(e => e.LoaiThaoTac).HasMaxLength(500);
            entity.Property(e => e.MaDoiTuong).HasMaxLength(50);
            entity.Property(e => e.NguoiThucHien).HasMaxLength(250);
            entity.Property(e => e.TenBangLienQuan).HasMaxLength(100);
            entity.Property(e => e.TenDichVu).HasMaxLength(250);
            entity.Property(e => e.ThoiGianThucHien)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThaiThaoTac).HasDefaultValue(true);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
