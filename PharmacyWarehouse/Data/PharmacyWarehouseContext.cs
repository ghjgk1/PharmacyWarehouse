using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Data;

public partial class PharmacyWarehouseContext : DbContext
{
    public PharmacyWarehouseContext()
    {
    }

    public PharmacyWarehouseContext(DbContextOptions<PharmacyWarehouseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Batch> Batches { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Document> Documents { get; set; }
    public virtual DbSet<DocumentLine> DocumentLines { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<BatchCorrectionLog> BatchCorrectionLogs { get; set; }
    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=localhost;Database=PharmacyWarehouse;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Партии__3214EC27A3B57D1D");

            entity.ToTable("Batches");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Series)
                .HasMaxLength(100)
                .IsRequired(); // Добавлено
            entity.Property(e => e.ExpirationDate)
                .IsRequired();
            entity.Property(e => e.ArrivalDate)
                .HasDefaultValueSql("(getdate())")
                .IsRequired();
            entity.Property(e => e.PurchasePrice)
                .HasColumnType("decimal(10, 2)")
                .IsRequired();
            entity.Property(e => e.SellingPrice)
                .HasColumnType("decimal(10, 2)")
                .IsRequired();
            entity.Property(e => e.Quantity)
                .IsRequired();
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            entity.HasOne(d => d.Product)
                .WithMany(p => p.Batches)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batches_ProductId");

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.Batches)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batches_SupplierId");

            entity.HasOne(d => d.IncomingDocument)
                .WithMany(p => p.Batches)
                .HasForeignKey(d => d.IncomingDocumentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Batches_IncomingDocumentId");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Категории__3214EC278AD91EA3");

            entity.ToTable("Categories");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsRequired(false);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Документы__3214EC278A955341");

            entity.ToTable("Documents");

            entity.HasIndex(e => e.Number, "UQ_Documents_Number").IsUnique();

            


            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Number)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Date)
                .HasDefaultValueSql("(getdate())")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .IsRequired();

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.OriginalDocumentId)
                .IsRequired(false);

            entity.Property(e => e.CorrectionType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired(false);

            entity.Property(e => e.CorrectionReason)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.SupplierInvoiceNumber)
                .HasMaxLength(50)
                .IsRequired(false);
            entity.Property(e => e.SupplierInvoiceDate)
                .IsRequired(false);

            entity.Property(e => e.CustomerName)
                .HasMaxLength(200)
                .IsRequired(false);
            entity.Property(e => e.CustomerDocument)
                .HasMaxLength(100)
                .IsRequired(false);

            entity.Property(e => e.WriteOffReason)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired(false);
            entity.Property(e => e.WriteOffCommission)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.Amount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)")
                .IsRequired(false);

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Documents_SupplierId");

            entity.HasOne(d => d.OriginalDocument)
                .WithMany(d => d.Corrections)  
                .HasForeignKey(d => d.OriginalDocumentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Documents_OriginalDocumentId");
        });

        modelBuilder.Entity<DocumentLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Строки_документов__3214EC2788683C48");

            entity.ToTable("DocumentLines");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.DocumentId)
                .IsRequired();
            entity.Property(e => e.ProductId)
                .IsRequired();
            entity.Property(e => e.Quantity)
                .IsRequired();
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(10, 2)")
                .IsRequired();
            entity.Property(e => e.SellingPrice)
                .HasColumnType("decimal(10, 2)")
                .IsRequired(false);
            entity.Property(e => e.Series)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.OldValue)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(e => e.NewValue)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(e => e.CorrectionNotes)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.HasOne(d => d.Document)
                .WithMany(p => p.DocumentLines)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DocumentLines_DocumentId");

            entity.HasOne(d => d.Product)
                .WithMany(p => p.DocumentLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DocumentLines_ProductId");

            entity.HasOne(d => d.CreatedBatch)
                .WithMany(p => p.DocumentLines)
                .HasForeignKey(d => d.CreatedBatchId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DocumentLines_CreatedBatchId");

            entity.HasOne(d => d.SourceBatch)
                .WithMany()
                .HasForeignKey(d => d.SourceBatchId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DocumentLines_SourceBatchId");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Товары__3214EC2788EA4D93");

            entity.ToTable("Products");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.ReleaseForm)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.UnitOfMeasure)
                .HasMaxLength(20)
                .HasDefaultValue("шт.")
                .IsRequired();
            entity.Property(e => e.MinRemainder)
                .HasDefaultValue(10)
                .IsRequired();
            entity.Property(e => e.RequiresPrescription)
                .HasDefaultValue(false)
                .IsRequired();
            entity.Property(e => e.IsSalesBlocked)
                .HasDefaultValue(false)
                .IsRequired();
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
            entity.Property(e => e.ArchiveDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .IsRequired(false);
            entity.Property(e => e.ArchiveReason)
                .HasMaxLength(200)
                .IsRequired(false);
            entity.Property(e => e.ArchiveComment)
                .HasMaxLength(500)
                .IsRequired(false);
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.HasOne(d => d.Category)
                .WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Products_CategoryId");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Поставщики__3214EC2715F63432");

            entity.ToTable("Suppliers");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Inn)
                .HasMaxLength(12)
                .IsRequired();
            entity.Property(e => e.BankAccount)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.BankName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

        });

        modelBuilder.Entity<BatchCorrectionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BatchCorrectionLogs");

            entity.ToTable("BatchCorrectionLogs");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.BatchId)
                .IsRequired();
            entity.Property(e => e.CorrectionDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .IsRequired();
            entity.Property(e => e.FieldName)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.OldValue)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(e => e.NewValue)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(e => e.ChangedBy)
                .HasMaxLength(100)
                .IsRequired(false);
            entity.Property(e => e.Reason)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.HasOne(d => d.Batch)
                .WithMany()
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BatchCorrectionLogs_BatchId");

            entity.HasOne(d => d.CorrectionDocument)
                .WithMany()
                .HasForeignKey(d => d.CorrectionDocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BatchCorrectionLogs_CorrectionDocumentId");
        });

        OnModelCreatingPartial(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Users");

            entity.ToTable("Users");

            entity.HasIndex(e => e.Login).IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Login)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Role)
                .HasConversion<string>() 
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
        });
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}