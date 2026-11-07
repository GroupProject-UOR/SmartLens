using FinanceApp.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<OtpVerification> OtpVerifications
        => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Unique email addresses, ignoring null values
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasFilter("\"NormalizedEmail\" IS NOT NULL");

        // Unique phone numbers, ignoring null values
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("\"PhoneNumber\" IS NOT NULL");

        builder.Entity<OtpVerification>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.UserId)
                .IsRequired();

            entity.Property(o => o.VerificationMethod)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(o => o.CodeHash)
                .IsRequired();

            entity.HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => new
            {
                o.UserId,
                o.VerificationMethod
            });
        });
    }
}