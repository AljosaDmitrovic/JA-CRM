using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JaCrm.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BaseCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    WorkingHoursPerMonth = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    StaleThresholdDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DateFormat = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                    table.CheckConstraint("CK_AppSettings_Hours", "CAST(\"WorkingHoursPerMonth\" AS REAL) > 0");
                    table.CheckConstraint("CK_AppSettings_Singleton", "\"Id\" = 1");
                    table.CheckConstraint("CK_AppSettings_Stale", "\"StaleThresholdDays\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    WebsiteDomain = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CareersUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Headquarters = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Size = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.CheckConstraint("CK_Companies_Rating", "\"Rating\" IS NULL OR (\"Rating\" BETWEEN 1 AND 5)");
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    RateToBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UpdatedOn = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRates_Positive", "CAST(\"RateToBase\" AS REAL) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Technologies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technologies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Seniority = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SourceDetail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    WorkArrangement = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    RemoteEligibility = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OpenToMyLocation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RelocationSupport = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RelocationNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EmploymentType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    RequiredExperienceYears = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredExperienceText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DateDiscovered = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ApplicationDeadline = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PostedSalary_Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    PostedSalary_Max = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    PostedSalary_Min = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    PostedSalary_Period = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    PostedSalary_Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.CheckConstraint("CK_Vacancies_ExperienceYears", "\"RequiredExperienceYears\" IS NULL OR \"RequiredExperienceYears\" BETWEEN 0 AND 60");
                    table.ForeignKey(
                        name: "FK_Vacancies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VacancyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ReasonForApplying = table.Column<string>(type: "TEXT", nullable: true),
                    CompensationNotes = table.Column<string>(type: "TEXT", nullable: true),
                    CvVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CoverLetter = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    PostingSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedSalary_Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    ExpectedSalary_Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    ExpectedSalary_Period = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ExpectedSalary_Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OfferedSalary_Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    OfferedSalary_Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    OfferedSalary_Period = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OfferedSalary_Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VacancyPreferredTechnologies",
                columns: table => new
                {
                    VacancyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechnologyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyPreferredTechnologies", x => new { x.VacancyId, x.TechnologyId });
                    table.ForeignKey(
                        name: "FK_VacancyPreferredTechnologies_Technologies_TechnologyId",
                        column: x => x.TechnologyId,
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyPreferredTechnologies_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VacancyRequiredTechnologies",
                columns: table => new
                {
                    VacancyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechnologyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyRequiredTechnologies", x => new { x.VacancyId, x.TechnologyId });
                    table.ForeignKey(
                        name: "FK_VacancyRequiredTechnologies_Technologies_TechnologyId",
                        column: x => x.TechnologyId,
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyRequiredTechnologies_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistory_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "BaseCurrency", "DateFormat", "StaleThresholdDays", "Theme", "WorkingHoursPerMonth" },
                values: new object[] { 1, "EUR", "DayMonthYear", 14, "System", 168m });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistory_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistory",
                columns: new[] { "ApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistory_ChangedAt",
                table: "ApplicationStatusHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistory_ToStatus",
                table: "ApplicationStatusHistory",
                column: "ToStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IsArchived",
                table: "Applications",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UpdatedAt",
                table: "Applications",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_VacancyId",
                table: "Applications",
                column: "VacancyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_IsArchived",
                table: "Companies",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NormalizedName",
                table: "Companies",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_WebsiteDomain",
                table: "Companies",
                column: "WebsiteDomain");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_Currency",
                table: "ExchangeRates",
                column: "Currency",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_NormalizedName",
                table: "Technologies",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_ApplicationDeadline",
                table: "Vacancies",
                column: "ApplicationDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_CompanyId",
                table: "Vacancies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_IsArchived",
                table: "Vacancies",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyPreferredTechnologies_TechnologyId",
                table: "VacancyPreferredTechnologies",
                column: "TechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRequiredTechnologies_TechnologyId",
                table: "VacancyRequiredTechnologies",
                column: "TechnologyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ApplicationStatusHistory");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "VacancyPreferredTechnologies");

            migrationBuilder.DropTable(
                name: "VacancyRequiredTechnologies");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Technologies");

            migrationBuilder.DropTable(
                name: "Vacancies");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
