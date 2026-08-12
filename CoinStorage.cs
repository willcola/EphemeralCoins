using System;
using RoR2;
using UnityEngine.Networking;
using R2API.Networking.Interfaces;

namespace EphemeralCoins
{
    public class CoinStorage
    {
        public NetworkUserId userId;
        public string name;
        public uint ephemeralCoinCount;

        public bool Matches(NetworkUser user)
        {
            if (user == null) return false;
            if (userId.Equals(user.id)) return true;
            // ProperSave reload can change NetworkUserId shape; fall back to stable display name.
            return !string.IsNullOrEmpty(name)
                && !string.IsNullOrEmpty(user.userName)
                && string.Equals(name, user.userName, StringComparison.Ordinal);
        }

        public bool Matches(NetworkUserId id, string userName)
        {
            if (userId.Equals(id)) return true;
            return !string.IsNullOrEmpty(name)
                && !string.IsNullOrEmpty(userName)
                && string.Equals(name, userName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// JSON-friendly ProperSave payload. Uses stable NetworkUserId fields instead of NetworkInstanceId
    /// (master object IDs are not valid across save/load).
    /// </summary>
    [Serializable]
    public class EphemeralCoinSaveEntry
    {
        public ulong idValue;
        public string idStr;
        public byte idSub;
        public string name;
        public uint count;

        public static EphemeralCoinSaveEntry From(CoinStorage storage)
        {
            return new EphemeralCoinSaveEntry
            {
                idValue = NetworkUserIdAccess.GetValue(storage.userId),
                idStr = NetworkUserIdAccess.GetStrValue(storage.userId) ?? "",
                idSub = NetworkUserIdAccess.GetSubId(storage.userId),
                name = storage.name,
                count = storage.ephemeralCoinCount
            };
        }

        public CoinStorage ToCoinStorage()
        {
            return new CoinStorage
            {
                userId = !string.IsNullOrEmpty(idStr)
                    ? NetworkUserId.FromIp(idStr, idSub)
                    : NetworkUserId.FromId(idValue, idSub),
                name = name,
                ephemeralCoinCount = count
            };
        }
    }

    public class SyncCoinStorage : INetMessage
    {
        public NetworkUserId userId;
        public string name;
        public uint ephemeralCoinCount;

        public SyncCoinStorage(){}

        public SyncCoinStorage(NetworkUserId userId, string name, uint ephemeralCoinCount)
        {
            this.userId = userId;
            this.name = name;
            this.ephemeralCoinCount = ephemeralCoinCount;
        }

        public void Deserialize(NetworkReader reader)
        {
            ulong value = reader.ReadPackedUInt64();
            string strValue = reader.ReadString();
            byte subId = reader.ReadByte();
            userId = !string.IsNullOrEmpty(strValue)
                ? NetworkUserId.FromIp(strValue, subId)
                : NetworkUserId.FromId(value, subId);
            name = reader.ReadString();
            ephemeralCoinCount = reader.ReadUInt32();
        }

        public void OnReceived()
        {
            if (NetworkServer.active)
            {
                EphemeralCoins.Logger.LogWarning("SyncCoinStorage: Host ran this. Skipping.");
                return;
            }

            if (EphemeralCoins.instance == null) return;

            foreach (CoinStorage player in EphemeralCoins.instance.coinCounts)
            {
                if (player.Matches(userId, name))
                {
                    player.userId = userId;
                    if (!string.IsNullOrEmpty(name)) player.name = name;
                    player.ephemeralCoinCount = ephemeralCoinCount;
                    return;
                }
            }

            EphemeralCoins.instance.coinCounts.Add(new CoinStorage
            {
                userId = userId,
                name = name,
                ephemeralCoinCount = ephemeralCoinCount
            });
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt64(NetworkUserIdAccess.GetValue(userId));
            writer.Write(NetworkUserIdAccess.GetStrValue(userId) ?? "");
            writer.Write(NetworkUserIdAccess.GetSubId(userId));
            writer.Write(name);
            writer.Write(ephemeralCoinCount);
        }
    }
}
