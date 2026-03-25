using Genelife.Domain;
using Genelife.Domain.House;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Human;
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
    public House House { get; set; }
    public int Version { get; set; }

    public async Task Consume(ConsumeContext<HouseBuilt> context)
    {
        var pos = new Position(context.Message.X, context.Message.Y, context.Message.Z);
        House = new House(context.Message.CorrelationId, pos, context.Message.Owners ?? new());
        if (context.Message.Owners is not null)
        {
            foreach (var owner in context.Message.Owners)
            {
                await context.Publish(new SetHomeAddress( owner, CorrelationId, pos));
            }
        }
        Log.Information("Created house {CorrelationId} at {Position}", CorrelationId, House.Position);
    }

    public async Task Consume(ConsumeContext<GoHome> context)
    {
        House.OccupantsEnters(context.Message.HumanId);
        Log.Information("Human {HumanId} is home", context.Message.HumanId);
        await context.Publish(new Arrived(context.Message.HumanId, House.Position.X, House.Position.Y, House.Position.Z, "Home"));
    }

    public async Task Consume(ConsumeContext<LeaveHome> context)
    {
        House.OccupantsLeaves(context.Message.HumanId);
        Log.Information("Human {HumanId} is leaving home", context.Message.HumanId);
        await context.Publish(new LeftHome(CorrelationId, context.Message.HumanId));
    }
}
