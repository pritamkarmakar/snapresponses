using System.Configuration;
using ShibpurConnectWebApp.Models.WebAPI;
using MongoDB.Driver;

namespace ShibpurConnectWebApp
{
    //public partial class ShibpurConnectDB : ApplicationIdentityContext
    //{
    //    //public ShibpurConnectDB()
    //    //    : base("name=ShibpurConnectDB")
    //    //{
    //    //    Configuration.ProxyCreationEnabled = false;
    //    //}

    //    public virtual DbSet<C__MigrationHistory> C__MigrationHistory { get; set; }
    //    public virtual DbSet<AspNetRoles> AspNetRoles { get; set; }
    //    public virtual DbSet<AspNetUserClaims> AspNetUserClaims { get; set; }
    //    public virtual DbSet<AspNetUserLogins> AspNetUserLogins { get; set; }
    //    public virtual DbSet<AspNetUsers> AspNetUsers { get; set; }
    //    public virtual DbSet<Questions> Questions { get; set; }
    //    public virtual DbSet<Comments> Comments { get; set; }
    //    public virtual DbSet<Categories> Categories { get; set; }
    //    public virtual DbSet<CategoryTaggings> CategoryTaggings { get; set; }
    //    public virtual DbSet<EducationalHistory> EducationalHistories { get; set; }
    //    public virtual DbSet<EmploymentHistory> EmploymentHistories { get; set; }
    //    public virtual DbSet<Departments> Departmentses { get; set; }

    //    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    //    {
    //        modelBuilder.Entity<AspNetRoles>()
    //            .HasMany(e => e.AspNetUsers)
    //            .WithMany(e => e.AspNetRoles)
    //            .Map(m => m.ToTable("AspNetUserRoles").MapLeftKey("RoleId").MapRightKey("UserId"));

    //        modelBuilder.Entity<AspNetUsers>()
    //            .HasMany(e => e.AspNetUserClaims)
    //            .WithRequired(e => e.AspNetUsers)
    //            .HasForeignKey(e => e.UserId);

    //        modelBuilder.Entity<AspNetUsers>()
    //            .HasMany(e => e.AspNetUserLogins)
    //            .WithRequired(e => e.AspNetUsers)
    //            .HasForeignKey(e => e.UserId);
    //    }

    //    //public System.Data.Entity.DbSet<ShibpurConnectWebApp.Models.WebAPI.EducationalHistory> EducationalHistories { get; set; }
    //}

    public class ShibpurConnectDB
    {
        
    }
}
