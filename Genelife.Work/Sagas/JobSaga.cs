using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Work.Sagas;

public class JobSaga : ISaga, InitiatedBy<Hired>, Orchestrates<Fired> {
    public float PayPerHour { get; set; } = 5f;
    public Guid CorrelationId { get; set; }
    public Guid CompanyId { get; set; }

    public Task Consume(ConsumeContext<Hired> context)
    {
        PayPerHour = context.Message.PayPerHour;
        CompanyId = context.Message.CompanyId;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<Fired> context) 
        => await context.GetPayload<SagaConsumeContext<JobSaga, Fired>>().SetCompleted();
}