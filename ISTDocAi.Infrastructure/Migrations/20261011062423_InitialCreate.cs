using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISTDocAi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IAM_Action",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Action", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Application",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Application", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_PermissionAudit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TargetRoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PermissionChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceRoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_PermissionAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Role",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_User",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsAdministrator = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ActionCategory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ActionCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_Attachments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileExtension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferenceSerial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ErrorLog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ErrorTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErrorTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ErrorLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_Notifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralMessageEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneralMessageAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventNameAndLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetURL = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_RequestLog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RequestTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestParameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestInitTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestCompleteTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_RequestLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    RunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastExecutionTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ScheduledTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pub_Setting",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CompanyNameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CountryId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyPhoneOne = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyPhoneTwo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyWebsite = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyFax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultCurrencyId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyLogoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportHeaderPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportFooterPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyLogoBinaryData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ReportHeaderBinaryData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ReportFooterBinaryData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CompanyLogoContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportHeaderContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportFooterContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyLogoHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportHeaderHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportFooterHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFolderPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttachedFileMaxSize = table.Column<int>(type: "int", nullable: true),
                    IsEmailEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmailHost = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailPort = table.Column<int>(type: "int", nullable: true),
                    EmailUsername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailPassword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailEnableSSL = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_Setting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Module",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ParentId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Module", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_Module_IAM_Application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "IAM_Application",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IAM_Module_IAM_Module_ParentId",
                        column: x => x.ParentId,
                        principalTable: "IAM_Module",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_RoleClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_RoleClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_RoleClaim_IAM_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "IAM_Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_UserClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_UserClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_UserClaim_IAM_User_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_UserLogin",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_UserLogin", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_IAM_UserLogin_IAM_User_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_UserRole",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_IAM_UserRole_IAM_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "IAM_Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IAM_UserRole_IAM_User_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_UserToken",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_UserToken", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_IAM_UserToken_IAM_User_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ActionType",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionCategoryID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ActionType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pub_ActionType_Pub_ActionCategory_ActionCategoryID",
                        column: x => x.ActionCategoryID,
                        principalTable: "Pub_ActionCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pub_NotificationUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NotificationId = table.Column<string>(type: "nvarchar(16)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SendingWay = table.Column<int>(type: "int", nullable: false),
                    IsAssigned = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_NotificationUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pub_NotificationUsers_IAM_User_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pub_NotificationUsers_Pub_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Pub_Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ScheduledTaskLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledTaskId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExecutionTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ScheduledTaskLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pub_ScheduledTaskLogs_Pub_ScheduledTasks_ScheduledTaskId",
                        column: x => x.ScheduledTaskId,
                        principalTable: "Pub_ScheduledTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Page",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ModuleId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ParentPageId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShowInSidebar = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    OpenInNewTab = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Page", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_Page_IAM_Module_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "IAM_Module",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IAM_Page_IAM_Page_ParentPageId",
                        column: x => x.ParentPageId,
                        principalTable: "IAM_Page",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pub_ActionLog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionTypeID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionID = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActionNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionDetails = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_ActionLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pub_ActionLog_Pub_ActionType_ActionTypeID",
                        column: x => x.ActionTypeID,
                        principalTable: "Pub_ActionType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pub_NotificationUserDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NotificationUserId = table.Column<string>(type: "nvarchar(16)", nullable: false),
                    SendingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedNotificationStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pub_NotificationUserDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pub_NotificationUserDetails_Pub_NotificationUsers_NotificationUserId",
                        column: x => x.NotificationUserId,
                        principalTable: "Pub_NotificationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IAM_PageAction",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PageId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActionId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_PageAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_PageAction_IAM_Action_ActionId",
                        column: x => x.ActionId,
                        principalTable: "IAM_Action",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IAM_PageAction_IAM_Page_PageId",
                        column: x => x.PageId,
                        principalTable: "IAM_Page",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IAM_Module_ApplicationId",
                table: "IAM_Module",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_Module_ParentId",
                table: "IAM_Module",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_Page_ModuleId",
                table: "IAM_Page",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_Page_ParentPageId",
                table: "IAM_Page",
                column: "ParentPageId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PageAction_ActionId",
                table: "IAM_PageAction",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PageAction_PageId",
                table: "IAM_PageAction",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PageAction_PageId_ActionId",
                table: "IAM_PageAction",
                columns: new[] { "PageId", "ActionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PageAction_PermissionKey",
                table: "IAM_PageAction",
                column: "PermissionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PermissionAudit_PerformedAt",
                table: "IAM_PermissionAudit",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PermissionAudit_PerformedByUserId",
                table: "IAM_PermissionAudit",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PermissionAudit_SourceRoleId",
                table: "IAM_PermissionAudit",
                column: "SourceRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PermissionAudit_TargetRoleId",
                table: "IAM_PermissionAudit",
                column: "TargetRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_PermissionAudit_TargetUserId",
                table: "IAM_PermissionAudit",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "IAM_Role",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_RoleClaim_RoleId",
                table: "IAM_RoleClaim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "IAM_User",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_User_Email_Unique",
                table: "IAM_User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_User_UserName_Unique",
                table: "IAM_User",
                column: "UserName",
                unique: true,
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "IAM_User",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_UserClaim_UserId",
                table: "IAM_UserClaim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_UserLogin_UserId",
                table: "IAM_UserLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_UserRole_RoleId",
                table: "IAM_UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Pub_ActionLog_ActionTypeID",
                table: "Pub_ActionLog",
                column: "ActionTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Pub_ActionType_ActionCategoryID",
                table: "Pub_ActionType",
                column: "ActionCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Pub_Attachments_ReferenceType_ReferenceId_StoredFileName",
                table: "Pub_Attachments",
                columns: new[] { "ReferenceType", "ReferenceId", "StoredFileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pub_NotificationUserDetails_NotificationUserId",
                table: "Pub_NotificationUserDetails",
                column: "NotificationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pub_NotificationUsers_NotificationId_UserId",
                table: "Pub_NotificationUsers",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pub_NotificationUsers_UserId",
                table: "Pub_NotificationUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskLog_ExecutionTimeUtc",
                table: "Pub_ScheduledTaskLogs",
                column: "ExecutionTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskLog_ScheduledTaskId",
                table: "Pub_ScheduledTaskLogs",
                column: "ScheduledTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTask_Interval_LastExecution",
                table: "Pub_ScheduledTasks",
                column: "LastExecutionTimeUtc",
                filter: "[IsActive] = 1 AND [IntervalMinutes] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTask_LockedUntilUtc",
                table: "Pub_ScheduledTasks",
                column: "LockedUntilUtc",
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTask_OneTime_RunAtUtc",
                table: "Pub_ScheduledTasks",
                column: "RunAtUtc",
                filter: "[IsActive] = 1 AND [RunAtUtc] IS NOT NULL AND [LastExecutionTimeUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ScheduledTask_Name_ReferenceId_Active",
                table: "Pub_ScheduledTasks",
                columns: new[] { "Name", "ReferenceId" },
                unique: true,
                filter: "[IsActive] = 1 AND [ReferenceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IAM_PageAction");

            migrationBuilder.DropTable(
                name: "IAM_PermissionAudit");

            migrationBuilder.DropTable(
                name: "IAM_RoleClaim");

            migrationBuilder.DropTable(
                name: "IAM_UserClaim");

            migrationBuilder.DropTable(
                name: "IAM_UserLogin");

            migrationBuilder.DropTable(
                name: "IAM_UserRole");

            migrationBuilder.DropTable(
                name: "IAM_UserToken");

            migrationBuilder.DropTable(
                name: "Pub_ActionLog");

            migrationBuilder.DropTable(
                name: "Pub_Attachments");

            migrationBuilder.DropTable(
                name: "Pub_ErrorLog");

            migrationBuilder.DropTable(
                name: "Pub_NotificationUserDetails");

            migrationBuilder.DropTable(
                name: "Pub_RequestLog");

            migrationBuilder.DropTable(
                name: "Pub_ScheduledTaskLogs");

            migrationBuilder.DropTable(
                name: "Pub_Setting");

            migrationBuilder.DropTable(
                name: "IAM_Action");

            migrationBuilder.DropTable(
                name: "IAM_Page");

            migrationBuilder.DropTable(
                name: "IAM_Role");

            migrationBuilder.DropTable(
                name: "Pub_ActionType");

            migrationBuilder.DropTable(
                name: "Pub_NotificationUsers");

            migrationBuilder.DropTable(
                name: "Pub_ScheduledTasks");

            migrationBuilder.DropTable(
                name: "IAM_Module");

            migrationBuilder.DropTable(
                name: "Pub_ActionCategory");

            migrationBuilder.DropTable(
                name: "IAM_User");

            migrationBuilder.DropTable(
                name: "Pub_Notifications");

            migrationBuilder.DropTable(
                name: "IAM_Application");
        }
    }
}
