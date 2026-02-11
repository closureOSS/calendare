using System.Collections.Generic;
using Calendare.Data.Models;

namespace Calendare.Server.Repository;

public class PrincipalListQuery : RepositoryQuery
{
    public List<PrincipalType>? PrincipalTypes { get; set; }
    public bool Unrestricted { get; set; }
    public bool IncludeSystemAccounts { get; set; }
    public string? SearchTerm { get; set; }
}
