using FluentValidation;
using MediatR;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Common;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Commands;

/// <summary>
/// Open a new request. A customer's request lands on their own account and <see cref="CustomerId"/>
/// is ignored; staff must say which customer they are opening it for.
/// </summary>
public sealed record CreateRequestCommand(
    string Title,
    string Description,
    RequestPriority Priority,
    Guid? CustomerId) : IRequest<RequestDetail>;

internal sealed class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator(ICurrentUser currentUser)
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithMessage("A title is required.")
            .MaximumLength(RequestLimits.MaxTitleLength);

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("A description is required.")
            .MaximumLength(RequestLimits.MaxDescriptionLength);

        RuleFor(c => c.Priority).IsInEnum();

        When(_ => currentUser.IsAuthenticated && currentUser.Actor.IsStaff, () =>
        {
            RuleFor(c => c.CustomerId)
                .NotNull()
                .WithMessage("Choose the customer this request is for.");
        });
    }
}

internal sealed class CreateRequestCommandHandler(
    IServiceRequestRepository requests,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock,
    ISender sender)
    : IRequestHandler<CreateRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;

        var customerId = actor.IsCustomer
            ? actor.CustomerId ?? throw new DomainRuleException("This customer user is not linked to a customer account.")
            : command.CustomerId ?? throw new DomainRuleException("Choose the customer this request is for.");

        if (!await customers.ExistsAsync(customerId, cancellationToken))
        {
            throw new DomainRuleException("That customer does not exist.");
        }

        var request = ServiceRequest.Create(
            customerId,
            command.Title,
            command.Description,
            command.Priority,
            actor,
            clock.GetUtcNow());

        requests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read through the query so the response carries the database-generated reference number and resolved names.
        return await sender.Send(new GetRequestQuery(request.Id), cancellationToken);
    }
}
