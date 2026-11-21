using Genelife.Messages.Events;

namespace Genelife.Api.Sagas;

public class HiringSagaHandler
{
    public HiringState Handle(JobPostingCreated @event, HiringState? state)
    {
        state ??= new HiringState
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        state.CompanyId = @event.CompanyId;
        state.PostingId = @event.PostingId;
        state.Position = @event.Position;
        state.Salary = @event.Salary;
        state.OpenSeats = @event.OpenSeats;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        return state;
    }

    public HiringState Handle(ApplicationSubmitted @event, HiringState state)
    {
        if (state.Status != HiringStatus.Active)
            return state;

        if (state.ApplicantIds.Contains(@event.SimId))
            return state;

        state.ApplicantIds.Add(@event.SimId);

        if (state.OpenSeats > state.FilledSeatSimIds.Count)
        {
            state.FilledSeatSimIds.Add(@event.SimId);
        }

        state.UpdatedAt = DateTimeOffset.UtcNow;
        return state;
    }

    public HiringState Handle(SeatFilled @event, HiringState state)
    {
        if (state.Status != HiringStatus.Active)
            return state;

        if (!state.FilledSeatSimIds.Contains(@event.SimId))
            state.FilledSeatSimIds.Add(@event.SimId);

        state.UpdatedAt = DateTimeOffset.UtcNow;

        if (state.FilledSeatSimIds.Count >= state.OpenSeats)
        {
            state.Status = HiringStatus.AllSeatsFilled;
        }

        return state;
    }

    public HiringState Handle(AllSeatsFilled @event, HiringState state)
    {
        state.Status = HiringStatus.AllSeatsFilled;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        return state;
    }

    public HiringState Handle(JobPostingTimedOut @event, HiringState state)
    {
        state.Status = HiringStatus.TimedOut;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        return state;
    }
}