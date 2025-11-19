using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Main.Domain;
using Genelife.Main.Sagas;
using Genelife.Main.Sagas.States;
using Genelife.Main.Usecases;
using Genelife.Main.Usecases.Working;
using Genelife.Tests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using CreateCompany = Genelife.Domain.Commands.Company.CreateCompany;

namespace Genelife.Tests.Sagas;

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
        await harness.Bus.Publish(new CreateCompany(id, company));
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
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100); 

        await harness.Bus.Publish(new DayElapsed());

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
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, salary));

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
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, 75000f));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeProductivityUpdated(id, humanId, 0.9f));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed());

        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();

        (await sagaHarness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}