using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyWarehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndStorageTemp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Категории__3214EC278AD91EA3", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InventoryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    CompletedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MdlpSettings",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UseMock = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ApiUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrgInn = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    OrgName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    SimulatedDelaySeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    SimulatedErrorRate = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    MaxRetries = table.Column<int>(type: "int", nullable: false, defaultValue: 3)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdlpSettings", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Inn = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    BankAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Поставщики__3214EC2715F63432", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    ReleaseForm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresPrescription = table.Column<bool>(type: "bit", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "шт."),
                    MinRemainder = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSalesBlocked = table.Column<bool>(type: "bit", nullable: false),
                    ArchiveDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    ArchiveReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArchiveComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Gtin = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    IsTracked = table.Column<bool>(type: "bit", nullable: false),
                    StorageTemperatureMin = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    StorageTemperatureMax = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    StorageConditions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Товары__3214EC2788EA4D93", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Products_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    SignedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true, defaultValueSql: "((0.0))"),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierInvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierInvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerDocument = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WriteOffReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WriteOffCommission = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: true),
                    CorrectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Документы__3214EC278A955341", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Documents_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "Documents",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Documents_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IncomingDocumentId = table.Column<int>(type: "int", nullable: true),
                    Sgtin = table.Column<string>(type: "nvarchar(27)", maxLength: 27, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Партии__3214EC27A3B57D1D", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Batches_IncomingDocumentId",
                        column: x => x.IncomingDocumentId,
                        principalTable: "Documents",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Batches_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Batches_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MdlpDocuments",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    MdlpDocumentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    SchemaVersion = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Ticket = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdlpDocuments", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MdlpDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BatchCorrectionLogs",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    CorrectionDocumentId = table.Column<int>(type: "int", nullable: false),
                    CorrectionDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    FieldName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NewValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchCorrectionLogs", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BatchCorrectionLogs_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BatchCorrectionLogs_CorrectionDocumentId",
                        column: x => x.CorrectionDocumentId,
                        principalTable: "Documents",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLines",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBatchId = table.Column<int>(type: "int", nullable: true),
                    SourceBatchId = table.Column<int>(type: "int", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrectionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Строки_документов__3214EC2788683C48", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DocumentLines_CreatedBatchId",
                        column: x => x.CreatedBatchId,
                        principalTable: "Batches",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_DocumentLines_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentLines_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_DocumentLines_SourceBatchId",
                        column: x => x.SourceBatchId,
                        principalTable: "Batches",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "InventoryLines",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: false),
                    ActualQuantity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLines", x => x.ID);
                    table.ForeignKey(
                        name: "FK_InventoryLines_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_InventoryLines_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MdlpDocumentHistory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MdlpDocumentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdlpDocumentHistory", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MdlpDocumentHistory_MdlpDocumentId",
                        column: x => x.MdlpDocumentId,
                        principalTable: "MdlpDocuments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MdlpSgtins",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    Sgtin = table.Column<string>(type: "nvarchar(27)", maxLength: 27, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "InStock"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MdlpDocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdlpSgtins", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MdlpSgtins_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_MdlpSgtins_MdlpDocumentId",
                        column: x => x.MdlpDocumentId,
                        principalTable: "MdlpDocuments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchCorrectionLogs_BatchId",
                table: "BatchCorrectionLogs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchCorrectionLogs_CorrectionDocumentId",
                table: "BatchCorrectionLogs",
                column: "CorrectionDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_IncomingDocumentId",
                table: "Batches",
                column: "IncomingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_ProductId",
                table: "Batches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_SupplierId",
                table: "Batches",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_CreatedBatchId",
                table: "DocumentLines",
                column: "CreatedBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_DocumentId",
                table: "DocumentLines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_ProductId",
                table: "DocumentLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_SourceBatchId",
                table: "DocumentLines",
                column: "SourceBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OriginalDocumentId",
                table: "Documents",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SupplierId",
                table: "Documents",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UQ_Documents_Number",
                table: "Documents",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLines_BatchId",
                table: "InventoryLines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLines_InventoryId",
                table: "InventoryLines",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MdlpDocumentHistory_MdlpDocumentId",
                table: "MdlpDocumentHistory",
                column: "MdlpDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MdlpDocuments_DocumentId",
                table: "MdlpDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MdlpSgtins_BatchId",
                table: "MdlpSgtins",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MdlpSgtins_MdlpDocumentId",
                table: "MdlpSgtins",
                column: "MdlpDocumentId");

            migrationBuilder.CreateIndex(
                name: "UQ_MdlpSgtins_Sgtin",
                table: "MdlpSgtins",
                column: "Sgtin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchCorrectionLogs");

            migrationBuilder.DropTable(
                name: "DocumentLines");

            migrationBuilder.DropTable(
                name: "InventoryLines");

            migrationBuilder.DropTable(
                name: "MdlpDocumentHistory");

            migrationBuilder.DropTable(
                name: "MdlpSettings");

            migrationBuilder.DropTable(
                name: "MdlpSgtins");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropTable(
                name: "MdlpDocuments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
