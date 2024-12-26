using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Commands.Money;
using Genelife.Domain.Events;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Work;
using MassTransit;
using Serilog;

namespace Genelife.Work.Sagas;

public class CompanyJobSaga : ISaga, ISagaVersion, InitiatedBy<Hired>, Orchestrates<Fired>, Observes<HourElapsed, CompanyJobSaga>{
    public float PayPerHour { get; set; } = 5f;
    public Guid CorrelationId { get; set; }
    public Guid CompanyId { get; set; }
    public int Version { get; set; }

    public Expression<Func<CompanyJobSaga, HourElapsed, bool>> CorrelationExpression => (saga, message) => true;


    public Task Consume(ConsumeContext<Hired> context)
    {
        Log.Information($"Created job saga for {CorrelationId}");
        PayPerHour = context.Message.PayPerHour;
        CompanyId = context.Message.CompanyId;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<Fired> context) 
        => await context.GetPayload<SagaConsumeContext<CompanyJobSaga, Fired>>().SetCompleted();

    public async Task Consume(ConsumeContext<HourElapsed> context)
    {
        Log.Information($"{CorrelationId} was payed {PayPerHour}");
        await context.Publish(new TransferHourlyPay(CorrelationId, PayPerHour));
    }
}