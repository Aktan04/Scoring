using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scoring.Migrations
{
    /// <inheritdoc />
    public partial class AddedMlModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_coefficients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScaleOffset = table.Column<double>(type: "double precision", nullable: false),
                    Factor = table.Column<double>(type: "double precision", nullable: false),
                    ThresholdApprove = table.Column<int>(type: "integer", nullable: false),
                    ThresholdReview = table.Column<int>(type: "integer", nullable: false),
                    PtsAge21_25 = table.Column<int>(type: "integer", nullable: false),
                    PtsAge26_35 = table.Column<int>(type: "integer", nullable: false),
                    PtsAge36_45 = table.Column<int>(type: "integer", nullable: false),
                    PtsAge46_55 = table.Column<int>(type: "integer", nullable: false),
                    PtsAge56_65 = table.Column<int>(type: "integer", nullable: false),
                    PtsIncomeLt30K = table.Column<int>(type: "integer", nullable: false),
                    PtsIncome30_60K = table.Column<int>(type: "integer", nullable: false),
                    PtsIncome60_100K = table.Column<int>(type: "integer", nullable: false),
                    PtsIncomeGt100K = table.Column<int>(type: "integer", nullable: false),
                    PtsExp0 = table.Column<int>(type: "integer", nullable: false),
                    PtsExp1_3 = table.Column<int>(type: "integer", nullable: false),
                    PtsExp3_7 = table.Column<int>(type: "integer", nullable: false),
                    PtsExpGt7 = table.Column<int>(type: "integer", nullable: false),
                    PtsDtiLt30 = table.Column<int>(type: "integer", nullable: false),
                    PtsDti30_45 = table.Column<int>(type: "integer", nullable: false),
                    PtsDti45_60 = table.Column<int>(type: "integer", nullable: false),
                    PtsDtiGt60 = table.Column<int>(type: "integer", nullable: false),
                    PtsDep0 = table.Column<int>(type: "integer", nullable: false),
                    PtsDep1 = table.Column<int>(type: "integer", nullable: false),
                    PtsDep2 = table.Column<int>(type: "integer", nullable: false),
                    PtsDepGt3 = table.Column<int>(type: "integer", nullable: false),
                    PtsRealEstateYes = table.Column<int>(type: "integer", nullable: false),
                    PtsRealEstateNo = table.Column<int>(type: "integer", nullable: false),
                    PtsVehicleYes = table.Column<int>(type: "integer", nullable: false),
                    PtsVehicleNo = table.Column<int>(type: "integer", nullable: false),
                    Gini = table.Column<double>(type: "double precision", nullable: false),
                    TrainAuc = table.Column<double>(type: "double precision", nullable: false),
                    TrainSamples = table.Column<int>(type: "integer", nullable: false),
                    DefaultRate = table.Column<double>(type: "double precision", nullable: false),
                    FeatureIvJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TrainedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrainedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_coefficients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "training_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    Income = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpYears = table.Column<int>(type: "integer", nullable: false),
                    Dti = table.Column<decimal>(type: "numeric", nullable: false),
                    Dependents = table.Column<int>(type: "integer", nullable: false),
                    HasRealEstate = table.Column<bool>(type: "boolean", nullable: false),
                    HasVehicle = table.Column<bool>(type: "boolean", nullable: false),
                    HadDelinquency = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SourceApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_records", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_coefficients");

            migrationBuilder.DropTable(
                name: "training_records");
        }
    }
}
