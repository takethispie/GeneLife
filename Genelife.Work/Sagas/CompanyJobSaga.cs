using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;
using Serilog;

namespace Genelife.Work.Sagas;

public class CompanyJobSaga : ISaga, ISagaVersion, 
    InitiatedBy<Hired>, 
    Orchestrates<Fired>, 
    Observes<HourElapsed, CompanyJobSaga>, 
    Orchestrates<StartedWorking>, 
    Orchestrates<FinishedWorking>
{
    public float PayPerHour { get; set; } = 5f;
    public Guid CorrelationId { get; set; }
    public Guid CompanyId { get; set; }
    public int Version { get; set; }
    public bool IsWorking { get; set; } = false;

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
        if(!IsWorking) return; 
        Log.Information($"{CorrelationId} was payed {PayPerHour}");
        await context.Publish(new TransferHourlyPay(CorrelationId, PayPerHour));
    }

    public Task Consume(ConsumeContext<StartedWorking> context)
    {
        Log.Information($"{CorrelationId} has started working");
        IsWorking = true;
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<FinishedWorking> context)
    {
        Log.Information($"{CorrelationId} has finished working");
        IsWorking = false;
        return Task.CompletedTask;
    }
}