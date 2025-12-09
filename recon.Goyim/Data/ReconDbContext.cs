using Microsoft.EntityFrameworkCore;
using recon.Goyim.Models;

namespace recon.Goyim.Data;

public class ReconDbContext : DbContext
{
    public ReconDbContext(DbContextOptions<ReconDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}
