using System;
using System.Collections.Generic;
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

    public virtual DbSet<PickingTransaction> PickingTransactions { get; set; }

    public virtual DbSet<Printer> Printers { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Sector> Sectors { get; set; }

    public virtual DbSet<KFlowStatus> Statuses { get; set; }

    public virtual DbSet<StorageLocation> StorageLocations { get; set; }

    public virtual DbSet<TblLog> TblLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VwKanbanBoard> VwKanbanBoard { get; set; }

    public virtual DbSet<Zpl> Zpls { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.IDCard);

            entity.ToTable(tb => tb.HasTrigger("trg_UpdateCardStatus"));

            entity.Property(e => e.IDCard)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDCard");
            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IDCombination).HasColumnName("IDCombination");
            entity.Property(e => e.StorageLocation)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Combination>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PK_PartnerProduct");

            entity.ToTable("Combination");

            entity.HasIndex(e => e.IDPartner, "IX_PartnerProduct_IDPartner");

            entity.HasIndex(e => e.IDProduct, "IX_PartnerProduct_IDProduct");

            entity.HasIndex(e => new { e.IDPartner, e.IDProduct, e.LocationID }, "UQ_PartnerProduct_IDPartner_IDProduct").IsUnique();

            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IDCross)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("")
                .HasColumnName("IDCross");
            entity.Property(e => e.IDPartner)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDPartner");
            entity.Property(e => e.IDProduct)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDProduct");
            entity.Property(e => e.LocationID).HasColumnName("LocationID");
            entity.Property(e => e.MOQ).HasColumnName("MOQ");
            entity.Property(e => e.PartnerType)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.PDR).HasColumnName("PDR");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.Property(e => e.LocationID).HasColumnName("LocationID");
            entity.Property(e => e.LocationCode)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.LocationGuid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LocationName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PartnerID)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PartnerID");
            entity.Property(e => e.PartnerType)
                .HasMaxLength(1)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.Guid);

            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ID)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PartnerType)
                .HasMaxLength(1)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PickingTransaction>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.ToTable("PickingTransactions");

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IDCard)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDCard");
            entity.Property(e => e.IsEnable)
                .HasDefaultValue(true)
                .HasColumnName("isEnable");
            entity.Property(e => e.ItemCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ItemPF)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ItemPF");
            entity.Property(e => e.LastUpdate).HasColumnType("datetime");
            entity.Property(e => e.Production)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Printer>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Printer");

            entity.Property(e => e.IdPrinter)
                .ValueGeneratedOnAdd()
                .HasColumnName("idPrinter");
            entity.Property(e => e.Ip)
                .HasMaxLength(50)
                .HasColumnName("ip");
            entity.Property(e => e.Port).HasColumnName("port");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Guid);

            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ID)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.UMBase)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("")
                .HasColumnName("UMBase");
        });

        modelBuilder.Entity<Sector>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.SectorGuid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.SectorID)
                .ValueGeneratedOnAdd()
                .HasColumnName("SectorID");
            entity.Property(e => e.SectorName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KFlowStatus>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PK_Status_1");

            entity.ToTable("Status");

            entity.Property(e => e.ID)
                .ValueGeneratedNever()
                .HasColumnName("ID");
            entity.Property(e => e.Color)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Description)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Enabled).HasDefaultValue(true);
            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
        });

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.HasKey(e => e.StorageLocationID);
            entity.Property(e => e.StorageLocationID)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("StorageLocationID");
        });

        modelBuilder.Entity<TblLog>(entity =>
        {
            entity.ToTable("tblLog");

            entity.Property(e => e.id).HasColumnName("id");
            entity.Property(e => e.IDMix)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValueSql("((0))")
                .HasColumnName("IDMix");
            entity.Property(e => e.IDRows)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasColumnName("IDRows");
            entity.Property(e => e.IDUser)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValueSql("((0))")
                .HasColumnName("IDUser");
            entity.Property(e => e.ODP)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("")
                .HasColumnName("ODP");
            entity.Property(e => e.PostingDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Text)
                .IsUnicode(false)
                .HasDefaultValue("");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Guid);

            entity.Property(e => e.Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Department)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.IDUser)
                .ValueGeneratedOnAdd()
                .HasColumnName("IDUser");
            entity.Property(e => e.LastModify)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("user");
            entity.Property(e => e.Surname)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("");
        });

        modelBuilder.Entity<VwKanbanBoard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vwKanbanBoard");

            entity.Property(e => e.Color)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IDCard)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDCard");
            entity.Property(e => e.IDCombinazione).HasColumnName("IDCombinazione");
            entity.Property(e => e.IDCross)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDCross");
            entity.Property(e => e.IDPartner)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDPartner");
            entity.Property(e => e.IDProduct)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IDProduct");
            entity.Property(e => e.LocationCode)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.LocationName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PartnerName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductName)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.StatusDescription)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UMBase)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("UMBase");
        });

        modelBuilder.Entity<Zpl>(entity =>
        {
            entity.ToTable("ZPL");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ZPL)
                .IsUnicode(false)
                .HasColumnName("ZPL");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
