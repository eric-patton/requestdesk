using RequestDesk.Domain.Common;

namespace RequestDesk.Domain.Users;

/// <summary>
/// A person who can sign in. Credentials live in the identity store; this is the profile the rest
/// of the domain refers to. The two share an id.
/// </summary>
public sealed class AppUser : Entity
{
    public const int MaxDisplayNameLength = 100;

    private AppUser()
    {
    }

    public string DisplayName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public UserRole Role { get; private set; }

    /// <summary>Set for customers only: the customer account whose requests they may see.</summary>
    public Guid? CustomerId { get; private set; }

    public bool IsActive { get; private set; }

    public static AppUser Create(Guid id, string displayName, string email, UserRole role, Guid? customerId)
    {
        displayName = (displayName ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();

        if (displayName.Length == 0)
        {
            throw new DomainRuleException("A display name is required.");
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            throw new DomainRuleException($"A display name may be at most {MaxDisplayNameLength} characters.");
        }

        if (email.Length == 0)
        {
            throw new DomainRuleException("An email address is required.");
        }

        if (role == UserRole.Customer && customerId is null)
        {
            throw new DomainRuleException("A customer user must belong to a customer account.");
        }

        if (role != UserRole.Customer && customerId is not null)
        {
            throw new DomainRuleException("Staff users do not belong to a customer account.");
        }

        return new AppUser
        {
            Id = id,
            DisplayName = displayName,
            Email = email,
            Role = role,
            CustomerId = customerId,
            IsActive = true,
        };
    }

    public Actor AsActor() => new(Id, Role, CustomerId);

    public void Deactivate() => IsActive = false;
}
