/**
 * Field limits, mirrored from `RequestDesk.Domain/Requests/RequestLimits.cs` and
 * `RequestDesk.Application/Requests/AttachmentRules.cs`. The server is the authority; these exist so
 * the form can say no before a round trip, with the same numbers.
 */
export const LIMITS = {
  title: 200,
  description: 4000,
  comment: 4000,
  reason: 500,
  fileName: 255,
} as const;

export const ATTACHMENTS = {
  maxSizeBytes: 10 * 1024 * 1024,
  allowedContentTypes: [
    'image/png',
    'image/jpeg',
    'image/gif',
    'image/webp',
    'application/pdf',
    'text/plain',
    'text/csv',
  ],
} as const;

export function isAllowedContentType(contentType: string): boolean {
  const bare = contentType.split(';')[0].trim().toLowerCase();
  return (ATTACHMENTS.allowedContentTypes as readonly string[]).includes(bare);
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
