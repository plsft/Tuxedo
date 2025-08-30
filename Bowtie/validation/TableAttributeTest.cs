using System.Reflection;
using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Tuxedo.Contrib;

namespace Bowtie.TableAttributeTest;

// Classes WITH [Table] attribute - should be processed
[Table("ProcessedTable1")]
public class ProcessedModel1
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Table("ProcessedTable2")]
public class ProcessedModel2
{
    [Key]
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

// Class WITHOUT [Table] attribute - should be ignored
public class IgnoredModel
{
    public int Id { get; set; }
    public string ShouldNotBeProcessed { get; set; } = string.Empty;
}

// Abstract class with [Table] - should be ignored
[Table("AbstractTable")]
public abstract class AbstractModel
{
    public int Id { get; set; }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("🧪 Table Attribute Processing Test");
        Console.WriteLine("==================================");

        var analyzer = new ModelAnalyzer();

        // Test assembly analysis
        var assembly = typeof(Program).Assembly;
        var tables = analyzer.AnalyzeAssembly(assembly);

        Console.WriteLine($"📊 Assembly contains {assembly.GetTypes().Length} total types");
        Console.WriteLine($"✅ Bowtie processed {tables.Count} table models");
        Console.WriteLine();

        Console.WriteLine("📋 Processed Tables:");
        foreach (var table in tables)
        {
            Console.WriteLine($"  ✅ {table.Name} (from {table.ModelType.Name})");
        }

        Console.WriteLine();

        // Test type filtering
        var allTypes = new[]
        {
            typeof(ProcessedModel1),   // Has [Table] - should be included
            typeof(ProcessedModel2),   // Has [Table] - should be included  
            typeof(IgnoredModel),      // No [Table] - should be ignored
            typeof(AbstractModel)      // Abstract - should be ignored
        };

        var filteredTables = analyzer.AnalyzeTypes(allTypes);

        Console.WriteLine("🔍 Type Filtering Test:");
        Console.WriteLine($"  Input: {allTypes.Length} types");
        Console.WriteLine($"  Output: {filteredTables.Count} table models");
        Console.WriteLine();

        foreach (var type in allTypes)
        {
            var hasTable = type.GetCustomAttribute<TableAttribute>() != null;
            var isProcessed = filteredTables.Any(t => t.ModelType == type);
            var shouldProcess = hasTable && !type.IsAbstract;

            var status = shouldProcess switch
            {
                true when isProcessed => "✅ CORRECTLY PROCESSED",
                false when !isProcessed => "✅ CORRECTLY IGNORED", 
                true when !isProcessed => "❌ ERROR: Should have been processed",
                false when isProcessed => "❌ ERROR: Should have been ignored",
            };

            Console.WriteLine($"  {type.Name}: {status}");
            Console.WriteLine($"    Has [Table]: {hasTable}, Is Abstract: {type.IsAbstract}, Processed: {isProcessed}");
        }

        Console.WriteLine();
        Console.WriteLine("🎯 VALIDATION RESULTS:");
        
        // Verify correct processing
        var shouldHaveBeenProcessed = allTypes.Where(t => t.GetCustomAttribute<TableAttribute>() != null && !t.IsAbstract).ToList();
        var actuallyProcessed = filteredTables.Select(t => t.ModelType).ToList();

        if (shouldHaveBeenProcessed.Count == actuallyProcessed.Count &&
            shouldHaveBeenProcessed.All(t => actuallyProcessed.Contains(t)))
        {
            Console.WriteLine("✅ Table attribute processing works CORRECTLY");
            Console.WriteLine("✅ All classes with [Table] attribute are processed");
            Console.WriteLine("✅ Classes without [Table] attribute are ignored");
            Console.WriteLine("✅ Abstract classes are properly ignored");
        }
        else
        {
            Console.WriteLine("❌ Table attribute processing has issues");
        }
    }
}