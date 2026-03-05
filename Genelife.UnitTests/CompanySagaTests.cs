using System.Numerics;
using FluentAssertions;
using Genelife.Application.Sagas;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Company;
using Genelife.UnitTests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using CreateCompany = Genelife.Messages.Commands.Company.CreateCompany;

namespace Genelife.UnitTests;

public class CompanySagaTests
{
    [Fact]
    public async Task CompanyCreated_ShouldInitializeSagaState()
    {
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSaga<CompanySaga>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaHarness<CompanySaga>();
        var id = Guid.NewGuid();
        var officeLocation = new Vector3(
            Random.Shared.NextSingle() * 800 - 400,
            Random.Shared.NextSingle() * 800 - 400,
            0
        );
        await harness.Bus.Publish(new CreateCompany(id, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Created.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task DayElapsed_ShouldBeConsumed()
    {
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSaga<CompanySaga>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaHarness<CompanySaga>();
        var id = Guid.NewGuid();
        var officeLocation = new Vector3(
            Random.Shared.NextSingle() * 800 - 400,
            Random.Shared.NextSingle() * 800 - 400,
            0
        );
        await harness.Bus.Publish(new CreateCompany(id, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
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
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSaga<CompanySaga>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaHarness<CompanySaga>();
        var id = Guid.NewGuid();
        var officeLocation = new Vector3(
            Random.Shared.NextSingle() * 800 - 400,
            Random.Shared.NextSingle() * 800 - 400,
            0
        );
        await harness.Bus.Publish(new CreateCompany(id, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, salary, new OfficeLocation(0, 0, 0)));

        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleEvents_ShouldBeHandledInSequence()
    {
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSaga<CompanySaga>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaHarness<CompanySaga>();
        var id = Guid.NewGuid();
        var officeLocation = new Vector3(
            Random.Shared.NextSingle() * 800 - 400,
            Random.Shared.NextSingle() * 800 - 400,
            0
        );
        await harness.Bus.Publish(new CreateCompany(id, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, 75000f, new OfficeLocation(0, 0, 0)));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeProductivityUpdated(id, humanId, 0.9f));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed(new DateOnly(1, 1, 1)));

        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}