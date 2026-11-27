using Microsoft.EntityFrameworkCore;
using OnlineSignature.Models;

namespace OnlineSignature.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Signature> Signatures => Set<Signature>();
}