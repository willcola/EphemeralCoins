using RoR2;
using UnityEngine;

namespace EphemeralCoins
{
	public static class Assets
	{
		public static AssetBundle mainBundle;

		public const string bundleName = "ephemeralcoins";

		public static ArtifactDef NewMoonArtifact;

		internal const string PickupLunarCoin = "RoR2/Base/MiscPickups/LunarCoin/PickupLunarCoin.prefab";
		internal const string LunarCoinMesh = "RoR2/Base/Common/VFX/Coins/mdlLunarCoin.fbx";
		internal const string LunarCoinWithHoleMesh = "RoR2/Base/Common/VFX/Coins/mdlLunarCoinWithHole.fbx";
		internal const string LunarCoinPlaceholderMat = "RoR2/Base/Common/VFX/Coins/matLunarCoinPlaceholder.mat";

		internal static string[] lunarInteractables = {
			"RoR2/Base/Interactables/LunarRecycler/LunarRecycler.prefab",
			"RoR2/Base/Interactables/LunarChest/LunarChest.prefab",
			"RoR2/Base/Interactables/LunarShopTerminal/LunarShopTerminal.prefab",
			"RoR2/Base/Scenes/bazaar/SeerStation.prefab",
			"RoR2/Base/Scenes/moon/Natural/Prefabs/FrogInteractable.prefab"
		};

		public static string AssetBundlePath
		{
			get
			{
				return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(EphemeralCoins.PInfo.Location), bundleName);
			}
		}

		public static void Init()
		{
			mainBundle = AssetBundle.LoadFromFile(AssetBundlePath);

			NewMoonArtifact = ScriptableObject.CreateInstance<ArtifactDef>();
			NewMoonArtifact.nameToken = "Artifact of the New Moon";
			NewMoonArtifact.descriptionToken = "Lunar Coins become Ephemeral Coins, a temporary per-run currency. <size=70%>(Your saved Lunar Coin count is unaffected.)</size>";
			NewMoonArtifact.smallIconSelectedSprite = mainBundle.LoadAsset<Sprite>("texArtifactNewMoonEnabled");
			NewMoonArtifact.smallIconDeselectedSprite = mainBundle.LoadAsset<Sprite>("texArtifactNewMoonDisabled");
		}
	}
}
