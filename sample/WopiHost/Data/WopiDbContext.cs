using Microsoft.EntityFrameworkCore;
using WopiHost.Models.Database;

namespace WopiHost.Data;

public class WopiDbContext : DbContext
{
    public WopiDbContext(DbContextOptions<WopiDbContext> options) : base(options)
    {
    }

    public DbSet<CR02TepDinhKem> CR02TepDinhKem { get; set; }

    public DbSet<NS01TaiKhoanNguoiDung> NS01TaiKhoanNguoiDungs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the ModelBase properties with correct column names
        modelBuilder.Entity<CR02TepDinhKem>(entity =>
        {
            entity.ToTable("cr02tepdinhkem", "section0");
            entity.HasKey(e => e.Id);
            
            // Configure base class properties with correct column names
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.MarkAsClose).HasColumnName("markasclose");
            entity.Property(e => e.CreateDate).HasColumnName("createdate");
            entity.Property(e => e.WriteDate).HasColumnName("writedate");
            entity.Property(e => e.CreateUid).HasColumnName("createuid");
            entity.Property(e => e.WriteUid).HasColumnName("writeuid");
            entity.Property(e => e.TrangThai).HasColumnName("trangthai");
            entity.Property(e => e.GhiChu).HasColumnName("ghichu");

            // Configure DateTime properties for UTC conversion
            entity.Property(e => e.CreateDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            
            entity.Property(e => e.WriteDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        modelBuilder.Entity<NS01TaiKhoanNguoiDung>(entity =>
        {
            entity.ToTable("ns01taikhoannguoidung", "section9nhansu");
            entity.HasKey(e => e.Id);

            // Configure base class properties with correct column names
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.MarkAsClose).HasColumnName("markasclose");
            entity.Property(e => e.CreateDate).HasColumnName("createdate");
            entity.Property(e => e.WriteDate).HasColumnName("writedate");
            entity.Property(e => e.CreateUid).HasColumnName("createuid");
            entity.Property(e => e.WriteUid).HasColumnName("writeuid");
            entity.Property(e => e.TrangThai).HasColumnName("trangthai");
            entity.Property(e => e.GhiChu).HasColumnName("ghichu");

            // Configure DateTime properties for UTC conversion
            entity.Property(e => e.CreateDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            
            entity.Property(e => e.WriteDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        modelBuilder.Entity<NS01TaiKhoanNguoiDung>(entity =>
        {
            entity.ToTable("ns01taikhoannguoidung", "section9nhansu");
            entity.HasKey(e => e.Id);

            // Configure base class properties with correct column names
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.MarkAsClose).HasColumnName("markasclose");
            entity.Property(e => e.CreateDate).HasColumnName("createdate");
            entity.Property(e => e.WriteDate).HasColumnName("writedate");
            entity.Property(e => e.CreateUid).HasColumnName("createuid");
            entity.Property(e => e.WriteUid).HasColumnName("writeuid");
            entity.Property(e => e.TrangThai).HasColumnName("trangthai");
            entity.Property(e => e.GhiChu).HasColumnName("ghichu");

            // Configure DateTime properties for UTC conversion
            entity.Property(e => e.CreateDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            
            entity.Property(e => e.WriteDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}
