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

        if (fromVersion <= 9)
        {
            // v10 persists explicit state-care records. Older saves used the
            // GuardianshipAssigned event only; no state-care rows is valid.
            state.StateCare ??= [];
        }

        if (fromVersion <= 10)
        {
            foreach (var sim in state.Sims)
                sim.HasWallet |= sim.WalletBalanceCents != 0 || sim.WalletBalance != 0 ||
                                 sim.EmployerNetworkId != 0;
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

        if (fromVersion <= 11)
        {
            // v12 adds player profiles, player life bindings and family
            // invitations. Reconstruct the safe subset from existing components.
            state.PlayerProfiles ??= [];
            state.PlayerLives ??= [];
            state.FamilyInvitations ??= [];

            if (state.PlayerProfiles.Count == 0)
            {
                var memberBySim = state.HouseholdMembers
                    .GroupBy(member => member.SimNetworkId)
                    .ToDictionary(group => group.Key, group => group.First());
                var householdById = state.Households
                    .GroupBy(household => household.NetworkId)
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var sim in state.Sims.Where(sim => !string.IsNullOrWhiteSpace(sim.PlayerId)))
                {
                    state.PlayerProfiles.Add(new PersistedPlayerProfile
                    {
                        PlayerId = sim.PlayerId!,
                        SimNetworkId = sim.NetworkId,
                        ContinuationPolicy = "Heir",
                        AwaitingContinuation = false,
                        Observer = false,
                        CreatedTick = 0,
                        LastActionTick = 0,
                        LastSequence = 0
                    });

                    var life = new PersistedPlayerLife
                    {
                        SimNetworkId = sim.NetworkId,
                        HasEmployment = sim.EmployerNetworkId != 0,
                        EmploymentOfficeNetworkId = sim.WorkOfficeNetworkId != 0
                            ? sim.WorkOfficeNetworkId
                            : sim.OfficeNetworkId,
                        HiredTick = sim.HiredAtTick,
                        EmploymentSource = "AdminAssigned"
                    };

                    if (memberBySim.TryGetValue(sim.NetworkId, out var member) &&
                        householdById.TryGetValue(member.HouseholdNetworkId, out var household))
                    {
                        life.HouseholdNetworkId = household.NetworkId;
                        life.UnitNetworkId = household.UnitNetworkId;
                        life.LeaseStartTick = household.MoveInTick;
                        life.LeaseActive = household.UnitNetworkId != 0;
                    }

                    state.PlayerLives.Add(life);
                }
            }
        }

        if (fromVersion <= 12)
        {
            state.PlayerProfiles ??= [];
            state.PlayerLives ??= [];
            state.FamilyInvitations ??= [];
            foreach (var sim in state.Sims)
            {
                if (!sim.AppearanceMigrated)
                {
                    sim.AppearanceVersion = 0;
                    sim.AppearanceMigrated = false;
                }
            }
        }

        if (fromVersion <= 13)
        {
            state.UtilityNetworks ??= [];
            state.ServiceLinks ??= [];
            state.HouseholdMeters ??= [];
            state.StoreInventories ??= [];
            state.Nutrition ??= [];
            if (state.GroceryCatalogVersion <= 0)
                state.GroceryCatalogVersion = Genelife.Components.Consumption.GroceryCatalog.Version;
            // Networks, links, meters and initial stock are bootstrapped during
            // restore so no legacy household is silently cut off.
        }

        if (fromVersion <= 14)
        {
            // v15 adds the social graph. Legacy worlds start with no invented
            // friendships, blocks or partnerships and an empty chat store.
            state.PlayerProfiles ??= [];
            state.SocialEdges ??= [];
            state.SocialRequests ??= [];
            state.SocialSettings ??= [];
            state.SocialRestrictions ??= [];
            state.PlayerRelationshipInvitations ??= [];

            var configured = state.SocialSettings
                .Select(setting => setting.PlayerId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var profile in state.PlayerProfiles)
            {
                if (configured.Contains(profile.PlayerId))
                    continue;
                state.SocialSettings.Add(new PersistedSocialSettings
                {
                    PlayerId = profile.PlayerId,
                    ShowDisplayName = true,
                    AllowNearbyForNonFriends = true,
                    FriendsOnlyMessages = false
                });
            }
        }

        state.SchemaVersion = PersistedWorldState.CurrentSchemaVersion;
    }
}
