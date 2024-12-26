using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Domain.Commands.Create;
using MassTransit;
using Serilog;

namespace Genelife.Physical.Sagas;

public class HouseSaga : ISaga, ISagaVersion, InitiatedBy<CreateHouse>, Orchestrates<AttachToHouse>
{
    public Guid CorrelationId { get; set; }
    public Vector2 Size { get; set; }
    public Vector3 Position { get; set; }
    public int Version { get; set; }
    public List<Guid> Occupants { get; set; } = [];


    public Task Consume(ConsumeContext<CreateHouse> context)
    {
        Log.Information($"created House {context.Message.CorrelationId} at position {context.Message.X}:{context.Message.Y}");
        Size = new(context.Message.Width, context.Message.Depth);
        Position = new(context.Message.X, context.Message.Y, 0);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<AttachToHouse> context)
    {
        if(Occupants.Any(x => x == context.Message.Being) is false) {
            Log.Information($"attach {context.Message.Being} to house {context.Message.CorrelationId}");
            Occupants.Add(context.Message.CorrelationId);
        } else Log.Information($"{context.Message.Being} is already an occupant of house {context.Message.CorrelationId}");
        return Task.CompletedTask;
    }
}