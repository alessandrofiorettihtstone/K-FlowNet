using Microsoft.EntityFrameworkCore;

namespace Scheletro.Models;

public partial class dbContext : DbContext
{
    public dbContext()
    {
    }

    public dbContext(DbContextOptions<dbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Card> Cards { get; set; }

    public virtual DbSet<Combination> Combinations { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Partner> Partners { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Printer> Printers { get; set; }

    public virtual DbSet<KFlowStatus> Statuses { get; set; }

    public virtual DbSet<Sector> Sectors { get; set; }

    public virtual DbSet<TblLog> TblLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VwKanbanBoard> VwKanbanBoard { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.IDCard);
            entity.ToTable("Cards", table => table.HasTrigger("Cards_DatabaseTrigger"));
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.IDCard).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Combination>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.ToTable("Combination");
            entity.Property(e => e.ID).ValueGeneratedOnAdd();
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.PartnerType).HasMaxLength(1).IsUnicode(false);
            entity.Property(e => e.IDPartner).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IDProduct).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IDCross).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationID);
            entity.ToTable("Locations");
            entity.Property(e => e.LocationGuid).ValueGeneratedNever();
            entity.Property(e => e.PartnerType).HasMaxLength(1).IsUnicode(false);
            entity.Property(e => e.PartnerID).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.LocationCode).HasMaxLength(5).IsUnicode(false);
            entity.Property(e => e.LocationName).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.Guid);
            entity.ToTable("Partners");
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.PartnerType).HasMaxLength(1).IsUnicode(false);
            entity.Property(e => e.ID).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Guid);
            entity.ToTable("Products");
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.ID).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.UMBase).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(e => e.IdPrinter);
            entity.ToTable("Printer");
            entity.Property(e => e.IdPrinter).HasColumnName("idPrinter").ValueGeneratedOnAdd();
            entity.Property(e => e.Ip).HasColumnName("ip").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Port).HasColumnName("port");
        });

        modelBuilder.Entity<KFlowStatus>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.ToTable("Status");
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Color).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Sector>(entity =>
        {
            entity.HasKey(e => e.SectorID);
            entity.ToTable("Sectors");
            entity.Property(e => e.SectorGuid).ValueGeneratedNever();
            entity.Property(e => e.SectorName).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<TblLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("tblLog");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ODP).HasMaxLength(15).IsUnicode(false);
            entity.Property(e => e.IDMix).HasMaxLength(3).IsUnicode(false);
            entity.Property(e => e.IDRows).HasMaxLength(3).IsUnicode(false);
            entity.Property(e => e.IDUser).HasMaxLength(3).IsUnicode(false);
            entity.Property(e => e.Text).IsUnicode(false);
            entity.Property(e => e.PostingDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Guid);
            entity.ToTable("Users");
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.IDUser).ValueGeneratedOnAdd();
            entity.Property(e => e.Username).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Password).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Surname).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Role).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Department).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.CreateDate).HasColumnType("date");
            entity.Property(e => e.LastModify).HasColumnType("datetime");
            entity.Ignore(e => e.Id);
            entity.Ignore(e => e.Nome);
            entity.Ignore(e => e.Cognome);
        });

        modelBuilder.Entity<VwKanbanBoard>(entity =>
        {
            entity.HasKey(e => e.IDCard);
            entity.ToView("vwKanbanBoard");
            entity.Property(e => e.Guid).ValueGeneratedNever();
            entity.Property(e => e.IDCard).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IDPartner).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.PartnerName).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IDProduct).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ProductName).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.UMBase).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.StatusDescription).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Color).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.LocationCode).HasMaxLength(5).IsUnicode(false);
            entity.Property(e => e.LocationName).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IDCross).HasMaxLength(50).IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
