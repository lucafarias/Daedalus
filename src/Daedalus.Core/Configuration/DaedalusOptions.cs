namespace Daedalus.Core.Configuration;

public sealed class DaedalusOptions
{
    public const string SectionName = "Daedalus";

    // ----- Ollama connection ------------------------------------------------
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5-coder:7b";
    public string? OllamaExecutablePath { get; set; }
    public int StartupTimeoutSeconds { get; set; } = 30;

    // Aumentato a 300s per gestire il cold start del modello (7B)
    public int RequestTimeoutSeconds { get; set; } = 600;

    public int MaxConcurrentRequests { get; set; } = 1;

    // ----- Prompt -----------------------------------------------------------
    public string? PromptFilePath { get; set; }
    public string DefaultPromptFileName { get; set; } = "DaedalusPrompt.default.txt";

    // ----- Solution scanning ------------------------------------------------
    public string[] ExcludedDirectories { get; set; } = { "bin", "obj", ".vs", ".git", "node_modules" };
    public string[] ExcludedFilePatterns { get; set; } = { "*.Designer.cs", "*.g.cs", "*.Generated.cs" };
    public string[] TargetExtensions { get; set; } = { ".cs", ".xaml", ".csproj" };
    public string[] TargetFileNames { get; set; } = { "appsettings.json" };
    public long MaxFileSizeBytes { get; set; } = 200L * 1024L;

    // ----- Logging ----------------------------------------------------------
    public string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Daedalus", "logs");
    public string LogMinimumLevel { get; set; } = "Information";

    // ----- Backup -----------------------------------------------------------
    public string BackupDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Daedalus", "backups");
    public int BackupRetentionDays { get; set; } = 7;

    // ----- Solution ---------------------------------------------------------
    public string? SolutionRootPath { get; set; }
}