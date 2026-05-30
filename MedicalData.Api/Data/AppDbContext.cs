using MedicalData.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LabResult> LabResults { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ShareToken> ShareTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ShareToken>().HasKey(x => x.Token);

        model.Entity<User>()
            .HasMany(u => u.LabResults)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<User>()
            .HasMany(u => u.ShareTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
