namespace RequestDesk.Application.Common;

/// <summary>
/// The thing does not exist, or the caller is not allowed to know whether it exists. A customer
/// asking for another customer's request gets this, not a 403, so the id space leaks nothing.
/// Maps to 404.
/// </summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;

    public object Key { get; } = key;
}

/// <summary>Bad credentials or an unusable refresh token. Maps to 401 with a deliberately vague message.</summary>
public sealed class AuthenticationFailedException()
    : Exception("The email address or password is incorrect.");

/// <summary>Someone else changed the same request between the read and the write. Maps to 409; the client reloads and retries.</summary>
public sealed class ConcurrencyConflictException()
    : Exception("The request was changed by someone else. Reload and try again.");
