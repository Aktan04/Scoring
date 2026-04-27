using CreditScoringSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Scoring.Models;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<LoanApplication> LoanApplications { get; set; }
    public DbSet<ScoringRule> ScoringRules { get; set; }
    public DbSet<ScoringResult> ScoringResults { get; set; }
    public DbSet<ApplicationStatus> ApplicationStatuses { get; set; }
    public DbSet<LoanProduct> LoanProducts { get; set; }
    public DbSet<BlackListEntry> BlackListEntries { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationStatus>().HasData(
            new ApplicationStatus { Id = 1, Name = "Черновик" },
            new ApplicationStatus { Id = 2, Name = "На скоринге" },
            new ApplicationStatus { Id = 3, Name = "Одобрено" },
            new ApplicationStatus { Id = 4, Name = "Отказ" },
            new ApplicationStatus { Id = 5, Name = "Ручная проверка" }
        );
    }
}