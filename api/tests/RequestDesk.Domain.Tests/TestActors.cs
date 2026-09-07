using RequestDesk.Domain.Users;

namespace RequestDesk.Domain.Tests;

/// <summary>Fixed actors and a fixed clock so every test reads the same way.</summary>
internal static class TestActors
{
    public static readonly Guid CustomerAccountA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid CustomerAccountB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public static readonly Actor Admin = new(Guid.Parse("00000000-0000-0000-0000-00000000ad01"), UserRole.Admin, null);
    public static readonly Actor Agent = new(Guid.Parse("00000000-0000-0000-0000-00000000a601"), UserRole.Agent, null);
    public static readonly Actor OtherAgent = new(Guid.Parse("00000000-0000-0000-0000-00000000a602"), UserRole.Agent, null);
    public static readonly Actor CustomerA = new(Guid.Parse("00000000-0000-0000-0000-00000000c0a1"), UserRole.Customer, CustomerAccountA);
    public static readonly Actor CustomerB = new(Guid.Parse("00000000-0000-0000-0000-00000000c0b1"), UserRole.Customer, CustomerAccountB);

    public static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    public static Actor For(UserRole role) => role switch
    {
        UserRole.Admin => Admin,
        UserRole.Agent => Agent,
        UserRole.Customer => CustomerA,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
