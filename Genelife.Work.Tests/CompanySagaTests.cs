using FluentAssertions;
using Genelife.Global.Messages.Events.Clock;
using Genelife.Work.Messages.Events.Company;
using Genelife.Work.Sagas;
using Genelife.Work.Sagas.States;
using Genelife.Work.Usecases;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CreateCompany = Genelife.Work.Messages.Commands.Company.CreateCompany;

namespace Genelife.Work.Tests;

public class CompanySagaTests
{
    [Fact]
    public async Task CompanyCreated_ShouldInitializeSagaState()
    {
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateCompanyProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        await harness.Bus.Publish(new CreateCompany(id, company, Guid.NewGuid()));
        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Created.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task DayElapsed_ShouldBeConsumed()
    {
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateCompanyProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        await harness.Bus.Publish(new CreateCompany(id, company, Guid.NewGuid()));
        await Task.Delay(100); 

        await harness.Bus.Publish(new DayElapsed(new DateOnly(1, 1, 1)));

        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task HireEmployee_ShouldBeConsumed()
    {
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();
        var salary = 75000f;

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateCompanyProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        await harness.Bus.Publish(new CreateCompany(id, company, Guid.NewGuid()));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, salary, Guid.NewGuid()));

        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleEvents_ShouldBeHandledInSequence()
    {
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateCompanyProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        await harness.Bus.Publish(new CreateCompany(id, company, Guid.NewGuid()));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, 75000f, Guid.NewGuid()));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeProductivityUpdated(id, humanId, 0.9f));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed(new  DateOnly(1, 1, 1)));

        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}