using System;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EphemeralCoins
{
	public static class NewMoonArtifactManager
	{
		private static bool runStartHooked;

		[SystemInitializer(new Type[] { typeof(ArtifactCatalog) })]
		private static void Init()
		{
			EphemeralCoins.Logger.LogDebug("NewMoonArtifactManager initialized");
			RunArtifactManager.onArtifactEnabledGlobal += OnArtifactEnabled;
			RunArtifactManager.onArtifactDisabledGlobal += OnArtifactDisabled;
		}

		private static void OnArtifactEnabled(RunArtifactManager runArtifactManager, ArtifactDef artifactDef)
		{
			if (artifactDef != Assets.NewMoonArtifact)
			{
				return;
			}

			EphemeralCoins.Logger.LogDebug("OnArtifactEnabled hook applied");
			if (!runStartHooked)
			{
				On.RoR2.Run.Start += Run_Start;
				runStartHooked = true;
			}

			// If a run is already underway, apply immediately without waiting for the next Start.
			if (Run.instance)
			{
				SafePrefabSetup(true);
			}
		}

		private static void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
		{
			// Always let vanilla (and other mods) finish run init first. PrefabSetup must never block spawn.
			orig(self);
			EphemeralCoins.Logger.LogDebug("Artifact enabled");
			SafePrefabSetup(true);
		}

		private static void OnArtifactDisabled(RunArtifactManager runArtifactManager, ArtifactDef artifactDef)
		{
			if (artifactDef != Assets.NewMoonArtifact)
			{
				return;
			}

			if (runStartHooked)
			{
				On.RoR2.Run.Start -= Run_Start;
				runStartHooked = false;
			}
			EphemeralCoins.Logger.LogDebug("Artifact disabled");
			SafePrefabSetup(false);
		}

		public static void PrefabSetup(bool set = false)
		{
			SafePrefabSetup(set);
		}

		private static void SafePrefabSetup(bool set)
		{
			try
			{
				PrefabSetupInternal(set);
			}
			catch (Exception ex)
			{
				EphemeralCoins.Logger.LogError("PrefabSetup failed (run init will continue): " + ex);
			}
		}

		private static void PrefabSetupInternal(bool set)
		{
			EphemeralCoins.Logger.LogDebug("PrefabSetup " + set);

			///
			/// Swap the Lunar Coin's model and pickup settings around based on whether the artifact is enabled.
			///
			PickupIndex coinIndex = PickupCatalog.FindPickupIndex("LunarCoin.Coin0");
			PickupDef TheCoinDef = coinIndex.pickupDef;
			if (TheCoinDef == null)
			{
				EphemeralCoins.Logger.LogWarning("PrefabSetup: LunarCoin.Coin0 pickup def missing; skipping coin visual updates.");
			}
			else
			{
				TheCoinDef.nameToken = set ? "Ephemeral Coin" : "PICKUP_LUNAR_COIN";
				TheCoinDef.interactContextToken = set ? "Pick up Ephemeral Coin" : "LUNAR_COIN_PICKUP_CONTEXT";
				TheCoinDef.baseColor = set ? new Color32(96, 254, byte.MaxValue, byte.MaxValue) : new Color32(48, 127, byte.MaxValue, byte.MaxValue);
				TheCoinDef.darkColor = set ? new Color32(152, 168, byte.MaxValue, byte.MaxValue) : new Color32(76, 84, 144, byte.MaxValue);
			}

			GameObject TheCoin = LoadAddressable<GameObject>(Assets.PickupLunarCoin);
			Transform coinMesh = FindCoinMesh(TheCoin);
			if (TheCoin == null || coinMesh == null)
			{
				EphemeralCoins.Logger.LogWarning("PrefabSetup: could not locate lunar coin mesh transform; skipping mesh/material swap.");
			}
			else
			{
				MeshFilter meshFilter = coinMesh.GetComponent<MeshFilter>();
				MeshRenderer meshRenderer = coinMesh.GetComponent<MeshRenderer>();
				if (meshFilter)
				{
					GameObject meshSource = LoadAddressable<GameObject>(set ? Assets.LunarCoinMesh : Assets.LunarCoinWithHoleMesh);
					MeshFilter sourceFilter = meshSource ? meshSource.GetComponent<MeshFilter>() : null;
					if (sourceFilter && sourceFilter.sharedMesh)
					{
						meshFilter.sharedMesh = sourceFilter.sharedMesh;
					}
					else
					{
						EphemeralCoins.Logger.LogWarning("PrefabSetup: failed to load replacement lunar coin mesh.");
					}
				}

				if (meshRenderer)
				{
					Material mat = set
						? Assets.mainBundle.LoadAsset<Material>("matEphemeralCoin")
						: LoadAddressable<Material>(Assets.LunarCoinPlaceholderMat);
					if (mat)
					{
						meshRenderer.sharedMaterial = mat;
					}
					else
					{
						EphemeralCoins.Logger.LogWarning("PrefabSetup: failed to load lunar coin material.");
					}
				}
			}

			///
			/// Changing the interactable costs.
			///
			/// Only the LunarChest (pods) and FrogInteractable (moonfrog) actually do anything here, because Bazaar is full of pre-loaded instances of the prefabs.
			/// Still change the other prefabs anyway, just in case Hopoo decides to change something down the line.
			/// The Seer Stations will always require hooks, because their scripts set their price at runtime. Why? Ask Hopoo.
			foreach (string x in Assets.lunarInteractables)
			{
				GameObject z = LoadAddressable<GameObject>(x);
				if (!z)
				{
					EphemeralCoins.Logger.LogWarning("PrefabSetup: failed to load lunarInteractable " + x);
					continue;
				}

				int zValue = 0;
				switch (z.name)
				{
					case "LunarRecycler":
						zValue = (int)BepConfig.RerollCost.Value;
						break;
					case "LunarChest":
						zValue = (int)BepConfig.PodCost.Value;
						break;
					case "LunarShopTerminal":
						zValue = (int)BepConfig.ShopCost.Value;
						break;
					case "SeerStation":
						zValue = (int)BepConfig.SeerCost.Value;
						break;
					case "FrogInteractable":
						zValue = (int)BepConfig.FrogCost.Value;
						FrogController frog = z.GetComponent<FrogController>();
						if (frog) frog.maxPets = (int)BepConfig.FrogPets.Value;
						break;
					default:
						EphemeralCoins.Logger.LogWarning("Unknown lunarInteractable " + x + ", will default to 0 cost!");
						break;
				}

				PurchaseInteraction purchase = z.GetComponent<PurchaseInteraction>();
				if (!purchase)
				{
					EphemeralCoins.Logger.LogWarning("PrefabSetup: PurchaseInteraction missing on " + x);
					continue;
				}

				// Write the backing field on the shared prefab. Networkcost/SetSyncVar on Addressable
				// prefab assets can corrupt client network state during run start.
				purchase.cost = zValue;
				if (zValue == 0)
				{
					purchase.costType = CostTypeIndex.None;
				}
			}
		}

		private static T LoadAddressable<T>(string key) where T : UnityEngine.Object
		{
			try
			{
				return Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
			}
			catch (Exception ex)
			{
				EphemeralCoins.Logger.LogWarning("PrefabSetup: Addressables key failed (" + key + "): " + ex.Message);
				return null;
			}
		}

		private static Transform FindCoinMesh(GameObject coinPrefab)
		{
			if (!coinPrefab) return null;

			Transform mesh = coinPrefab.transform.Find("Coin5Mesh");
			if (mesh) return mesh;

			mesh = coinPrefab.transform.Find("LunarCoinMesh");
			if (mesh) return mesh;

			MeshFilter filter = coinPrefab.GetComponentInChildren<MeshFilter>(true);
			return filter ? filter.transform : null;
		}
	}
}
