using Genelife.Domain;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Locomotion;
using Genelife.Messages.Events;
using Genelife.Messages.Events.Buildings;
using Genelife.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Application.Sagas;

public class HouseSaga :
    ISaga,
    ISagaVersion,
    InitiatedBy<HouseBuilt>,
    Orchestrates<GoHome>,
    Orchestrates<LeaveHome>
{
    public Guid CorrelationId { get; set; }
    public Position Position { get; set; } = new(0, 0, 0);
    public List<Guid> Owners { get; set; } = [];
    public List<Guid> Occupants { get; set; } = [];
    public int Version { get; set; }

    public async Task Consume(ConsumeContext<HouseBuilt> context)
    {
        Position = new(context.Message.X, context.Message.Y, context.Message.Z);
        if (context.Message.Owners is not null)
        {
            Owners = context.Message.Owners;
            foreach (var owner in context.Message.Owners)
            {
                await context.Publish(new SetHomeAddress(
                    owner,
                    CorrelationId,
                    new Position(context.Message.X, context.Message.Y, context.Message.Z)
                ));
            }
        }
        Log.Information("Created house {CorrelationId} at {Position}", CorrelationId, Position);
    }

    public async Task Consume(ConsumeContext<GoHome> context)
    {
        if (!Occupants.Contains(context.Message.HumanId))
            Occupants.Add(context.Message.HumanId);
        Log.Information("Human {HumanId} is home", context.Message.HumanId);
        await context.Publish(new Arrived(context.Message.HumanId, Position.X, Position.Y, Position.Z, "Home"));
    }

    public async Task Consume(ConsumeContext<LeaveHome> context)
    {
        Occupants.Remove(context.Message.HumanId);
        Log.Information("Human {HumanId} is leaving home", context.Message.HumanId);
        await context.Publish(new LeftHome(CorrelationId, context.Message.HumanId));
    }
}
