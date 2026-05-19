using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PharmacyWarehouse.Models;
using System.IO;

namespace PharmacyWarehouse.Data;

public partial class PharmacyWarehouseContext : DbContext
{
    public PharmacyWarehouseContext() { }

    public PharmacyWarehouseContext(DbContextOptions<PharmacyWarehouseContext> options)
        : base(options) { }

    public virtual DbSet<Batch> Batches { get; set; }
    public virtual DbSet<BatchCorrectionLog> BatchCorrectionLogs { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Document> Documents { get; set; }
    public virtual DbSet<DocumentLine> DocumentLines { get; set; }
    public virtual DbSet<MdlpDocument> MdlpDocuments { get; set; }
    public virtual DbSet<MdlpDocumentHistory> MdlpDocumentHistories { get; set; }
    public virtual DbSet<MdlpSetting> MdlpSettings { get; set; }
    public virtual DbSet<MdlpSgtin> MdlpSgtins { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            optionsBuilder.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                options => options.EnableRetryOnFailure());

        }
#if DEBUG
        optionsBuilder.LogTo(msg => System.IO.File.AppendAllText(
           Path.Combine(Directory.GetCurrentDirectory(), "ef_log.txt"), msg + "\n"),
           Microsoft.Extensions.Logging.LogLevel.Error);
#endif

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Партии__3214EC27A3B57D1D");
            entity.HasIndex(e => e.IncomingDocumentId, "IX_Batches_IncomingDocumentId");
            entity.HasIndex(e => e.ProductId, "IX_Batches_ProductId");
            entity.HasIndex(e => e.SupplierId, "IX_Batches_SupplierId");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ArrivalDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PurchasePrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.SellingPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Series).HasMaxLength(100);
            entity.Property(e => e.Sgtin).HasMaxLength(27);

            entity.HasOne(d => d.IncomingDocument).WithMany(p => p.Batches)
                .HasForeignKey(d => d.IncomingDocumentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Batches_IncomingDocumentId");

            entity.HasOne(d => d.Product).WithMany(p => p.Batches)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batches_ProductId");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Batches)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batches_SupplierId");
        });

        modelBuilder.Entity<BatchCorrectionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BatchCorrectionLogs");
            entity.HasIndex(e => e.BatchId, "IX_BatchCorrectionLogs_BatchId");
            entity.HasIndex(e => e.CorrectionDocumentId, "IX_BatchCorrectionLogs_CorrectionDocumentId");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ChangedBy).HasMaxLength(100);
            entity.Property(e => e.CorrectionDate).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.FieldName).HasMaxLength(50);
            entity.Property(e => e.NewValue).HasMaxLength(100);
            entity.Property(e => e.OldValue).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.Batch).WithMany(p => p.BatchCorrectionLogs)
                .HasForeignKey(d => d.BatchId)
                .HasConstraintName("FK_BatchCorrectionLogs_BatchId");

            entity.HasOne(d => d.CorrectionDocument).WithMany(p => p.BatchCorrectionLogs)
                .HasForeignKey(d => d.CorrectionDocumentId)
                .HasConstraintName("FK_BatchCorrectionLogs_CorrectionDocumentId");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Категории__3214EC278AD91EA3");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Документы__3214EC278A955341");
            entity.HasIndex(e => e.OriginalDocumentId, "IX_Documents_OriginalDocumentId");
            entity.HasIndex(e => e.SupplierId, "IX_Documents_SupplierId");
            entity.HasIndex(e => e.Number, "UQ_Documents_Number").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Amount).HasDefaultValueSql("((0.0))").HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CorrectionReason).HasMaxLength(500);
            entity.Property(e => e.CorrectionType).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CustomerDocument).HasMaxLength(100);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.Date).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Number).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.SupplierInvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.WriteOffCommission).HasMaxLength(500);
            entity.Property(e => e.WriteOffReason).HasMaxLength(20);

            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.WriteOffReason).HasConversion<string>();
            entity.Property(e => e.CorrectionType).HasConversion<string>();

            entity.HasOne(d => d.OriginalDocument).WithMany(p => p.InverseOriginalDocument)
                .HasForeignKey(d => d.OriginalDocumentId)
                .HasConstraintName("FK_Documents_OriginalDocumentId");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Documents_SupplierId");
        });

        modelBuilder.Entity<DocumentLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Строки_документов__3214EC2788683C48");
            entity.HasIndex(e => e.CreatedBatchId, "IX_DocumentLines_CreatedBatchId");
            entity.HasIndex(e => e.DocumentId, "IX_DocumentLines_DocumentId");
            entity.HasIndex(e => e.ProductId, "IX_DocumentLines_ProductId");
            entity.HasIndex(e => e.SourceBatchId, "IX_DocumentLines_SourceBatchId");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CorrectionNotes).HasMaxLength(500);
            entity.Property(e => e.NewValue).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.OldValue).HasMaxLength(100);
            entity.Property(e => e.SellingPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Series).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.CreatedBatch).WithMany(p => p.DocumentLineCreatedBatches)
                .HasForeignKey(d => d.CreatedBatchId)
                .HasConstraintName("FK_DocumentLines_CreatedBatchId");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentLines)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("FK_DocumentLines_DocumentId");

            entity.HasOne(d => d.Product).WithMany(p => p.DocumentLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DocumentLines_ProductId");

            entity.HasOne(d => d.SourceBatch).WithMany(p => p.DocumentLineSourceBatches)
                .HasForeignKey(d => d.SourceBatchId)
                .HasConstraintName("FK_DocumentLines_SourceBatchId");
        });

        modelBuilder.Entity<MdlpDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MdlpDocuments");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.ErrorCode).HasMaxLength(10);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.MdlpDocumentId).HasMaxLength(100);
            entity.Property(e => e.OperationType).HasMaxLength(50);
            entity.Property(e => e.ProcessedAt).HasColumnType("datetime");
            entity.Property(e => e.SchemaVersion).HasMaxLength(10);
            entity.Property(e => e.SentAt).HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Ticket).HasMaxLength(36);

            entity.HasOne(d => d.Document).WithMany(p => p.MdlpDocuments)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MdlpDocuments_DocumentId");
        });

        modelBuilder.Entity<MdlpDocumentHistory>(entity =>
        {
            entity.ToTable("MdlpDocumentHistory");

            entity.HasIndex(e => e.MdlpDocumentId, "IX_MdlpDocumentHistory_MdlpDocumentId");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30);

            entity.HasOne(d => d.MdlpDocument).WithMany(p => p.MdlpDocumentHistories)
                .HasForeignKey(d => d.MdlpDocumentId)
                .HasConstraintName("FK_MdlpDocumentHistory_MdlpDocumentId");
        });

        modelBuilder.Entity<MdlpSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MdlpSettings");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ApiUrl).HasMaxLength(200);
            entity.Property(e => e.ClientId).HasMaxLength(100);
            entity.Property(e => e.MaxRetries).HasDefaultValue(3);
            entity.Property(e => e.OrgInn).HasMaxLength(12);
            entity.Property(e => e.OrgName).HasMaxLength(200);
            entity.Property(e => e.SimulatedDelaySeconds).HasDefaultValue(5);
            entity.Property(e => e.SimulatedErrorRate).HasDefaultValue(10);
            entity.Property(e => e.SubjectId).HasMaxLength(36);
            entity.Property(e => e.UseMock).HasDefaultValue(true);
        });

        modelBuilder.Entity<MdlpSgtin>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MdlpSgtins");
            entity.HasIndex(e => e.Sgtin, "UQ_MdlpSgtins_Sgtin").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PreviousStatus).HasMaxLength(20);
            entity.Property(e => e.Sgtin).HasMaxLength(27);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("InStock");
            entity.Property(e => e.StatusChangedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Batch).WithMany(p => p.MdlpSgtins)
                .HasForeignKey(d => d.BatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MdlpSgtins_BatchId");

            entity.HasOne(d => d.MdlpDocument).WithMany(p => p.MdlpSgtins)
                .HasForeignKey(d => d.MdlpDocumentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_MdlpSgtins_MdlpDocumentId");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Товары__3214EC2788EA4D93");
            entity.HasIndex(e => e.CategoryId, "IX_Products_CategoryId");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ArchiveComment).HasMaxLength(500);
            entity.Property(e => e.ArchiveDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ArchiveReason).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Gtin).HasMaxLength(14);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Manufacturer).HasMaxLength(100);
            entity.Property(e => e.MinRemainder).HasDefaultValue(10);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ReleaseForm).HasMaxLength(50);
            entity.Property(e => e.UnitOfMeasure)
                .HasMaxLength(20)
                .HasDefaultValue("шт.");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Products_CategoryId");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Поставщики__3214EC2715F63432");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BankAccount).HasMaxLength(20);
            entity.Property(e => e.BankName).HasMaxLength(200);
            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.Inn).HasMaxLength(12);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Users");
            entity.HasIndex(e => e.Login, "IX_Users_Login").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Login).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Role).HasConversion<string>();

        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
