using System.Linq.Expressions;
using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Work.Sagas;

public class CompanySaga : ISaga, InitiatedBy<CreateCompany>, Observes<Hired, CompanySaga> {
    public Guid CorrelationId { get; set; }
    public Guid[] Employees { get; set; } = [];
    public string Name { get; set; }
    public Vector3 HQPosition { get; set; }

    public Expression<Func<CompanySaga, Hired, bool>> CorrelationExpression => (saga, message) => message.CompanyId == CorrelationId;

    public Task Consume(ConsumeContext<Hired> context)
    {
        var id = context.Message.CorrelationId;
        Employees = Employees.Where(x => x != id).Append(id).ToArray();
        Console.WriteLine($"{id} was hired at {CorrelationId}");
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<Fired> context) 
    {
        var id = context.Message.CorrelationId;
        Employees = Employees.Where(x => x != id).ToArray();
        Console.WriteLine($"{id} was Fired from {CorrelationId}");
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<CreateCompany> context)
    {
        Console.WriteLine($"Company {context.Message.CorrelationId} created");
        Name = context.Message.Name;
        HQPosition = context.Message.HQPosition;
        return Task.CompletedTask;
    }
}