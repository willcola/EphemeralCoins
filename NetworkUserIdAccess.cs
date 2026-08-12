using System.Reflection;
using RoR2;

namespace EphemeralCoins
{
	/// <summary>
	/// NetworkUserId.value/strValue/subId are often non-public on runtime RoR2.dll
	/// (GameLibs publicizer is compile-only).
	/// </summary>
	internal static class NetworkUserIdAccess
	{
		private static readonly FieldInfo ValueField = typeof(NetworkUserId).GetField(
			"value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo StrValueField = typeof(NetworkUserId).GetField(
			"strValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo SubIdField = typeof(NetworkUserId).GetField(
			"subId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly PropertyInfo ValueProp = typeof(NetworkUserId).GetProperty(
			"value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly PropertyInfo StrValueProp = typeof(NetworkUserId).GetProperty(
			"strValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly PropertyInfo SubIdProp = typeof(NetworkUserId).GetProperty(
			"subId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		public static ulong GetValue(NetworkUserId id)
		{
			object boxed = id;
			if (ValueField != null) return (ulong)ValueField.GetValue(boxed);
			if (ValueProp != null) return (ulong)ValueProp.GetValue(boxed);
			return 0UL;
		}

		public static string GetStrValue(NetworkUserId id)
		{
			object boxed = id;
			if (StrValueField != null) return StrValueField.GetValue(boxed) as string;
			if (StrValueProp != null) return StrValueProp.GetValue(boxed) as string;
			return null;
		}

		public static byte GetSubId(NetworkUserId id)
		{
			object boxed = id;
			if (SubIdField != null) return (byte)SubIdField.GetValue(boxed);
			if (SubIdProp != null) return (byte)SubIdProp.GetValue(boxed);
			return 0;
		}
	}
}
