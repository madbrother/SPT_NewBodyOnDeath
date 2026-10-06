using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace MadBro.NewBodyOnDeath
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "madbro.newbodyondeath";
        public override string Name { get; init; } = "NewBodyOnDeath";
        public override string Author { get; init; } = "MadBrother";
        public override List<string>? Contributors { get; init; }
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.1");
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");


        public override List<string>? Incompatibilities { get; init; }
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public override string? Url { get; init; }
        public override bool? IsBundleMod { get; init; }
        public override string License { get; init; } = "MIT";
    }

    [Injectable(TypePriority = OnLoadOrder.PreSptModLoader)]
    public class Plugin(ISptLogger<HealthHelper> logger) : IOnLoad
    {
        public Task OnLoad()
        {
            new NewBodyOnDeathPatch().Enable();

            logger.Success($"NBoD harmony patch has successfully loaded!");

            return Task.CompletedTask;
        }

        public class NewBodyOnDeathPatch() : AbstractPatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(HealthHelper).GetMethod(nameof(HealthHelper.ApplyHealthChangesToProfile));
            }

            [PatchPrefix]
            public static bool Prefix(MongoId sessionId, PmcData pmcProfileToUpdate, BotBaseHealth healthChanges, bool isDead)
            {
                // We add a log message to the StartAsync method
                ISptLogger<HealthHelper> logger = (ISptLogger<HealthHelper>)ServiceLocator.ServiceProvider.GetService(typeof(ISptLogger<HealthHelper>));
                //logger.Info("NBoD Applying prefix to ApplyHealthChangesToProfile");

                if (isDead)
                {
                    try
                    {
                        MaxHydroEnergyTemp(logger, pmcProfileToUpdate, healthChanges, isDead);
                        HealBody(logger, pmcProfileToUpdate, healthChanges.BodyParts, null, isDead);
                    } catch (Exception ex)
                    {
                        logger.Error($"NBoD Exception in prefix: {ex}");
                    }
                    return false;
                }

                //logger.Info("NBoD He not dead");
                return true;
            }

            private static void MaxHydroEnergyTemp(ISptLogger<HealthHelper> logger, PmcData profileToUpdate, BotBaseHealth healthChanges, bool isDead)
            {
                //logger.Info("NBoD maxing hydration, energy, and temperature");

                var profileHealth = profileToUpdate.Health;
                profileHealth.Hydration.Current = healthChanges.Hydration.Maximum;
                profileHealth.Energy.Current = healthChanges.Energy.Maximum;
                profileHealth.Temperature.Current = healthChanges.Temperature.Maximum;

                //logger.Info("NBoD maxed hydration, energy, and temperature");
            }

            private static void HealBody(ISptLogger<HealthHelper> logger, PmcData profileToAdjust, Dictionary<string, BodyPartHealth> bodyPartChanges, HashSet<string>? effectsToSkip = null, bool isDead = false, bool playerWasCursed = false)
            {
                //logger.Info("NBoD healing body");

                foreach (var (partName, partProperties) in bodyPartChanges)
                {
                    if (profileToAdjust.Health?.BodyParts?.TryGetValue(partName, out var matchingProfilePart) is null or false)
                    {
                        continue;
                    }

                    // Restore limbs health to maximum
                    matchingProfilePart.Health.Current = matchingProfilePart.Health.Maximum;

                    // Process each effect for each part
                    foreach (var (key, _) in partProperties.Effects ?? [])
                    {
                        if (key == "MildMusclePain" || key == "SevereMusclePain")
                        {
                            matchingProfilePart.Effects.TryGetValue(key, out var effect);
                            if (effect is not null)
                            {
                                effect.Time = 0;
                            }
                            continue;
                        }

                        matchingProfilePart.Effects ??= [];
                        if (matchingProfilePart.Effects.ContainsKey(key))
                        {
                            matchingProfilePart.Effects[key] = null;
                        }
                    }
                }

                //logger.Info("NBoD healed body");
            }
        }
    }
}
