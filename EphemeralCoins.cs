using BepInEx;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace EphemeralCoins
{
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [R2APISubmoduleDependency(nameof(NetworkingAPI))]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInDependency("com.KingEnderBrine.ProperSave", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.Varna.EphemeralCoins", "Ephemeral_Coins", "2.3.10")]
    public class EphemeralCoins : BaseUnityPlugin
    {
        public int numTimesRerolled;
        public List<CoinStorage> coinCounts = new List<CoinStorage>();
        /// <summary>Set when ProperSave restores coin counts so Run.Start does not treat the load as a blank slate.</summary>
        public bool restoredCoinCountsFromSave;

        public bool artifactEnabled {
            get
            {
                return BepConfig.EnableArtifact.Value == 2f || (RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(Assets.NewMoonArtifact));
            }
        }
        
        public static PluginInfo PInfo { get; private set; }
        public static EphemeralCoins instance;

        public static new BepInEx.Logging.ManualLogSource Logger;

        public void Awake()
        {
            PInfo = Info;
            instance = this;
            Logger = base.Logger;

            //internal counters
            numTimesRerolled = 0;

            //cost override
            RoR2Application.onLoad += AddCostType;
            RoR2Application.onLoad += AlwaysOnMode;

            Assets.Init();
            BepConfig.Init();
            Hooks.Init();

            NetworkingAPI.RegisterMessageType<SyncCoinStorage>();

            if (ProperSaveCompatibility.enabled) ProperSaveCompatibility.Setup();
        }

        ///
        /// Based on the PlayerStorage system used in https://github.com/WondaMegapon/Refightilization/blob/master/Refightilization/Refightilization.cs
        /// Keys players by NetworkUserId (stable) rather than Network_masterObjectId (can be invalid early / mid-run).
        ///
        public void SetupCoinStorage(List<CoinStorage> coinStorage, bool NewRun = true)
        {
            if (NewRun)
            {
                coinStorage.Clear();
                restoredCoinCountsFromSave = false;
            }
            foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)
            {
                // Skipping over Disconnected Players.
                if (coinStorage != null && user == null)
                {
                    Logger.LogDebug("A player disconnected! Skipping over what remains of them...");
                    continue;
                }

                // If this is ran mid-stage, just skip over existing players and add anybody who joined.
                if (!NewRun && coinStorage != null)
                {
                    bool flag = false;
                    foreach (CoinStorage player in coinStorage)
                    {
                        if (player.Matches(user))
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (flag) continue;
                }
                // Sync from setup only — award/deduct RPCs already update every peer.
                CoinStorage newPlayer = EnsureUser(user, syncToClients: true);
                Logger.LogDebug(newPlayer.name + " added to CoinStorage!");
            }
            Logger.LogDebug("Setting up CoinStorage finished.");
        }

        public CoinStorage EnsureUser(NetworkUser user, bool syncToClients = false)
        {
            if (user == null) return null;
            foreach (CoinStorage player in coinCounts)
            {
                if (player.Matches(user))
                {
                    if (string.IsNullOrEmpty(player.name)) player.name = user.userName;
                    return player;
                }
            }
            CoinStorage newPlayer = new CoinStorage();
            newPlayer.userId = user.id;
            newPlayer.name = user.userName;
            newPlayer.ephemeralCoinCount = 0;
            coinCounts.Add(newPlayer);
            if (syncToClients && NetworkServer.active)
            {
                new SyncCoinStorage(newPlayer.userId, newPlayer.name, newPlayer.ephemeralCoinCount).Send(NetworkDestination.Clients);
            }
            return newPlayer;
        }

        public void giveCoinsToUser(NetworkUser user, uint count)
        {
            CoinStorage player = EnsureUser(user);
            if (player == null) return;
            player.ephemeralCoinCount += count;
            Logger.LogDebug("giveCoinsToUser: " + user.userName + " " + count + " -> " + player.ephemeralCoinCount);
        }

        public void takeCoinsFromUser(NetworkUser user, uint count)
        {
            CoinStorage player = EnsureUser(user);
            if (player == null) return;
            if (count > player.ephemeralCoinCount) player.ephemeralCoinCount = 0;
            else player.ephemeralCoinCount -= count;
            Logger.LogDebug("takeCoinsFromUser: " + user.userName + " " + count + " -> " + player.ephemeralCoinCount);
        }

        public uint getCoinsFromUser(NetworkUser user)
        {
            if (Run.instance != null && user != null) {
                foreach (CoinStorage player in coinCounts)
                {
                    if (player.Matches(user))
                    {
                        //Spams the console due to HUD hook, only used for debugging.
                        //Logger.LogDebug("getCoinsFromUser: " + user.userName + player.ephemeralCoinCount);
                        return player.ephemeralCoinCount;
                    }
                }
            }
            return 0;
        }

        ///
        /// Override LunarCoin affordability so shops check ephemeral balance when the artifact is active.
        /// CostTypeCatalog.Register is private on runtime RoR2.dll — mutate the existing CostTypeDef instead.
        /// 
        public void AddCostType()
        {
            try
            {
                CostTypeDef def = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.LunarCoin);
                if (def == null)
                {
                    Logger.LogWarning("LunarCoin CostTypeDef missing; shop affordability override skipped.");
                    return;
                }

                def.isAffordable = delegate (CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
                {
                    NetworkUser networkUser = Util.LookUpBodyNetworkUser(context.activator.gameObject);
                    if (!(bool)networkUser) return false;
                    // When the artifact is active, never fall back to profile/lunarCoins — that lets shops ignore ephemeral balance.
                    if (artifactEnabled)
                    {
                        return getCoinsFromUser(networkUser) >= context.cost;
                    }
                    return networkUser.lunarCoins >= context.cost;
                };
                Logger.LogDebug("LunarCoin isAffordable delegate overridden.");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to override LunarCoin affordability (load will continue): " + ex);
            }
        }

        public void AlwaysOnMode()
        {
            //do prefabsetup for 'always on' functionality
            if (artifactEnabled)
            {
                Logger.LogDebug("Artifact mode 2 (always on/hidden); setting up prefabs.");
                NewMoonArtifactManager.PrefabSetup(true);
            }
        }

        public void ApplySavedCoinCounts(List<EphemeralCoinSaveEntry> saved)
        {
            coinCounts.Clear();
            if (saved == null)
            {
                restoredCoinCountsFromSave = false;
                return;
            }

            foreach (EphemeralCoinSaveEntry entry in saved)
            {
                if (entry == null) continue;
                CoinStorage player = entry.ToCoinStorage();
                coinCounts.Add(player);
                if (NetworkServer.active)
                {
                    new SyncCoinStorage(player.userId, player.name, player.ephemeralCoinCount).Send(NetworkDestination.Clients);
                }
            }

            restoredCoinCountsFromSave = true;
            Logger.LogInfo("ProperSave: restored " + coinCounts.Count + " ephemeral coin entr" + (coinCounts.Count == 1 ? "y" : "ies") + ".");
        }

        ///
        /// Required for BTB to change the costs of the pre-loaded prefab instances.
        /// 
        public IEnumerator DelayedLunarPriceChange()
        {
            yield return new WaitForSeconds(2f);
            var purchaseInteractions = InstanceTracker.GetInstancesList<PurchaseInteraction>();
            foreach (PurchaseInteraction purchaseInteraction in purchaseInteractions)
            {
                if (purchaseInteraction.name.StartsWith("LunarShop"))
                {
                    purchaseInteraction.Networkcost = (int)BepConfig.ShopCost.Value;
                    if (BepConfig.ShopCost.Value == 0) { purchaseInteraction.costType = CostTypeIndex.None; }
                }
                else if (purchaseInteraction.name.StartsWith("LunarRecycler"))
                {
                    purchaseInteraction.Networkcost = (int)BepConfig.RerollCost.Value;
                    if (BepConfig.RerollCost.Value == 0) { purchaseInteraction.costType = CostTypeIndex.None; }
                }
            }
        }

        ///
        /// Our opening message + starting coins functionality, packed in a coroutine so that it can run alongside Run.Start().
        ///
        public IEnumerator DelayedStartingLunarCoins()
        {
            yield return new WaitForSeconds(1f);
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<color=#beeca1><size=15px>A new moon rises...</size></color>" });
            yield return new WaitForSeconds(3f);
            if (BepConfig.StartingCoins.Value > 0)
            {
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = "<nobr><color=#adf2fa><sprite name=\"LunarCoin\" tint=1>" + BepConfig.StartingCoins.Value + "</color></nobr> " + (BepConfig.StartingCoins.Value > 1 ? "coins fade" : "coin fades") + " into existence..."
                });
                for (int i = 0; i < PlayerCharacterMasterController.instances.Count; i++)
                {
                    PlayerCharacterMasterController.instances[i].networkUser.AwardLunarCoins((uint)BepConfig.StartingCoins.Value);
                }
            }
        }
    }
}
