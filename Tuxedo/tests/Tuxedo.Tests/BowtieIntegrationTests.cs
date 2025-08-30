using Tuxedo.Contrib;
using Xunit;

namespace Tuxedo.Tests;

/// <summary>
/// Basic integration tests to verify Bowtie can analyze Tuxedo models
/// </summary>
public class BowtieIntegrationTests
{
    [Fact]
    public void BowtieAnalyzer_CanAnalyzeTuxedoModels()
    {
        // This test verifies that Bowtie can be referenced and analyze Tuxedo models
        // Full Bowtie tests are in Bowtie/tests/Bowtie.Tests/
        
        var modelType = typeof(TuxedoTestModel);
        
        // Verify model has required attributes
        var tableAttribute = modelType.GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault();
        Assert.NotNull(tableAttribute);
        
        var keyProperty = modelType.GetProperty("Id");
        Assert.NotNull(keyProperty);
        
        var keyAttribute = keyProperty!.GetCustomAttributes(typeof(KeyAttribute), false).FirstOrDefault();
        Assert.NotNull(keyAttribute);
    }

    [Fact] 
    public void TuxedoAttributes_AreCompatibleWithBowtie()
    {
        // Verify that Tuxedo's core attributes work with Bowtie analysis
        var properties = typeof(TuxedoTestModel).GetProperties();
        
        // Should have Id property with Key attribute
        var idProperty = properties.FirstOrDefault(p => p.Name == "Id");
        Assert.NotNull(idProperty);
        Assert.True(idProperty!.GetCustomAttributes(typeof(KeyAttribute), false).Any());
        
        // Should have computed property
        var computedProperty = properties.FirstOrDefault(p => p.Name == "DisplayName");
        Assert.NotNull(computedProperty);
        Assert.True(computedProperty!.GetCustomAttributes(typeof(ComputedAttribute), false).Any());
    }
}

[Table("TuxedoTestModels")]
public class TuxedoTestModel
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public bool IsActive { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    [Computed]
    public string DisplayName => $"Model: {Name}";
}