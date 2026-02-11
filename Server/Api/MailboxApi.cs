using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Calendare.Server.Api.Models;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Calendare.Server.Api;


public static partial class AdministrationApi
{
    public static RouteGroupBuilder MapMailboxApi(this RouteGroupBuilder app)
    {
        app.MapGet("/uid/{uid}", async Task<Results<Ok<List<MailboxItem>>, NotFound>> (string uid, MailboxRepository mailboxRepository, HttpContext context) =>
        {
            var mailItems = await mailboxRepository.ListMailboxItems(new()
            {
                CurrentUser = new Principal(), // TODO: Set correct current user or change query
                IncludeProcessed = true,
                Uid = uid,
            }, context.RequestAborted);
            if (mailItems.Count == 0)
            {
                return TypedResults.NotFound();
            }
            return TypedResults.Ok(mailItems.ToView());
        })
        .WithName("GetMailboxByUid")
        .RequireAuthorization()
        .WithSummary("Get mailbox item by Uid (Debugging)")
        .WithDescription("Returns mailbox items.")
        .ProducesProblem(StatusCodes.Status404NotFound)
        ;

        app.MapGet("/", async Task<Results<Ok<List<MailboxItem>>, BadRequest>> (
            MailboxRepository mailboxRepository, HttpContext context,
            [FromQuery(Name = "sender"), Required] string senderEmail) =>
        {
            var mailItems = await mailboxRepository.ListMailboxItems(new()
            {
                CurrentUser = new Principal(), // TODO: Set correct current user or change query
                SenderEmail = senderEmail,
            }, context.RequestAborted);
            return TypedResults.Ok(mailItems.ToView());
        })
        .WithName("GetMailboxBySender")
        .RequireAuthorization()
        .WithSummary("Get unprocessed mailbox item by sender email")
        .WithDescription("Returns mailbox items.")
        ;


        return app;
    }
}
