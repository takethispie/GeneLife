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
            foreach (var genos in state.Genoses)
            {
                if (genos.WalletBalanceCents == 0 && genos.WalletBalance != 0)
                    genos.WalletBalanceCents = (long)Math.Round((decimal)genos.WalletBalance * 100m);
                genos.WalletBalance = 0;
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
            // deterministic backfill needs the world seed and each genos's network id;
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
            foreach (var genos in state.Genoses)
                genos.HasWallet |= genos.WalletBalanceCents != 0 || genos.WalletBalance != 0 ||
                                 genos.EmployerNetworkId != 0;
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
                var memberByGenos = state.HouseholdMembers
                    .GroupBy(member => member.GenosNetworkId)
                    .ToDictionary(group => group.Key, group => group.First());
                var householdById = state.Households
                    .GroupBy(household => household.NetworkId)
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var genos in state.Genoses.Where(genos => !string.IsNullOrWhiteSpace(genos.PlayerId)))
                {
                    state.PlayerProfiles.Add(new PersistedPlayerProfile
                    {
                        PlayerId = genos.PlayerId!,
                        GenosNetworkId = genos.NetworkId,
                        ContinuationPolicy = "Heir",
                        AwaitingContinuation = false,
                        Observer = false,
                        CreatedTick = 0,
                        LastActionTick = 0,
                        LastSequence = 0
                    });

                    var life = new PersistedPlayerLife
                    {
                        GenosNetworkId = genos.NetworkId,
                        HasEmployment = genos.EmployerNetworkId != 0,
                        EmploymentOfficeNetworkId = genos.WorkOfficeNetworkId != 0
                            ? genos.WorkOfficeNetworkId
                            : genos.OfficeNetworkId,
                        HiredTick = genos.HiredAtTick,
                        EmploymentSource = "AdminAssigned"
                    };

                    if (memberByGenos.TryGetValue(genos.NetworkId, out var member) &&
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
            foreach (var genos in state.Genoses)
            {
                if (!genos.AppearanceMigrated)
                {
                    genos.AppearanceVersion = 0;
                    genos.AppearanceMigrated = false;
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

        if (fromVersion <= 15)
        {
            // v16 splits per-cycle accumulators from the last-cycle display
            // values. Older saves hold the in-progress cycle in the last-cycle
            // fields, so seed the new accumulators from them.
            foreach (var company in state.Companies)
            {
                company.RevenueThisCycleCents = company.RevenueLastCycleCents;
                company.WagesThisCycleCents = company.WagesLastCycleCents;
                company.CostsThisCycleCents = company.CostsLastCycleCents;
                company.InterestPaidThisCycleCents = company.InterestPaidLastCycleCents;
            }
        }

        state.SchemaVersion = PersistedWorldState.CurrentSchemaVersion;
    }
}
