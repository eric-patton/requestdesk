using FluentValidation;
using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Commands;

/// <summary>
/// Attach a file. The validator rejects oversize files and disallowed content types before any
/// bytes are written; the aggregate then decides whether this actor may attach to this request.
/// </summary>
public sealed record AddAttachmentCommand(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content) : IRequest<AttachmentDto>;

internal sealed class AddAttachmentCommandValidator : AbstractValidator<AddAttachmentCommand>
{
    public AddAttachmentCommandValidator()
    {
        RuleFor(c => c.FileName)
            .NotEmpty()
            .MaximumLength(RequestLimits.MaxFileNameLength);

        RuleFor(c => c.SizeBytes)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(AttachmentRules.MaxSizeBytes)
            .WithMessage($"Files may be at most {AttachmentRules.MaxSizeBytes / (1024 * 1024)} MB.");

        RuleFor(c => c.ContentType)
            .Must(AttachmentRules.IsAllowedContentType)
            .WithMessage($"That file type is not accepted. Allowed: {string.Join(", ", AttachmentRules.AllowedContentTypes.Order())}.");
    }
}

internal sealed class AddAttachmentCommandHandler(
    IServiceRequestRepository requests,
    IUserDirectory users,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
    : IRequestHandler<AddAttachmentCommand, AttachmentDto>
{
    public async Task<AttachmentDto> Handle(AddAttachmentCommand command, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var request = await requests.GetAsync(command.Id, cancellationToken);

        if (request is null || !RequestPolicy.CanView(request, actor))
        {
            throw new NotFoundException("Request", command.Id);
        }

        // Ask the domain before touching storage, so a forbidden upload never leaves a stray file behind.
        if (!RequestPolicy.CanAttach(request, actor))
        {
            throw new Domain.Common.PermissionDeniedException("Attachments can only be added to an open request.");
        }

        var key = await storage.SaveAsync(command.Content, cancellationToken);

        try
        {
            var attachment = request.AddAttachment(
                command.FileName,
                command.ContentType.Split(';')[0].Trim(),
                command.SizeBytes,
                key,
                actor,
                clock.GetUtcNow());

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var names = await users.GetSummariesAsync([actor.UserId], cancellationToken);
            return RequestMapper.ToAttachment(attachment, names);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }
}
