using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartRental.Models;

namespace SmartRental.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Phongtro> Phongtros { get; set; }

        public DbSet<TienNghi> TienNghis { get; set; }

        public DbSet<PhongTienNghi> PhongTienNghis { get; set; }

        public DbSet<YeuThich> YeuThichs { get; set; }

        public DbSet<LichSuXem> LichSuXems { get; set; }

        public DbSet<DanhGia> DanhGias { get; set; }
        public DbSet<PhongtroHinhAnh> PhongtroHinhAnhs { get; set; }
        public DbSet<CuocTroChuyen> CuocTroChuyens { get; set; }
        public DbSet<TinNhan> TinNhans { get; set; }
        public DbSet<ThongBao> ThongBaos { get; set; }
        public DbSet<LichXemPhong> LichXemPhongs { get; set; }


        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PhongTienNghi>()
                .HasKey(pt => new
                {
                    pt.PhongtroId,
                    pt.TienNghiId
                });

            modelBuilder.Entity<PhongTienNghi>()
                .HasOne(pt => pt.Phongtro)
                .WithMany(p => p.PhongTienNghis)
                .HasForeignKey(pt => pt.PhongtroId);

            modelBuilder.Entity<PhongTienNghi>()
                .HasOne(pt => pt.TienNghi)
                .WithMany(t => t.PhongTienNghis)
                .HasForeignKey(pt => pt.TienNghiId);

            modelBuilder.Entity<Phongtro>()
                .HasOne(p => p.Owner)
                .WithMany(user => user.Phongtros)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YeuThich>()
                .HasKey(yt => new { yt.UserId, yt.PhongtroId });

            modelBuilder.Entity<YeuThich>()
                .HasOne(yt => yt.User)
                .WithMany(user => user.YeuThichs)
                .HasForeignKey(yt => yt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<YeuThich>()
                .HasOne(yt => yt.Phongtro)
                .WithMany(phongtro => phongtro.YeuThichs)
                .HasForeignKey(yt => yt.PhongtroId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LichSuXem>()
                .HasKey(ls => new { ls.UserId, ls.PhongtroId });

            modelBuilder.Entity<LichSuXem>()
                .HasIndex(ls => ls.PhongtroId);

            modelBuilder.Entity<LichSuXem>()
                .HasIndex(ls => ls.LanXemCuoi);

            modelBuilder.Entity<LichSuXem>()
                .HasOne(ls => ls.User)
                .WithMany(user => user.LichSuXems)
                .HasForeignKey(ls => ls.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PhongtroHinhAnh>().HasIndex(x => new { x.PhongtroId, x.ThuTu });
            modelBuilder.Entity<PhongtroHinhAnh>().HasOne(x => x.Phongtro).WithMany(x => x.HinhAnhs)
                .HasForeignKey(x => x.PhongtroId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CuocTroChuyen>().HasIndex(x => new { x.NguoiThueId, x.ChuTroId }).IsUnique();
            modelBuilder.Entity<CuocTroChuyen>().HasOne(x => x.Phongtro).WithMany(x => x.CuocTroChuyens)
                .HasForeignKey(x => x.PhongtroId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<CuocTroChuyen>().HasOne(x => x.NguoiThue).WithMany().HasForeignKey(x => x.NguoiThueId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CuocTroChuyen>().HasOne(x => x.ChuTro).WithMany().HasForeignKey(x => x.ChuTroId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TinNhan>().HasIndex(x => new { x.CuocTroChuyenId, x.NgayGui });
            modelBuilder.Entity<TinNhan>().HasOne(x => x.CuocTroChuyen).WithMany(x => x.TinNhans).HasForeignKey(x => x.CuocTroChuyenId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TinNhan>().HasOne(x => x.NguoiGui).WithMany().HasForeignKey(x => x.NguoiGuiId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TinNhan>().HasOne(x => x.Phongtro).WithMany(x => x.TinNhans).HasForeignKey(x => x.PhongtroId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ThongBao>().HasIndex(x => new { x.UserId, x.DaDoc, x.NgayTao });
            modelBuilder.Entity<ThongBao>().HasOne(x => x.User).WithMany(x => x.ThongBaos).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LichXemPhong>().HasIndex(x => new { x.ChuTroId, x.TrangThai, x.ThoiGianXem });
            modelBuilder.Entity<LichXemPhong>().HasOne(x => x.Phongtro).WithMany(x => x.LichXemPhongs).HasForeignKey(x => x.PhongtroId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LichXemPhong>().HasOne(x => x.NguoiThue).WithMany().HasForeignKey(x => x.NguoiThueId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<LichXemPhong>().HasOne(x => x.ChuTro).WithMany().HasForeignKey(x => x.ChuTroId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DanhGia>()
                .HasKey(dg => new { dg.UserId, dg.PhongtroId });

            modelBuilder.Entity<DanhGia>()
                .HasIndex(dg => dg.PhongtroId);

            modelBuilder.Entity<DanhGia>()
                .HasIndex(dg => dg.NgayDanhGia);

            modelBuilder.Entity<DanhGia>()
                .HasOne(dg => dg.User)
                .WithMany(user => user.DanhGias)
                .HasForeignKey(dg => dg.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DanhGia>()
                .HasOne(dg => dg.Phongtro)
                .WithMany(phongtro => phongtro.DanhGias)
                .HasForeignKey(dg => dg.PhongtroId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LichSuXem>()
                .HasOne(ls => ls.Phongtro)
                .WithMany(phongtro => phongtro.LichSuXems)
                .HasForeignKey(ls => ls.PhongtroId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
