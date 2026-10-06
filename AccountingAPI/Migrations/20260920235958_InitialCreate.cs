using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_AccountGroupHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Code_Group = table.Column<int>(type: "int", nullable: false),
                    Name_Group = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nature_Group = table.Column<int>(type: "int", nullable: false),
                    type_Group = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AccountGroupHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AccountKolHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Group_id = table.Column<int>(type: "int", nullable: false),
                    Code_kol = table.Column<int>(type: "int", nullable: false),
                    Name_kol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AccountKolHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AccountMoeinHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Kol_id = table.Column<int>(type: "int", nullable: false),
                    Code_moein = table.Column<int>(type: "int", nullable: false),
                    Name_moein = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AccountMoeinHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AccountTafziliHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Moein_id = table.Column<int>(type: "int", nullable: false),
                    Code_tafzili = table.Column<int>(type: "int", nullable: false),
                    Name_tafzili = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AccountTafziliHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserMobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_CompanyHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    X = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Y = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_CompanyHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_DatabaseHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    DbName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinancialYear = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_DatabaseHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_DocumentHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_DocumentHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_UserHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_UserHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Company",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NationalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    X = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Y = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Company", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_Company_tbl_User_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_UserToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_UserToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_UserToken_tbl_User_UserId",
                        column: x => x.UserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_AccountGroup",
                columns: table => new
                {
                    id_Group = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Code_Group = table.Column<int>(type: "int", nullable: false),
                    Name_Group = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nature_Group = table.Column<int>(type: "int", nullable: false),
                    type_Group = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_AccountGroup", x => x.id_Group);
                    table.ForeignKey(
                        name: "FK_tbl_AccountGroup_tbl_Company_Company_id",
                        column: x => x.Company_id,
                        principalTable: "tbl_Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_AccountGroup_tbl_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_CompanyDatabases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ServerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DatabaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectionString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_CompanyDatabases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_CompanyDatabases_tbl_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tbl_Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Databases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DbName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Databases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_Databases_tbl_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tbl_Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Document",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Document", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_Document_tbl_Company_Company_id",
                        column: x => x.Company_id,
                        principalTable: "tbl_Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Document_tbl_Document_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "tbl_Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_Document_tbl_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountKol",
                columns: table => new
                {
                    id_kol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Group_id = table.Column<int>(type: "int", nullable: false),
                    Code_kol = table.Column<int>(type: "int", nullable: false),
                    Name_kol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountKol", x => x.id_kol);
                    table.ForeignKey(
                        name: "FK_AccountKol_tbl_AccountGroup_Group_id",
                        column: x => x.Group_id,
                        principalTable: "tbl_AccountGroup",
                        principalColumn: "id_Group",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountKol_tbl_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_UserCompany",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DatabaseId = table.Column<int>(type: "int", nullable: true),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DisableDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeleteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOwner = table.Column<bool>(type: "bit", nullable: false),
                    CanSelect = table.Column<bool>(type: "bit", nullable: false),
                    CanInsert = table.Column<bool>(type: "bit", nullable: false),
                    CanUpdate = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_UserCompany", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_UserCompany_tbl_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tbl_Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_UserCompany_tbl_Databases_DatabaseId",
                        column: x => x.DatabaseId,
                        principalTable: "tbl_Databases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountMoein",
                columns: table => new
                {
                    id_moein = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Kol_id = table.Column<int>(type: "int", nullable: false),
                    Code_moein = table.Column<int>(type: "int", nullable: false),
                    Name_moein = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMoein", x => x.id_moein);
                    table.ForeignKey(
                        name: "FK_AccountMoein_AccountKol_Kol_id",
                        column: x => x.Kol_id,
                        principalTable: "AccountKol",
                        principalColumn: "id_kol",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMoein_tbl_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTafzili",
                columns: table => new
                {
                    id_tafzili = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Company_id = table.Column<int>(type: "int", nullable: false),
                    Fiscal_year = table.Column<int>(type: "int", nullable: false),
                    Moein_id = table.Column<int>(type: "int", nullable: false),
                    Code_tafzili = table.Column<int>(type: "int", nullable: false),
                    Name_tafzili = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTafzili", x => x.id_tafzili);
                    table.ForeignKey(
                        name: "FK_AccountTafzili_AccountMoein_Moein_id",
                        column: x => x.Moein_id,
                        principalTable: "AccountMoein",
                        principalColumn: "id_moein",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTafzili_tbl_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "tbl_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_DocumentRow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    tafzili_id = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_DocumentRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_DocumentRow_AccountTafzili_tafzili_id",
                        column: x => x.tafzili_id,
                        principalTable: "AccountTafzili",
                        principalColumn: "id_tafzili",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_DocumentRow_tbl_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "tbl_Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountKol_CreatedByUserId",
                table: "AccountKol",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountKol_Group_id",
                table: "AccountKol",
                column: "Group_id");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMoein_CreatedByUserId",
                table: "AccountMoein",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMoein_Kol_id",
                table: "AccountMoein",
                column: "Kol_id");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTafzili_CreatedByUserId",
                table: "AccountTafzili",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTafzili_Moein_id",
                table: "AccountTafzili",
                column: "Moein_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_AccountGroup_Company_id",
                table: "tbl_AccountGroup",
                column: "Company_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_AccountGroup_CreatedByUserId",
                table: "tbl_AccountGroup",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountGroupHistory_OriginalId",
                table: "tbl_AccountGroupHistory",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountKolHistory_OriginalId",
                table: "tbl_AccountKolHistory",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMoeinHistory_OriginalId",
                table: "tbl_AccountMoeinHistory",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTafziliHistory_OriginalId",
                table: "tbl_AccountTafziliHistory",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Company_RegisteredByUserId",
                table: "tbl_Company",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_CompanyDatabases_CompanyId",
                table: "tbl_CompanyDatabases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Databases_CompanyId",
                table: "tbl_Databases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Document_Company_Fiscal_Number",
                table: "tbl_Document",
                columns: new[] { "Company_id", "Fiscal_year", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Document_CreatedByUserId",
                table: "tbl_Document",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Document_OriginalDocumentId",
                table: "tbl_Document",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentHistory_OriginalId",
                table: "tbl_DocumentHistory",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_DocumentRow_DocumentId",
                table: "tbl_DocumentRow",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_DocumentRow_tafzili_id",
                table: "tbl_DocumentRow",
                column: "tafzili_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserCompany_CompanyId",
                table: "tbl_UserCompany",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserCompany_DatabaseId",
                table: "tbl_UserCompany",
                column: "DatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_UserToken_UserId",
                table: "tbl_UserToken",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_AccountGroupHistory");

            migrationBuilder.DropTable(
                name: "tbl_AccountKolHistory");

            migrationBuilder.DropTable(
                name: "tbl_AccountMoeinHistory");

            migrationBuilder.DropTable(
                name: "tbl_AccountTafziliHistory");

            migrationBuilder.DropTable(
                name: "tbl_AuditLogs");

            migrationBuilder.DropTable(
                name: "tbl_CompanyDatabases");

            migrationBuilder.DropTable(
                name: "tbl_CompanyHistories");

            migrationBuilder.DropTable(
                name: "tbl_DatabaseHistories");

            migrationBuilder.DropTable(
                name: "tbl_DocumentHistory");

            migrationBuilder.DropTable(
                name: "tbl_DocumentRow");

            migrationBuilder.DropTable(
                name: "tbl_UserCompany");

            migrationBuilder.DropTable(
                name: "tbl_UserHistories");

            migrationBuilder.DropTable(
                name: "tbl_UserToken");

            migrationBuilder.DropTable(
                name: "AccountTafzili");

            migrationBuilder.DropTable(
                name: "tbl_Document");

            migrationBuilder.DropTable(
                name: "tbl_Databases");

            migrationBuilder.DropTable(
                name: "AccountMoein");

            migrationBuilder.DropTable(
                name: "AccountKol");

            migrationBuilder.DropTable(
                name: "tbl_AccountGroup");

            migrationBuilder.DropTable(
                name: "tbl_Company");

            migrationBuilder.DropTable(
                name: "tbl_User");
        }
    }
}
