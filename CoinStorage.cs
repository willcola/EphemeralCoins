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
            return user != null && userId.Equals(user.id);
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
            CoinStorage newPlayer = new CoinStorage();
            newPlayer.userId = userId;
            newPlayer.name = name;
            newPlayer.ephemeralCoinCount = ephemeralCoinCount;

            bool flag = false;
            foreach (CoinStorage player in EphemeralCoins.instance.coinCounts)
            {
                if (player.userId.Equals(newPlayer.userId))
                {
                    player.name = newPlayer.name;
                    player.ephemeralCoinCount = newPlayer.ephemeralCoinCount;
                    flag = true;
                    break;
                }
            }
            if (!flag) EphemeralCoins.instance.coinCounts.Add(newPlayer);
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
