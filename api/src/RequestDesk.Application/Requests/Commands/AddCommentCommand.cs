using FluentValidation;
using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Commands;

public sealed record AddCommentCommand(Guid Id, string Body) : IRequest<CommentDto>;

internal sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(c => c.Body)
            .NotEmpty().WithMessage("A comment needs some text.")
            .MaximumLength(RequestLimits.MaxCommentLength);
    }
}

internal sealed class AddCommentCommandHandler(
    IServiceRequestRepository requests,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
    : IRequestHandler<AddCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(AddCommentCommand command, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var request = await requests.GetAsync(command.Id, cancellationToken);

        if (request is null || !RequestPolicy.CanView(request, actor))
        {
            throw new NotFoundException("Request", command.Id);
        }

        var comment = request.AddComment(command.Body, actor, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await users.GetSummariesAsync([actor.UserId], cancellationToken);
        return RequestMapper.ToComment(comment, names);
    }
}
