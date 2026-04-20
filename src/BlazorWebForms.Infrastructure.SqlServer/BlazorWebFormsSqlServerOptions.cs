namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class BlazorWebFormsSqlServerOptions
{
    public string ConnectionString { get; set; } = "Server=(localdb)\\MSSQLLocalDB;Database=BlazorWebForms;";
    public string StorageRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
    public string SchemaName { get; set; } = "bwf";
}
