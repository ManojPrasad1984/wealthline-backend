
using Microsoft.EntityFrameworkCore;
using Wealthline.Functions.Functions.Services;
using Wealthline.Functions.Models;
namespace Wealthline.Functions.Functions.Data
{


    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Agent> Agents { get; set; }
        public DbSet<LuckyDrawEntry> LuckyDrawEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Agent>().ToTable("Agents", "Wealthline_LuckyDraw");
            modelBuilder.Entity<LuckyDrawEntry>().ToTable("LuckyDrawEntries");
            modelBuilder.Entity<LuckyDrawEntry>().HasOne(e => e.Agent).WithMany().HasForeignKey(e => e.AgentId).IsRequired(false);
        }
    }
}
