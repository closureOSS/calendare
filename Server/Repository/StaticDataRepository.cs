using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;

namespace Calendare.Server.Repository;

public class StaticDataRepository
{
    public Dictionary<PrincipalTypes, PrincipalType> PrincipalTypeList { get; } = [];
    public Dictionary<RelationshipTypes, GrantType> RelationshipTypeList { get; } = [];
    public Dictionary<string, UsrCredentialType> UserAccessTypeList { get; } = [];

    public StaticDataRepository()
    {
        LoadData();
    }

    private void LoadData()
    {
        UserAccessTypeList[CredentialTypes.PasswordCode] = new() { Id = CredentialTypes.Password, Name = "Username+Password", Label = CredentialTypes.PasswordCode };
        UserAccessTypeList[CredentialTypes.AccessKeyCode] = new() { Id = CredentialTypes.AccessKey, Name = "Accesskey+Secret", Label = CredentialTypes.AccessKeyCode };
        UserAccessTypeList[CredentialTypes.JwtBearerCode] = new() { Id = CredentialTypes.JwtBearer, Name = "JwtBearer Token", Label = CredentialTypes.JwtBearerCode };
        PrincipalTypeList[PrincipalTypes.Individual] = new() { Id = 1, Name = "Person", Label = PrincipalTypeCode.Individual };
        PrincipalTypeList[PrincipalTypes.Resource] = new() { Id = 2, Name = "Resource", Label = PrincipalTypeCode.Resource };
        PrincipalTypeList[PrincipalTypes.Group] = new() { Id = 3, Name = "Group", Label = PrincipalTypeCode.Group };
        PrincipalTypeList[PrincipalTypes.Room] = new() { Id = 4, Name = "Room", Label = PrincipalTypeCode.Room };
        RelationshipTypeList[RelationshipTypes.Custom] = new()
        {
            Id = (int)RelationshipTypes.Custom,
            Name = "Custom",
            Confers = "C",
            Privileges = PrivilegeMask.None,
        };
        RelationshipTypeList[RelationshipTypes.Administers] = new()
        {
            Id = (int)RelationshipTypes.Administers,
            Name = "Administers",
            Confers = "A",
            Privileges = PrivilegeMask.All,
        };
        RelationshipTypeList[RelationshipTypes.ReadWrite] = new()
        {
            Id = (int)RelationshipTypes.ReadWrite,
            Name = "Read/Write",
            Confers = "RW",
            Privileges = PrivilegeMask.Write | PrivilegeMask.Read | PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadFreeBusy,
        };
        RelationshipTypeList[RelationshipTypes.Read] = new()
        {
            Id = (int)RelationshipTypes.Read,
            Name = "Read",
            Confers = "R",
            Privileges = PrivilegeMask.Read | PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadFreeBusy,
        };
        RelationshipTypeList[RelationshipTypes.Freebusy] = new()
        {
            Id = (int)RelationshipTypes.Freebusy,
            Name = "Free/Busy",
            Confers = "F",
            Privileges = PrivilegeMask.ReadFreeBusy,
        };
    }

    public GrantType FindCommonRelationship(PrivilegeMask privilegeMask)
    {
        var hit = RelationshipTypeList.Values.FirstOrDefault(rtl => rtl.Privileges == privilegeMask);
        if (hit is not null)
        {
            return hit;
        }
        return RelationshipTypeList[RelationshipTypes.Custom];
    }
}
