namespace Calendare.Server.Repository;

public class PrincipalQuery : RepositoryQuery
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool IncludeProxy { get; set; }

    public bool IsValid()
    {
        return !(string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Email));
    }
}
