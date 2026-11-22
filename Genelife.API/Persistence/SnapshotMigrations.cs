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

        state.SchemaVersion = PersistedWorldState.CurrentSchemaVersion;
    }
}
