using AlarmHub.Api.Domain.Customers;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Api.Endpoints;

/// <summary>
/// Customer endpoints: create and read the owner of protected sites.
/// </summary>
internal static class CustomersEndpoints
{
    /// <summary>Registers the <c>/api/customers</c> route group.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapPost("/", CreateCustomerAsync)
            .WithName("CreateCustomer")
            .WithSummary("Registers a new customer")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("alarms.write");

        group.MapGet("/{id:guid}", GetCustomerAsync)
            .WithName("GetCustomer")
            .WithSummary("Returns a customer with their sites")
            .Produces<CustomerDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("alarms.read");

        return app;
    }

    /// <summary>Creates a customer.</summary>
    private static async Task<Created<CustomerResponse>> CreateCustomerAsync(
        CreateCustomerRequest request,
        AlarmHubDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Customer customer = Customer.Create(request.Name, request.Email, timeProvider.GetUtcNow());

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        CustomerResponse response = new(customer.Id, customer.Name, customer.Email, customer.IsActive);

        return TypedResults.Created($"/api/customers/{customer.Id}", response);
    }

    /// <summary>Returns a single customer together with their sites.</summary>
    private static async Task<Results<Ok<CustomerDetailsResponse>, NotFound>> GetCustomerAsync(
        Guid id,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Customer? customer = await dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Sites)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        CustomerDetailsResponse response = new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.IsActive,
            customer.Sites
                .Select(site => new SiteSummaryResponse(site.Id, site.Name, site.Address.ToString(), site.TimeZoneId))
                .ToList());

        return TypedResults.Ok(response);
    }

    /// <summary>Payload accepted by <c>POST /api/customers</c>.</summary>
    internal sealed record CreateCustomerRequest(string Name, string Email);

    /// <summary>Compact customer representation.</summary>
    internal sealed record CustomerResponse(Guid Id, string Name, string Email, bool IsActive);

    /// <summary>Customer representation with the list of owned sites.</summary>
    internal sealed record CustomerDetailsResponse(
        Guid Id,
        string Name,
        string Email,
        bool IsActive,
        IReadOnlyCollection<SiteSummaryResponse> Sites);

    /// <summary>Site summary embedded in customer responses.</summary>
    internal sealed record SiteSummaryResponse(Guid Id, string Name, string Address, string TimeZoneId);
}
