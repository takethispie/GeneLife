namespace Genelife.Api.Persistence;

/// <summary>
/// Upgrades older snapshots in memory. v1 stored wallet balances as float units;
/// v2 stores integer cents and adds accounts, offices, postings and employment.
/// v3 adds housing, v4 adds lifecycle state and v5 adds the spatial map and
/// building placements.
/// </summary>
public static class SnapshotMigrations
{
    public static void Upgrade(PersistedWorldState state, int fromVersion)
    {
        state.MigratedFromVersion = fromVersion;

        if (fromVersion <= 1)
        {
            foreach (var sim in state.Sims)
            {
                if (sim.WalletBalanceCents == 0 && sim.WalletBalance != 0)
                    sim.WalletBalanceCents = (long)Math.Round((decimal)sim.WalletBalance * 100m);
                sim.WalletBalance = 0;
            }

            foreach (var company in state.Companies)
            {
                if (!company.HasAccount)
                {
                    company.HasAccount = true;
                    company.BalanceCents = 10_000_00;
                    company.Solvency = "Healthy";
                }
            }

            if (state.Fiscal.BankBalanceCents == 0)
                state.Fiscal.BankBalanceCents = 100_000_00;
        }

        if (fromVersion <= 2)
        {
            state.HousingMigrated = false;
            state.Properties.Clear();
            state.HousingUnits.Clear();
            state.Households.Clear();
            state.HouseholdMembers.Clear();
            state.ConstructionProjects.Clear();
            state.HousingMetricHistory.Clear();
            state.HousingMarket = new PersistedHousingMarket();
        }

        if (fromVersion <= 3)
        {
            state.LifecycleMigrated = false;
            state.Schools.Clear();
            state.LifecycleMetricHistory.Clear();
            state.MortalityLog.Clear();
            state.Immigration = new PersistedImmigration();
        }

        if (fromVersion <= 4)
        {
            state.Map = new PersistedMap();
            state.Placements.Clear();
        }

        if (fromVersion <= 5)
        {
            // v6 adds heritable traits. They cannot be backfilled here because the
            // deterministic backfill needs the world seed and each sim's network id;
            // SimulationEngine.Restore generates them and sets TraitsMigrated.
            state.GeneticsOptions ??= Genelife.Family.GeneticsOptions.Default;
            if (string.IsNullOrWhiteSpace(state.AttractionModelVersion))
                state.AttractionModelVersion = Genelife.Family.AttractionCoefficients.CurrentVersion;
        }

        if (fromVersion <= 8)
        {
            // v9 adds inheritance/guardianship policy and minor trusts. Older
            // saves have no trusts, which is valid.
            state.InheritanceOptions ??= Genelife.Family.InheritanceOptions.Default;
            state.GuardianshipOptions ??= Genelife.Family.GuardianshipOptions.Default;
            state.Trusts ??= [];
        }

        if (fromVersion <= 7)
        {
            // v8 adds reproduction and family housing policy options. Older saves
            // have no pregnancies, which is valid.
            state.ReproductionOptions ??= Genelife.Family.ReproductionOptions.Default;
            state.FamilyHousingPolicy ??= Genelife.Family.FamilyHousingPolicy.Default;
            state.Pregnancies ??= [];
        }

        if (fromVersion <= 6)
        {
            // v7 adds relationship edges. Older saves have no family edges, which
            // is valid; new partner/parent edges are created by simulation systems.
            state.RelationshipOptions ??= Genelife.Family.RelationshipOptions.Default;
            state.Relationships ??= [];
        }

        state.SchemaVersion = PersistedWorldState.CurrentSchemaVersion;
    }
}
