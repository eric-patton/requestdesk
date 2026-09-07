using RequestDesk.Domain.Common;

namespace RequestDesk.Domain.Customers;

/// <summary>A customer account. One account can have several users who submit requests on its behalf.</summary>
public sealed class Customer : Entity
{
    public const int MaxNameLength = 150;
    public const int MaxOrganizationLength = 150;

    private Customer()
    {
    }

    public string Name { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string? Organization { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Customer Create(string name, string email, string? organization, DateTimeOffset now)
    {
        name = (name ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();
        organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim();

        if (name.Length == 0)
        {
            throw new DomainRuleException("A customer name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new DomainRuleException($"A customer name may be at most {MaxNameLength} characters.");
        }

        if (email.Length == 0)
        {
            throw new DomainRuleException("A customer email address is required.");
        }

        if (organization is { Length: > MaxOrganizationLength })
        {
            throw new DomainRuleException($"An organization name may be at most {MaxOrganizationLength} characters.");
        }

        return new Customer
        {
            Name = name,
            Email = email,
            Organization = organization,
            CreatedAt = now,
        };
    }
}
