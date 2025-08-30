using Bowtie.Analysis;
using Bowtie.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Bowtie.NUnit.Tests.Analysis;

[TestFixture]
public class DataLossAnalyzerTests
{
    private DataLossAnalyzer _analyzer = null!;
    private Mock<ILogger<DataLossAnalyzer>> _mockLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<DataLossAnalyzer>>();
        _analyzer = new DataLossAnalyzer(_mockLogger.Object);
    }

    [Test]
    public void AnalyzeMigrationRisks_WithDroppedTable_ShouldDetectHighRisk()
    {
        // Arrange
        var currentTables = new List<TableModel>
        {
            new() 
            { 
                Name = "OldTable",
                Columns = new List<ColumnModel>
                {
                    new() { Name = "Id", PropertyType = typeof(int) },
                    new() { Name = "Data", PropertyType = typeof(string) }
                }
            }
        };
        var targetTables = new List<TableModel>(); // Empty - table will be dropped

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(currentTables, targetTables);

        // Assert
        risk.HasHighRiskOperations.Should().BeTrue();
        risk.RequiresConfirmation.Should().BeTrue();
        risk.Warnings.Should().ContainSingle();
        
        var warning = risk.Warnings[0];
        warning.Type.Should().Be(DataLossType.TableDrop);
        warning.Severity.Should().Be(DataLossSeverity.High);
        warning.Message.Should().Contain("Table 'OldTable' will be DROPPED");
        warning.TableName.Should().Be("OldTable");
    }

    [Test]
    public void AnalyzeMigrationRisks_WithDroppedColumn_ShouldDetectHighRisk()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), DataType = "INT" },
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR(100)" },
                new() { Name = "OldColumn", PropertyType = typeof(string), DataType = "NVARCHAR(50)" } // Will be dropped
            }
        };

        var targetTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), DataType = "INT" },
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR(100)" }
                // OldColumn missing
            }
        };

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasHighRiskOperations.Should().BeTrue();
        risk.RequiresConfirmation.Should().BeTrue();
        
        var columnDropWarning = risk.Warnings.Should().ContainSingle(w => w.Type == DataLossType.ColumnDrop).Subject;
        columnDropWarning.Message.Should().Contain("Column 'Users.OldColumn' will be DROPPED");
        columnDropWarning.ColumnName.Should().Be("OldColumn");
    }

    [Test]
    public void AnalyzeMigrationRisks_WithLengthReduction_ShouldDetectHighRisk()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "Users", 
            Columns = new List<ColumnModel>
            {
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR", MaxLength = 200 }
            }
        };

        var targetTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR", MaxLength = 50 } // Reduced
            }
        };

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasHighRiskOperations.Should().BeTrue();
        
        var lengthWarning = risk.Warnings.Should().ContainSingle(w => w.Type == DataLossType.LengthReduction).Subject;
        lengthWarning.Message.Should().Contain("max length reducing from 200 to 50");
        lengthWarning.Severity.Should().Be(DataLossSeverity.High);
    }

    [Test]
    public void AnalyzeMigrationRisks_WithDataTypeChange_ShouldDetectRisk()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "Products",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Price", PropertyType = typeof(decimal), DataType = "DECIMAL(18,4)" }
            }
        };

        var targetTable = new TableModel
        {
            Name = "Products", 
            Columns = new List<ColumnModel>
            {
                new() { Name = "Price", PropertyType = typeof(int), DataType = "INT" } // Type change
            }
        };

        // Act  
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasHighRiskOperations.Should().BeTrue();
        
        var typeWarning = risk.Warnings.Should().ContainSingle(w => w.Type == DataLossType.DataTypeChange).Subject;
        typeWarning.Message.Should().Contain("data type changing from 'DECIMAL(18,4)' to 'INT'");
        typeWarning.Severity.Should().Be(DataLossSeverity.High);
    }

    [Test]
    public void AnalyzeMigrationRisks_WithPrecisionReduction_ShouldDetectHighRisk()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "Products",
            Columns = new List<ColumnModel>
            {
                new() 
                { 
                    Name = "Price", 
                    PropertyType = typeof(decimal), 
                    DataType = "DECIMAL", 
                    Precision = 18, 
                    Scale = 4 
                }
            }
        };

        var targetTable = new TableModel
        {
            Name = "Products", 
            Columns = new List<ColumnModel>
            {
                new() 
                { 
                    Name = "Price", 
                    PropertyType = typeof(decimal), 
                    DataType = "DECIMAL", 
                    Precision = 10, 
                    Scale = 2 
                }
            }
        };

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasHighRiskOperations.Should().BeTrue();
        
        var precisionWarning = risk.Warnings.Should().ContainSingle(w => w.Type == DataLossType.PrecisionReduction).Subject;
        precisionWarning.Message.Should().Contain("precision/scale reducing");
        precisionWarning.Severity.Should().Be(DataLossSeverity.High);
    }

    [Test]
    public void AnalyzeMigrationRisks_WithNullabilityChange_ShouldDetectMediumRisk()
    {
        // Arrange  
        var currentTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Email", PropertyType = typeof(string), DataType = "NVARCHAR", IsNullable = true }
            }
        };

        var targetTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Email", PropertyType = typeof(string), DataType = "NVARCHAR", IsNullable = false }
            }
        };

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasMediumRiskOperations.Should().BeTrue();
        risk.RequiresConfirmation.Should().BeTrue();
        
        var nullabilityWarning = risk.Warnings.Should().ContainSingle(w => w.Type == DataLossType.NullabilityChange).Subject;
        nullabilityWarning.Message.Should().Contain("changing from nullable to non-nullable");
        nullabilityWarning.Severity.Should().Be(DataLossSeverity.Medium);
    }

    [Test]
    public void AnalyzeMigrationRisks_WithSafeChanges_ShouldDetectNoRisks()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), DataType = "INT" },
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR", MaxLength = 50 }
            }
        };

        var targetTable = new TableModel
        {
            Name = "Users",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), DataType = "INT" },
                new() { Name = "Username", PropertyType = typeof(string), DataType = "NVARCHAR", MaxLength = 100 }, // Length increase - safe
                new() { Name = "NewColumn", PropertyType = typeof(string), DataType = "NVARCHAR" } // New column - safe
            }
        };

        // Act
        var risk = _analyzer.AnalyzeMigrationRisks(new[] { currentTable }.ToList(), new[] { targetTable }.ToList());

        // Assert
        risk.HasHighRiskOperations.Should().BeFalse();
        risk.HasMediumRiskOperations.Should().BeFalse();
        risk.RequiresConfirmation.Should().BeFalse();
        risk.Warnings.Should().BeEmpty();
    }

    [Test]
    public void LogDataLossWarnings_WithNoRisks_ShouldLogSuccess()
    {
        // Arrange
        var risk = new DataLossRisk { Warnings = new List<DataLossWarning>() };

        // Act
        _analyzer.LogDataLossWarnings(risk);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No data loss risks detected")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void LogDataLossWarnings_WithHighRiskWarnings_ShouldLogErrors()
    {
        // Arrange
        var risk = new DataLossRisk 
        { 
            Warnings = new List<DataLossWarning>
            {
                new()
                {
                    Type = DataLossType.TableDrop,
                    Severity = DataLossSeverity.High,
                    Message = "Table 'OldTable' will be DROPPED",
                    TableName = "OldTable"
                }
            },
            HasHighRiskOperations = true
        };

        // Act
        _analyzer.LogDataLossWarnings(risk);

        // Assert  
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DATA LOSS WARNINGS DETECTED")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HIGH RISK OPERATIONS DETECTED")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}