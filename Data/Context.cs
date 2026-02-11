using System.Reflection;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Calendare.Data;
/*
    # dotnet ef database drop
    dotnet ef migrations remove  --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\
    dotnet ef migrations add CreateInitial --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\
    dotnet ef migrations script --idempotent --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\  --output erm.sql
    dotnet ef database update --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\

    dotnet ef migrations add CalDav1 --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\

     --msbuildprojectextensionspath ..\artifacts\obj\Calendare.Data\
*/
public class CalendareContext : DbContext
{
    public CalendareContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseHiLo();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public DbSet<Usr> Usr => Set<Usr>();
    public DbSet<UsrCredential> UsrCredential => Set<UsrCredential>();
    public DbSet<UsrCredentialType> UsrCredentialType => Set<UsrCredentialType>();
    public DbSet<PrincipalType> PrincipalType => Set<PrincipalType>();
    public DbSet<GrantType> GrantType => Set<GrantType>();
    public DbSet<GrantRelation> GrantRelation => Set<GrantRelation>();

    public DbSet<Collection> Collection => Set<Collection>();
    public DbSet<CollectionObject> CollectionObject => Set<CollectionObject>();
    public DbSet<CollectionGroup> CollectionGroup => Set<CollectionGroup>();
    public DbSet<ObjectCalendar> Calendar => Set<ObjectCalendar>();
    public DbSet<ObjectCalendarAttendee> CalendarAttendee => Set<ObjectCalendarAttendee>();
    public DbSet<ObjectAddress> Address => Set<ObjectAddress>();

    public DbSet<SyncJournal> SyncJournal => Set<SyncJournal>();
    public DbSet<TrxJournal> TrxJournal => Set<TrxJournal>();
    public DbSet<PushSubscription> PushSubscription => Set<PushSubscription>();

    public DbSet<SchedulingMessage> CalendarMessage => Set<SchedulingMessage>();


    public DbSet<DataMigrationDto> __DataMigrationHistory => Set<DataMigrationDto>();
}
