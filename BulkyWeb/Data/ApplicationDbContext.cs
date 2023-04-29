using BulkyWeb.Models;
using Microsoft.EntityFrameworkCore;

//NOTE TO SELF: this class is basic config for the entity framework, always register in Program.cs
namespace BulkyWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            
        }

        //Create table named Categories
        //NuGet console command: add-migration UsefulName
        public DbSet<Category> Categories { get; set; }

        //insert into Category table
        //NuGet console command: add-migration UsefulName
        //to apply changes run NuGet console command: update-database
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Action", DisplayOrder = 1 },
                new Category { Id = 2, Name = "SciFi", DisplayOrder = 2 },
                new Category { Id = 3, Name = "History", DisplayOrder = 3 }
                );
        }
    }
}
