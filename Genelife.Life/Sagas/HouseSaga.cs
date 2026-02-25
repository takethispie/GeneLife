using Genelife.Global.Messages.DTOs;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Messages.Commands.Locomotion;
using Genelife.Life.Messages.DTOs;
using Genelife.Life.Messages.Events;
using Genelife.Life.Messages.Events.Buildings;
using Genelife.Life.Messages.Events.Locomotion;
using MassTransit;
using Serilog;

namespace Genelife.Life.Sagas;

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
                    new Coordinates(context.Message.X, context.Message.Y, context.Message.Z)
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
