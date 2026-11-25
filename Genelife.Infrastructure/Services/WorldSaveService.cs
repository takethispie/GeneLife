using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Models;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Marten;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

public sealed record WorldSaveBatch(
    IReadOnlyList<GenosDocument> Genoses,
    IReadOnlyList<CompanyDocument> Companies) : IWorldSaveBatch;

public class WorldSaveService : IWorldPersistence
{
    private readonly IDocumentStore? documentStore;
    private readonly ILogger<WorldSaveService>? logger;

    public WorldSaveService(IDocumentStore? documentStore = null, ILogger<WorldSaveService>? logger = null)
    {
        this.documentStore = documentStore;
        this.logger = logger;
    }

    public WorldSaveBatch Build(World world, EmploymentIndex employmentIndex)
    {
        var genoses = new List<GenosDocument>();

        var genosQuery = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();
        world.Query(in genosQuery, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId nid) =>
        {
            long householdNetworkId = 0;
            long unitNetworkId = 0;
            var tenure = string.Empty;
            var housingTier = string.Empty;
            if (world.TryGet<HouseholdMember>(entity, out var member))
            {
                householdNetworkId = NetworkIdOf(world, member.Household);
                if (world.TryGet<Household>(member.Household, out var household))
                    tenure = household.Tenure.ToString();
            }
            if (world.TryGet<Residence>(entity, out var residence))
            {
                unitNetworkId = NetworkIdOf(world, residence.Unit);
                if (world.TryGet<HousingUnit>(residence.Unit, out var unit))
                    housingTier = unit.Tier.ToString();
            }

            GenosDocument document = new()
            {
                Id = DocumentIds.ForNetworkId(nid.Value),
                NetworkId = (long)nid.Value,
                Name = name.Name,
                Age = world.TryGet<Alive>(entity, out var alive) ? alive.Age : 18,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                ActionTimeRemaining = action.TimeRemaining,
                IsPlayerControlled = world.Has<PlayerControlled>(entity),
                PlayerId = world.TryGet<PlayerControlled>(entity, out var pc) ? pc.PlayerId : null,
                HouseholdNetworkId = householdNetworkId,
                HousingUnitNetworkId = unitNetworkId,
                Tenure = tenure,
                HousingTier = housingTier,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (Genelife.Api.Appearance.AppearanceMapper.TryDerive(world, entity, out var appearance))
            {
                var dto = Genelife.Api.Appearance.AppearanceMapper.ToDto(appearance);
                document.SkinTone = dto.SkinTone;
                document.FaceShape = dto.FaceShape;
                document.HairColor = dto.HairColor;
                document.HairStyle = dto.HairStyle;
                document.EyeColor = dto.EyeColor;
                document.BodyFrame = dto.BodyFrame;
                document.HeightScale = dto.HeightScale;
                document.AgeBand = dto.AgeBand;
                document.Expression = dto.Expression;
                document.AppearanceVersion = dto.AppearanceVersion;
            }

            genoses.Add(document);
        });

        var companies = new List<CompanyDocument>();

        var officesByCompany = CompanyOfficeCounter.CountOpenByCompany(world);

        var companyQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
        world.Query(in companyQuery, (Entity entity, ref CompanyName name, ref NetworkId nid) =>
        {
            var document = new CompanyDocument
            {
                Id = DocumentIds.ForNetworkId(nid.Value),
                NetworkId = (long)nid.Value,
                Name = name.Name,
                EmployeeCount = employmentIndex.CountEmployees(entity),
                OfficeCount = officesByCompany.GetValueOrDefault(entity),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (world.TryGet<CompanyAccount>(entity, out var account))
            {
                var financials = CompanyFinancialSnapshot.From(account);
                document.Balance = financials.Balance;
                document.ReservedForPayroll = financials.ReservedForPayroll;
                document.Arrears = financials.Arrears;
                document.RevenueLastCycle = financials.RevenueLastCycle;
                document.WagesLastCycle = financials.WagesLastCycle;
                document.Solvency = financials.Solvency;
            }

            companies.Add(document);
        });

        return new WorldSaveBatch(genoses, companies);
    }

    private static long NetworkIdOf(World world, Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? (long)world.Get<NetworkId>(entity).Value
            : 0;

    public async Task SaveAsync(World world, EmploymentIndex employmentIndex)
    {
        if (documentStore == null)
        {
            logger?.LogInformation("No document store available; skipping world save");
            return;
        }

        await SaveBatchAsync(Build(world, employmentIndex));
    }

    public async Task SaveBatchAsync(WorldSaveBatch batch)
    {
        if (documentStore == null)
        {
            logger?.LogInformation("No document store available; skipping world save");
            return;
        }

        await using var session = documentStore.LightweightSession();

        foreach (var doc in batch.Genoses)
            session.Store(doc);

        foreach (var doc in batch.Companies)
            session.Store(doc);

        await session.SaveChangesAsync();
        logger?.LogInformation("World saved to Marten: {GenosCount} genoses, {CompanyCount} companies",
            batch.Genoses.Count, batch.Companies.Count);
    }

    IWorldSaveBatch? IWorldPersistence.Build(World world, EmploymentIndex employmentIndex) =>
        Build(world, employmentIndex);

    Task IWorldPersistence.SaveBatchAsync(IWorldSaveBatch batch) =>
        SaveBatchAsync((WorldSaveBatch)batch);
}
