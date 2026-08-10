using BepInEx;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EphemeralCoins
{
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [R2APISubmoduleDependency(nameof(NetworkingAPI))]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInDependency("com.KingEnderBrine.ProperSave", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.Varna.EphemeralCoins", "Ephemeral_Coins", "2.3.8")]
    public class EphemeralCoins : BaseUnityPlugin
    {
        public int numTimesRerolled;
        public List<CoinStorage> coinCounts = new List<CoinStorage>();

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

            //Utterly broken, fix later
            //if (ProperSaveCompatibility.enabled) ProperSaveSetup();
        }

        ///
        /// Based on the PlayerStorage system used in https://github.com/WondaMegapon/Refightilization/blob/master/Refightilization/Refightilization.cs
        ///
        public void SetupCoinStorage(List<CoinStorage> coinStorage, bool NewRun = true)
        {
            if (NewRun) coinStorage.Clear();
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
                        if (player.user.Equals(user.id))
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (flag) continue;
                }
                CoinStorage newPlayer = new CoinStorage();
                newPlayer.user = user.Network_masterObjectId;
                newPlayer.name = user.userName;
                newPlayer.ephemeralCoinCount = 0;
                coinStorage.Add(newPlayer);
                Logger.LogDebug(newPlayer.name + " added to CoinStorage!");
                new SyncCoinStorage(newPlayer.user, newPlayer.name, 0).Send(NetworkDestination.Clients);
            }
            Logger.LogDebug("Setting up CoinStorage finished.");
        }

        public void giveCoinsToUser(NetworkUser user, uint count)
        {
            foreach (CoinStorage player in coinCounts)
            {
                if (player.user.Equals(user.Network_masterObjectId))
                {
                    player.ephemeralCoinCount += count;
                    Logger.LogDebug("giveCoinsToUser: " + user.userName + " " + count);
                }
            }
        }

        public void takeCoinsFromUser(NetworkUser user, uint count)
        {
            foreach (CoinStorage player in coinCounts)
            {
                if (player.user.Equals(user.Network_masterObjectId))
                {
                    player.ephemeralCoinCount -= count;
                    Logger.LogDebug("takeCoinsFromUser: " + user.userName + " " + count);
                }
            }
        }

        public uint getCoinsFromUser(NetworkUser user)
        {
            if (Run.instance != null) {
                foreach (CoinStorage player in coinCounts)
                {
                    if (player.user.Equals(user.Network_masterObjectId))
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
        /// Override the CostType delegates so that we can use a different coin count check when the artifact is active. Hacky, but works.
        /// 
        public void AddCostType()
        {
            CostTypeDef newdef = new CostTypeDef
            {
                costStringFormatToken = "COST_LUNARCOIN_FORMAT",
                saturateWorldStyledCostString = false,
                darkenWorldStyledCostString = true,
                isAffordable = delegate (CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
                {
                    NetworkUser networkUser2 = Util.LookUpBodyNetworkUser(context.activator.gameObject);
                    if (artifactEnabled) {
                        foreach (CoinStorage player in coinCounts)
                        {
                            if (player.user.Equals(networkUser2.Network_masterObjectId))
                            {
                                return player.ephemeralCoinCount >= context.cost;
                            }
                        }
                    }
                    return (bool)networkUser2 && networkUser2.lunarCoins >= context.cost;
                },
                payCost = delegate (CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults results)
                {
                    NetworkUser networkUser = Util.LookUpBodyNetworkUser(context.activator.gameObject);
                    if ((bool)networkUser)
                    {
                        networkUser.DeductLunarCoins((uint)context.cost);
                        RoR2.Items.MultiShopCardUtils.OnNonMoneyPurchase(context);
                    }
                },
                colorIndex = ColorCatalog.ColorIndex.LunarCoin
            };

            CostTypeCatalog.Register(CostTypeIndex.LunarCoin, newdef);
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

        /*
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ProperSaveSetup()
        {
            ProperSave.SaveFile.OnGatherSaveData += (dict) =>
            {
                if (dict.ContainsKey("ephemeralCoinCount"))
                    dict["ephemeralCoinCount"] = coinCounts;
                else
                    dict.Add("ephemeralCoinCount", coinCounts);
            };

            ProperSave.Loading.OnLoadingEnded += (save) =>
            {
                coinCounts = save.GetModdedData<List<CoinStorage>>("ephemeralCoinCount");
            };
        }
        */

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
