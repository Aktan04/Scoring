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
    public DbSet<LoanProduct> LoanProducts { get; set; }
    public DbSet<BlackListEntry> BlackListEntries { get; set; }
    public DbSet<ApplicationHistory> ApplicationHistories { get; set; }
    public DbSet<TrainingRecord>    TrainingRecords    { get; set; }
    public DbSet<ModelCoefficients> ModelCoefficients  { get; set; }
    
}