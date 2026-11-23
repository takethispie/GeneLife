using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Models;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Marten;

namespace Genelife.Api.Services;

public sealed record WorldSaveBatch(
    IReadOnlyList<SimDocument> Sims,
    IReadOnlyList<CompanyDocument> Companies);

public class WorldSaveService
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
        var sims = new List<SimDocument>();

        var simQuery = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();
        world.Query(in simQuery, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId nid) =>
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

            SimDocument document = new()
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

            sims.Add(document);
        });

        var companies = new List<CompanyDocument>();

        var companyQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
        world.Query(in companyQuery, (Entity entity, ref CompanyName name, ref NetworkId nid) =>
        {
            companies.Add(new CompanyDocument
            {
                Id = DocumentIds.ForNetworkId(nid.Value),
                NetworkId = (long)nid.Value,
                Name = name.Name,
                EmployeeCount = employmentIndex.CountEmployees(entity),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        });

        return new WorldSaveBatch(sims, companies);
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

        foreach (var doc in batch.Sims)
            session.Store(doc);

        foreach (var doc in batch.Companies)
            session.Store(doc);

        await session.SaveChangesAsync();
        logger?.LogInformation("World saved to Marten: {SimCount} sims, {CompanyCount} companies",
            batch.Sims.Count, batch.Companies.Count);
    }
}
