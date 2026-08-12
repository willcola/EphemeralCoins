using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace EphemeralCoins
{
    public static class ProperSaveCompatibility
    {
        public const string SaveDataKey = "EphemeralCoins.coinCounts";

        private static bool? _enabled;

        public static bool enabled
        {
            get
            {
                if (_enabled == null) {
                    _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.ProperSave");
                }
                return (bool)_enabled;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsRunNew()
        {
            return !ProperSave.Loading.IsLoading;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Setup()
        {
            ProperSave.SaveFile.OnGatherSaveData += OnGatherSaveData;
            ProperSave.Loading.OnLoadingEnded += OnLoadingEnded;
            EphemeralCoins.Logger.LogInfo("ProperSave compatibility enabled (ephemeral coin save/load).");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnGatherSaveData(Dictionary<string, object> dict)
        {
            if (EphemeralCoins.instance == null) return;

            List<EphemeralCoinSaveEntry> entries = new List<EphemeralCoinSaveEntry>();
            foreach (CoinStorage player in EphemeralCoins.instance.coinCounts)
            {
                if (player == null) continue;
                entries.Add(EphemeralCoinSaveEntry.From(player));
            }

            dict[SaveDataKey] = entries;
            EphemeralCoins.Logger.LogDebug("ProperSave: gathered " + entries.Count + " ephemeral coin entries.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnLoadingEnded(ProperSave.SaveFile save)
        {
            if (EphemeralCoins.instance == null || save == null) return;

            try
            {
                List<EphemeralCoinSaveEntry> entries = save.TryGetModdedData<List<EphemeralCoinSaveEntry>>(SaveDataKey);
                if (entries == null)
                {
                    EphemeralCoins.Logger.LogDebug("ProperSave: no ephemeral coin data in save (starting at 0).");
                    return;
                }

                EphemeralCoins.instance.ApplySavedCoinCounts(entries);
            }
            catch (Exception ex)
            {
                EphemeralCoins.Logger.LogWarning("ProperSave: failed to restore ephemeral coins: " + ex);
            }
        }
    }

    public static class RiskOfOptionsCompatibility
    {
        private static bool? _enabled;

        public static bool enabled
        {
            get
            {
                if (_enabled == null)
                {
                    _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");
                }
                return (bool)_enabled;
            }
        }

        public static void InvokeAddOptionStepSlider(ConfigEntry<float> configEntry, int min, int max, float increment) => InvokeAddOptionStepSlider(configEntry, min, max, increment, false, false);
        public static void InvokeAddOptionStepSlider(ConfigEntry<float> configEntry, int min, int max, float increment, bool checkArtifact) => InvokeAddOptionStepSlider(configEntry, min, max, increment, checkArtifact, false);
        public static void InvokeAddOptionStepSlider(ConfigEntry<float> configEntry, int min, int max, float increment, bool checkArtifact, bool restartRequired)
        {
            RiskOfOptions.ModSettingsManager.AddOption(
                new StepSliderOption(
                    configEntry, 
                    new StepSliderConfig() { min = min, max = max, increment = increment, restartRequired = restartRequired, checkIfDisabled = delegate () { return checkIfDisabled(checkArtifact); } }
                    )
                );
        }

        public static void InvokeAddOptionCheckBox(ConfigEntry<bool> configEntry) => InvokeAddOptionCheckBox(configEntry, false, false);
        public static void InvokeAddOptionCheckBox(ConfigEntry<bool> configEntry, bool checkArtifact) => InvokeAddOptionCheckBox(configEntry, checkArtifact, false);
        public static void InvokeAddOptionCheckBox(ConfigEntry<bool> configEntry, bool checkArtifact, bool restartRequired)
        {
            RiskOfOptions.ModSettingsManager.AddOption(
                new CheckBoxOption(
                    configEntry,
                    new CheckBoxConfig() { restartRequired = restartRequired, checkIfDisabled = delegate () { return checkIfDisabled(checkArtifact); } }
                    )
                );
        }

        public static bool checkIfDisabled(bool checkArtifact = false)
        {
            return (RoR2.Run.instance != null | (checkArtifact & BepConfig.EnableArtifact.Value == 0f));
        }

        public static void InvokeSetModIcon(UnityEngine.Sprite iconSprite)
        {
            RiskOfOptions.ModSettingsManager.SetModIcon(iconSprite);
        }

        public static void InvokeSetModDescription(string desc)
        {
            RiskOfOptions.ModSettingsManager.SetModDescription(desc);
        }

    }
}
