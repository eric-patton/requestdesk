using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Queries;

public sealed record DownloadAttachmentQuery(Guid RequestId, Guid AttachmentId) : IRequest<AttachmentContent>;

internal sealed class DownloadAttachmentQueryHandler(
    IServiceRequestRepository requests,
    IFileStorage storage,
    ICurrentUser currentUser)
    : IRequestHandler<DownloadAttachmentQuery, AttachmentContent>
{
    public async Task<AttachmentContent> Handle(DownloadAttachmentQuery query, CancellationToken cancellationToken)
    {
        var request = await requests.GetAsync(query.RequestId, cancellationToken);

        if (request is null || !RequestPolicy.CanView(request, currentUser.Actor))
        {
            throw new NotFoundException("Request", query.RequestId);
        }

        var attachment = request.Attachments.FirstOrDefault(a => a.Id == query.AttachmentId)
            ?? throw new NotFoundException("Attachment", query.AttachmentId);

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken)
            ?? throw new NotFoundException("Attachment", query.AttachmentId);

        return new AttachmentContent(content, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
    }
}
