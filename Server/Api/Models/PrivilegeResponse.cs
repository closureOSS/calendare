using System.Collections.Generic;

namespace Calendare.Server.Api.Models;

public class PrivilegeItemResponse
{
    public string? Id { get; set; }
    // public bool IsAbstract { get; init; }
    public string? Description { get; set; }
}

public class PrivilegeLineResponse
{
    public string? Uri { get; set; }
    public string? Username { get; set; }
    public string? Displayname { get; set; }
    public string? PrincipalType { get; set; }
    public List<PrivilegeItemResponse>? Grants { get; set; }
    public bool? IsIndirect { get; set; }
    public bool? IsInherited { get; set; }
    public bool? IsVirtual { get; set; }
}

public class PrivilegeGroupResponse
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public List<PrivilegeLineResponse>? Privileges { get; set; }
}


public class PrivilegeResponse
{
    public bool GrantedTo { get; set; }
    public List<PrivilegeGroupResponse> PrivilegeGroups { get; set; } = [];
    public bool? HasProxyWriteCollection { get; set; }
    public bool? HasProxyReadCollection { get; set; }
}

public class PrivilegePrincipalRequest
{
    public required string Username { get; set; }
    public bool DoRemove { get; set; }
}

public class PrivilegeGroupRequest
{
    public required string Code { get; set; }
    public List<PrivilegePrincipalRequest>? Principals { get; set; }
}

public class PrivilegeRequest
{
    public required string GrantorUri { get; set; }
    public required List<PrivilegeGroupRequest> Groups { get; set; }
}
