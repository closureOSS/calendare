using System;
using Calendare.Data.Models;
using Calendare.Server.Api;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Handlers;
using Calendare.Server.Recorder;
using Calendare.Server.Reports;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.Server.Webpush;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Middleware;


public static class StartupExtensions
{
    public static IServiceCollection AddCaldav(this IServiceCollection services, Action<CaldavOptions>? configCaldav = null)
    {
        services
            .AddHttpContextAccessor()
            .AddSingleton<DavPropertyRepository>()
            .AddSingleton<StaticDataRepository>()
            .AddSingleton<InternalQueue<TrxJournal>>()
            .AddSingleton<InternalQueue<SyncMsg>>()
            .AddTransient<CaldavMiddleware>()
            .AddScoped<DavEnvironmentRepository>()
            .AddScoped<SiteRepository>()
            .AddScoped<SiteStatisticsRepository>()
            .AddScoped<PrincipalRepository>()
            .AddScoped<UserRepository>()
            .AddScoped<CredentialRepository>()
            .AddScoped<CollectionRepository>()
            .AddScoped<ResourceRepository>()
            .AddScoped<ItemRepository>()
            .AddScoped<MailboxRepository>()
            .AddScoped<PushSubscriptionRepository>()
            .AddScoped<RecorderSession>()
            .AddScoped<SchedulingRepository>()
            .AddScoped<MkCalendarHandler>()
            .AddScoped<OptionsHandler>()
            .AddScoped<PutHandler>()
            .AddScoped<PostHandler>()
            .AddScoped<MoveHandler>()
            .AddScoped<DeleteHandler>()
            .AddScoped<GetHandler>()
            .AddScoped<PropFindHandler>()
            .AddScoped<PropPatchHandler>()
            .AddScoped<ReportHandler>()
            .AddScoped<AclHandler>()
            .AddScoped<ScheduleGetHandler>()
            .AddScoped<SchedulePostHandler>()
            .AddScoped<SyncCollectionReport>()
            .AddScoped<PrincipalMatchReport>()
            .AddScoped<PrincipalPropertySearchReport>()
            .AddScoped<MultigetReport>()
            .AddScoped<CalendarQueryReport>()
            .AddScoped<FreeBusyQueryReport>()
            .AddScoped<AddressbookQueryReport>()
            .AddScoped<ExpandPropertyReport>()
            .AddScoped<AclPrincipalPropSetReport>()
            .AddScoped<UserManagementRepository>()
        ;
        services.AddHostedService<RecorderWorker>();
        services.AddHostedService<WebpushWorker>();
        services.AddHttpClient();

        var config = new CaldavOptions();
        services.Configure<CaldavOptions>(o =>
        {
            //
            // Handlers/Methods
            //
            o.Handlers["OPTIONS"] = typeof(OptionsHandler);
            o.Handlers["PROPFIND"] = typeof(PropFindHandler);
            o.Handlers["REPORT"] = typeof(ReportHandler);
            o.Handlers["DELETE"] = typeof(DeleteHandler);
            o.Handlers["MOVE"] = typeof(MoveHandler);
            o.Handlers["GET"] = typeof(GetHandler);
            o.Handlers["PUT"] = typeof(PutHandler);
            o.Handlers["HEAD"] = typeof(GetHandler);
            o.Handlers["MKCOL"] = typeof(MkCalendarHandler);
            o.Handlers["MKCALENDAR"] = typeof(MkCalendarHandler);
            o.Handlers["POST"] = typeof(PostHandler);
            o.Handlers["PROPPATCH"] = typeof(PropPatchHandler);
            o.Handlers["ACL"] = typeof(AclHandler);
            o.Handlers["SCHEDULE#GET"] = typeof(ScheduleGetHandler);
            o.Handlers["SCHEDULE#POST"] = typeof(SchedulePostHandler);
            //
            // Not supported, but known methods:
            //
            // LOCK, UNLOCK - https://datatracker.ietf.org/doc/html/rfc4918#section-6
            o.UnsupportedMethods.Add("LOCK");
            o.UnsupportedMethods.Add("UNLOCK");
            // BIND - https://datatracker.ietf.org/doc/html/rfc5842
            o.UnsupportedMethods.Add("BIND");
            // COPY - https://datatracker.ietf.org/doc/html/rfc4918#section-9.8
            o.UnsupportedMethods.Add("COPY");

            //
            // Reports
            //
            o.Reports[XmlNs.Dav + "sync-collection"] = typeof(SyncCollectionReport);
            o.Reports[XmlNs.Dav + "principal-match"] = typeof(PrincipalMatchReport);
            o.Reports[XmlNs.Dav + "principal-property-search"] = typeof(PrincipalPropertySearchReport);
            o.Reports[XmlNs.Dav + "acl-principal-prop-set"] = typeof(AclPrincipalPropSetReport);
            o.Reports[XmlNs.Caldav + "calendar-multiget"] = typeof(MultigetReport);
            o.Reports[XmlNs.Caldav + "free-busy-query"] = typeof(FreeBusyQueryReport);
            o.Reports[XmlNs.Caldav + "calendar-query"] = typeof(CalendarQueryReport);
            o.Reports[XmlNs.Carddav + "addressbook-multiget"] = typeof(MultigetReport);
            o.Reports[XmlNs.Carddav + "addressbook-query"] = typeof(AddressbookQueryReport);
            o.Reports[XmlNs.Dav + "expand-property"] = typeof(ExpandPropertyReport);

            configCaldav?.Invoke(config);
        });

        return services;
    }

    public static IApplicationBuilder UseCaldav(this IApplicationBuilder app)
    {
        return app
            .RegisterDavProperties()
            .UseMiddleware<CaldavMiddleware>();
    }
}
