namespace Calendare.Data.Options;

public class PostgresOptions
{
    public string? ConnectionString { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Dbname { get; set; }
}
