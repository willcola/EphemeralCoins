using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;

namespace EphemeralCoins
{
    public static class ProperSaveCompatibility
    {
        public const string SaveDataKey = "EphemeralCoins.coinCounts";

        private static bool? _enabled;
        private static List<EphemeralCoinSaveEntry> _lastRestored;
        private static int _preserveLoadId;
        private static int _loadId;
        private static int _delayedReapplyLoadId;
        private static bool _stageHooked;

        /// <summary>
        /// True only during the current ProperSave load window (OnLoadingEnded → first stage gather).
        /// </summary>
        public static bool ShouldPreserveRestoredCoins =>
            _preserveLoadId != 0 && _preserveLoadId == _loadId && _lastRestored != null;

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
        public static bool IsLoading()
        {
            try
            {
                return ProperSave.Loading.IsLoading;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Setup()
        {
            ProperSave.SaveFile.OnGatherSaveData += OnGatherSaveData;
            ProperSave.Loading.OnLoadingEnded += OnLoadingEnded;
            if (!_stageHooked)
            {
                Stage.onStageStartGlobal += OnStageStartGlobal;
                Run.onRunDestroyGlobal += OnRunDestroyGlobal;
                _stageHooked = true;
            }
            EphemeralCoins.Logger.LogInfo("ProperSave compatibility enabled (ephemeral coin save/load).");
        }

        private static void OnRunDestroyGlobal(Run run)
        {
            ClearRestoredSnapshot();
            if (EphemeralCoins.instance != null)
            {
                EphemeralCoins.instance.coinCounts.Clear();
                EphemeralCoins.instance.restoredCoinCountsFromSave = false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnGatherSaveData(Dictionary<string, object> dict)
        {
            if (EphemeralCoins.instance == null) return;

            if (ShouldPreserveRestoredCoins)
            {
                EphemeralCoins.instance.ApplySavedCoinCounts(_lastRestored);
            }
            _preserveLoadId = 0;

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

                _loadId++;
                _preserveLoadId = _loadId;
                _delayedReapplyLoadId = _loadId;
                _lastRestored = CloneList(entries);
                EphemeralCoins.instance.ApplySavedCoinCounts(_lastRestored);
                EphemeralCoins.instance.StartCoroutine(PostLoadReapplyCoroutine(_loadId));
            }
            catch (Exception ex)
            {
                EphemeralCoins.Logger.LogWarning("ProperSave: failed to restore ephemeral coins: " + ex);
            }
        }

        private static void OnStageStartGlobal(Stage stage)
        {
            if (!ShouldPreserveRestoredCoins || EphemeralCoins.instance == null) return;
            EphemeralCoins.instance.ApplySavedCoinCounts(_lastRestored);
        }

        private static IEnumerator PostLoadReapplyCoroutine(int loadId)
        {
            yield return new WaitForSeconds(1f);
            if (loadId != _delayedReapplyLoadId || _lastRestored == null || EphemeralCoins.instance == null) yield break;
            EphemeralCoins.instance.ApplySavedCoinCounts(_lastRestored);
            EphemeralCoins.Logger.LogDebug("ProperSave: re-applied ephemeral coin snapshot after load settle.");
            if (_preserveLoadId == 0) _lastRestored = null;
        }

        public static void ClearRestoredSnapshot()
        {
            _loadId++;
            _preserveLoadId = 0;
            _delayedReapplyLoadId = 0;
            _lastRestored = null;
        }

        private static List<EphemeralCoinSaveEntry> CloneList(List<EphemeralCoinSaveEntry> source)
        {
            List<EphemeralCoinSaveEntry> copy = new List<EphemeralCoinSaveEntry>(source.Count);
            foreach (EphemeralCoinSaveEntry entry in source)
            {
                if (entry == null) continue;
                copy.Add(new EphemeralCoinSaveEntry
                {
                    idValue = entry.idValue,
                    idStr = entry.idStr,
                    idSub = entry.idSub,
                    name = entry.name,
                    count = entry.count
                });
            }
            return copy;
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
