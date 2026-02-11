namespace Calendare.Server.Options;


public class BootstrapOptions : UserDefaultOptions
{
    public string Password { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
}


