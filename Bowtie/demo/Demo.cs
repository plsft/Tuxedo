using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Tuxedo.Contrib;

namespace Bowtie.Demo;

// Demo models
[Table("Users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Users_Username")]
    [Unique("UQ_Users_Username")]
    public string Username { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    public string Email { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }
}

[Table("Documents")]
public class Document
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
    public string Content { get; set; } = "{}";
}

class Program
{
    static async Task Main()
    {
        Console.WriteLine("🎭 Bowtie Demo - DDL Generation");
        Console.WriteLine("==============================\n");

        var analyzer = new ModelAnalyzer();
        var modelTypes = new[] { typeof(User), typeof(Document) };
        var tables = analyzer.AnalyzeTypes(modelTypes);

        Console.WriteLine($"📊 Analyzed {tables.Count} models:");
        foreach (var table in tables)
        {
            Console.WriteLine($"  📋 {table.Name}: {table.Columns.Count} columns, {table.Indexes.Count} indexes");
        }
        Console.WriteLine();

        var generators = new IDdlGenerator[]
        {
            new SqlServerDdlGenerator(),
            new PostgreSqlDdlGenerator(), 
            new MySqlDdlGenerator(),
            new SqliteDdlGenerator()
        };

        foreach (var generator in generators)
        {
            Console.WriteLine($"🔧 {generator.Provider} DDL:");
            Console.WriteLine(new string('=', 50));
            
            foreach (var table in tables)
            {
                try
                {
                    var ddl = generator.GenerateCreateTable(table);
                    Console.WriteLine($"-- Table: {table.Name}");
                    Console.WriteLine(ddl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error generating {table.Name}: {ex.Message}");
                }
            }
            Console.WriteLine();
        }

        // Save scripts to files
        foreach (var generator in generators)
        {
            var scripts = generator.GenerateMigrationScript(new(), tables);
            var fullScript = string.Join("\n\n", scripts);
            var fileName = $"demo_schema_{generator.Provider.ToString().ToLower()}.sql";
            
            await File.WriteAllTextAsync(fileName, fullScript);
            Console.WriteLine($"💾 Generated {fileName} ({fullScript.Length} characters)");
        }

        Console.WriteLine("\n✅ Demo completed successfully!");
    }
}