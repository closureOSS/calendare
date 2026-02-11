using Calendare.Server.Models.DavProperties;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Middleware;

public static class RegisterDavPropertiesExtensions
{

    public static IApplicationBuilder RegisterDavProperties(this IApplicationBuilder applicationBuilder)
    {
        var registry = applicationBuilder.ApplicationServices.GetRequiredService<DavPropertyRepository>();
        // perform registration

        registry.CommonProperties();
        registry.UserProperties();
        registry.RootProperties();
        registry.ContainerProperties();
        registry.PrincipalProperties();
        registry.ObjectProperties();
        registry.ObjectAddressbookProperties();
        registry.ObjectCalendarProperties();

        // end of registration
        return applicationBuilder;
    }
}
