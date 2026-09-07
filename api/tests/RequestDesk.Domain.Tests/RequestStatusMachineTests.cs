using RequestDesk.Domain.Requests;
using static RequestDesk.Domain.Requests.RequestStatus;

namespace RequestDesk.Domain.Tests;

/// <summary>
/// The full transition matrix. The expected table is written out here in full, on purpose: if the
/// machine changes, this file has to change with it, and the diff shows exactly which move changed.
/// </summary>
public class RequestStatusMachineTests
{
    private static readonly Dictionary<RequestStatus, RequestStatus[]> Expected = new()
    {
        [New] = [Triaged, Cancelled],
        [Triaged] = [InProgress, Cancelled],
        [InProgress] = [Blocked, Resolved],
        [Blocked] = [InProgress, Cancelled],
        [Resolved] = [InProgress, Closed],
        [Closed] = [],
        [Cancelled] = [],
    };

    public static TheoryData<RequestStatus, RequestStatus, bool> EveryPair()
    {
        var data = new TheoryData<RequestStatus, RequestStatus, bool>();

        foreach (var from in Enum.GetValues<RequestStatus>())
        {
            foreach (var to in Enum.GetValues<RequestStatus>())
            {
                data.Add(from, to, Expected[from].Contains(to));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Every_pair_of_statuses_matches_the_matrix(RequestStatus from, RequestStatus to, bool legal)
    {
        RequestStatusMachine.CanTransition(from, to).ShouldBe(legal);
    }

    [Fact]
    public void The_matrix_covers_every_status()
    {
        Expected.Keys.OrderBy(s => s).ShouldBe(Enum.GetValues<RequestStatus>().OrderBy(s => s));
        RequestStatusMachine.All.ShouldBe(Enum.GetValues<RequestStatus>());
    }

    [Theory]
    [InlineData(New)]
    [InlineData(Triaged)]
    [InlineData(InProgress)]
    [InlineData(Blocked)]
    [InlineData(Resolved)]
    [InlineData(Closed)]
    [InlineData(Cancelled)]
    public void Legal_targets_match_the_matrix_and_come_back_in_enum_order(RequestStatus from)
    {
        var targets = RequestStatusMachine.LegalTargets(from);

        targets.ShouldBe(Expected[from]);

        // Enum order, so the API returns targets in a stable order.
        targets.ShouldBe(targets.OrderBy(t => t).ToArray());
    }

    [Theory]
    [InlineData(Closed)]
    [InlineData(Cancelled)]
    public void Terminal_states_have_no_targets(RequestStatus status)
    {
        RequestStatusMachine.IsTerminal(status).ShouldBeTrue();
        RequestStatusMachine.IsOpen(status).ShouldBeFalse();
        RequestStatusMachine.LegalTargets(status).ShouldBeEmpty();
    }

    [Fact]
    public void Only_closed_and_cancelled_are_terminal()
    {
        var terminal = Enum.GetValues<RequestStatus>().Where(RequestStatusMachine.IsTerminal).ToArray();

        terminal.ShouldBe([Closed, Cancelled]);
    }

    [Fact]
    public void No_status_transitions_to_itself()
    {
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            RequestStatusMachine.CanTransition(status, status).ShouldBeFalse($"{status} -> {status}");
        }
    }

    [Fact]
    public void Every_status_is_reachable_from_new()
    {
        var seen = new HashSet<RequestStatus> { New };
        var frontier = new Queue<RequestStatus>([New]);

        while (frontier.TryDequeue(out var current))
        {
            foreach (var next in RequestStatusMachine.LegalTargets(current))
            {
                if (seen.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        seen.OrderBy(s => s).ShouldBe(Enum.GetValues<RequestStatus>().OrderBy(s => s));
    }

    [Fact]
    public void Every_open_status_can_still_reach_a_terminal_state()
    {
        foreach (var start in Enum.GetValues<RequestStatus>().Where(RequestStatusMachine.IsOpen))
        {
            var seen = new HashSet<RequestStatus> { start };
            var frontier = new Queue<RequestStatus>([start]);

            while (frontier.TryDequeue(out var current))
            {
                foreach (var next in RequestStatusMachine.LegalTargets(current))
                {
                    if (seen.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }

            seen.Any(RequestStatusMachine.IsTerminal).ShouldBeTrue($"{start} can never finish");
        }
    }

    [Fact]
    public void Reopen_is_exactly_resolved_to_in_progress()
    {
        foreach (var from in Enum.GetValues<RequestStatus>())
        {
            foreach (var to in Enum.GetValues<RequestStatus>())
            {
                RequestStatusMachine.IsReopen(from, to).ShouldBe(from == Resolved && to == InProgress, $"{from} -> {to}");
            }
        }

        RequestStatusMachine.CanTransition(Resolved, InProgress).ShouldBeTrue("reopen has to be a legal move");
    }
}
