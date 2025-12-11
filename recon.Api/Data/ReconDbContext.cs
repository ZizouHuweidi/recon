using Microsoft.EntityFrameworkCore;
using recon.Api.Models;

namespace recon.Api.Data;

public class ReconDbContext : DbContext
{
    public ReconDbContext(DbContextOptions<ReconDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}
