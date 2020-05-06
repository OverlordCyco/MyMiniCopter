//#define DEBUG
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System;

namespace Oxide.Plugins
{
    [Info("My Mini Copter", "RFC1920", "0.2.2")]
    // Thanks to BuzZ[PHOQUE], the original author of this plugin
    [Description("Spawn a Mini Helicopter")]
    public class MyMiniCopter : RustPlugin
    {
        string Prefix = "[My MiniCopter] :";
        const string prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

        private bool useCooldown = true;
        private bool copterDecay = false;
        private bool allowWhenBlocked = false;
        private bool killOnSleep = false;
        private bool allowFuelIfUnlimited = false;
        private bool allowDriverDismountWhileFlying = true;
        private bool allowPassengerDismountWhileFlying = true;
        private float stdFuelConsumption = 0.25f;
        private float mindistance = 0f;
        private float gminidistance = 0f;
        private float minDismountHeight = 7f;
        const string MinicopterSpawn = "myminicopter.spawn";
        const string MinicopterFetch = "myminicopter.fetch";
        const string MinicopterWhere = "myminicopter.where";
        const string MinicopterAdmin = "myminicopter.admin";
        const string MinicopterCooldown = "myminicopter.cooldown";
        const string MinicopterUnlimited = "myminicopter.unlimited";

        static LayerMask layerMask = LayerMask.GetMask("Terrain", "World", "Construction");
        double cooldownmin = 60;
        float trigger = 60f;
        private Timer clock;

        public Dictionary<ulong, BaseVehicle > baseplayerminicop = new Dictionary<ulong, BaseVehicle>();
        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        class StoredData
        {
            public Dictionary<ulong, uint> playerminiID = new Dictionary<ulong, uint>();
            public Dictionary<ulong, double> playercounter = new Dictionary<ulong, double>();
            public StoredData()
            {
            }
        }
        private StoredData storedData;
        private bool HasPermission(ConsoleSystem.Arg arg, string permname) => permission.UserHasPermission(arg?.Player().UserIDString, permname) ? true : false;

        #region loadunload
        void Loaded()
        {
            LoadVariables();
            permission.RegisterPermission(MinicopterSpawn, this);
            permission.RegisterPermission(MinicopterFetch, this);
            permission.RegisterPermission(MinicopterWhere, this);
            permission.RegisterPermission(MinicopterAdmin, this);
            permission.RegisterPermission(MinicopterCooldown, this);
            permission.RegisterPermission(MinicopterUnlimited, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        void OnServerInitialized()
        {
            if(((cooldownmin * 60) <= 120) & useCooldown)
            {
                PrintError("Please set a longer cooldown time. Minimum is 2 min.");
                cooldownmin = 2;
                return;
            }
        }

        void Unload()
        {
            SaveData();
            storedData = null;
            baseplayerminicop = null;
        }
        #endregion

        #region MESSAGES
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "You already have a mini helicopter.\nUse command '/nomini' to remove it."},
                {"SpawnedMsg", "Your mini copter has spawned !\nUse command '/nomini' to remove it."},
                {"KilledMsg", "Your mini copter has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"SpawnUsage", "You need to supply a valid SteamId."},
                {"NoFoundMsg", "You do not have an active copter."},
                {"FoundMsg", "Your copter is located at {0}."},
                {"CooldownMsg", "You must wait {0} seconds before spawning a new mini copter."},
                {"DistanceMsg", "You must be within {0} meters of your mini copter."},
                {"BlockedMsg", "You cannot spawn or fetch your copter while building blocked."}
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "Vous avez déjà un mini hélicoptère\nUtilisez la commande '/nomini' pour le supprimer."},
                {"SpawnedMsg", "Votre mini hélico est arrivé !\nUtilisez la commande '/nomini' pour le supprimer."},
                {"KilledMsg", "Votre mini hélico a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"SpawnUsage", "Vous devez fournir un SteamId valide."},
                {"NoFoundMsg", "Vous n'avez pas de mini hélico actif"},
                {"FoundMsg", "Votre mini hélico est situé à {0}."},
                {"CooldownMsg", "Vous devez attendre {0} secondes avant de créer un nouveau mini hélico."},
                {"DistanceMsg", "Vous devez être à moins de {0} mètres de votre mini-hélico."},
                {"BlockedMsg", "Vous ne pouvez pas faire apparaître ou aller chercher votre hélico lorsque la construction est bloquée."}
            }, this, "fr");
        }

        private string _(string msgId, BasePlayer player, params object[] args)
        {
            var msg = lang.GetMessage(msgId, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void PrintMsgL(BasePlayer player, string msgId, params object[] args)
        {
            if(player == null) return;
            PrintMsg(player, _(msgId, player, args));
        }

        private void PrintMsg(BasePlayer player, string msg)
        {
            if(player == null) return;
            SendReply(player, $"{Prefix}{msg}");
        }

        // Chat message to online player with ulong
        private void ChatPlayerOnline(ulong ailldi, string message)
        {
            BasePlayer player = BasePlayer.FindByID(ailldi);
            if(player != null)
            {
                if(message == "killed") PrintMsgL(player, "KilledMsg");
            }
        }
        #endregion

        #region CONFIG
        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            allowWhenBlocked = Convert.ToBoolean(GetConfig("Global", "Allow spawn when building blocked", false));
            stdFuelConsumption = (float) Convert.ToSingle(GetConfig("Global", "Standard fuel consumption per second", 0.25));
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[My MiniCopter] :")); // Chat prefix
            cooldownmin = Convert.ToSingle(GetConfig("Cooldown (on permission)", "Value in minutes", "60"));
            useCooldown = Convert.ToBoolean(GetConfig("Cooldown (on permission)", "Use Cooldown", true));
            copterDecay = Convert.ToBoolean(GetConfig("Allow decay on our minicopters", "Copter Decay", false));
            mindistance = Convert.ToSingle(GetConfig("Minimum Distance for /nomini", "Value in meters", "0"));
            gminidistance = Convert.ToSingle(GetConfig("Minimum Distance for /gmini", "Value in meters", "0"));
            killOnSleep = Convert.ToBoolean(GetConfig("Global", "Destroy copter on player sleep", false));
            allowFuelIfUnlimited = Convert.ToBoolean(GetConfig("Global", "Allow unlimited to use fuel tank", false));
            allowDriverDismountWhileFlying = Convert.ToBoolean(GetConfig("Global", "Allow driver dismount while flying", true));
            allowPassengerDismountWhileFlying = Convert.ToBoolean(GetConfig("Global", "Allow passenger dismount while flying", true));
            minDismountHeight = Convert.ToSingle(GetConfig("Maximum height for dismount", "Value in meters", "7"));

            SaveConfig();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if(data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object cfgvalue;
            if(!data.TryGetValue(datavalue, out cfgvalue))
            {
                cfgvalue = defaultValue;
                data[datavalue] = cfgvalue;
            }
            return cfgvalue;
        }

        void SaveData()
        {
            // Save the data file as we add/remove minicopters.
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }
        #endregion

        #region chatcommands
        // Chat spawn
        [ChatCommand("mymini")]
        private void SpawnMyMinicopterChatCommand(BasePlayer player, string command, string[] args)
        {
            double secondsSinceEpoch = DateTime.UtcNow.Subtract(epoch).TotalSeconds;

            bool isspawner = permission.UserHasPermission(player.UserIDString, MinicopterSpawn);
            if(isspawner == false)
            {
                PrintMsgL(player, "NoPermMsg");
                return;
            }
            if(storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                PrintMsgL(player, "AlreadyMsg");
                return;
            }

            if(player.IsBuildingBlocked() & !allowWhenBlocked)
            {
                PrintMsgL(player, "BlockedMsg");
                return;
            }

            bool hascooldown = permission.UserHasPermission(player.UserIDString, MinicopterCooldown);
            if(!useCooldown) hascooldown = false;

            int secsleft = 0;
            if(hascooldown == true)
            {
                if(storedData.playercounter.ContainsKey(player.userID) == false)
                {
                    storedData.playercounter.Add(player.userID, secondsSinceEpoch);
                    SaveData();
                }
                else
                {
                    double count;
                    storedData.playercounter.TryGetValue(player.userID, out count);

                    if((secondsSinceEpoch - count) > (cooldownmin * 60))
                    {
#if DEBUG
                        Puts($"Player reached cooldown.  Clearing data.");
#endif
                        storedData.playercounter.Remove(player.userID);
                        SaveData();
                    }
                    else
                    {
                        secsleft = Math.Abs((int)((cooldownmin * 60) - (secondsSinceEpoch - count)));

                        if(secsleft > 0)
                        {
#if DEBUG
                            Puts($"Player DID NOT reach cooldown. Still {secsleft.ToString()} secs left.");
#endif
                            PrintMsgL(player, "CooldownMsg", secsleft.ToString());
                            return;
                        }
                    }
                }
            }
            else
            {
                if(storedData.playercounter.ContainsKey(player.userID))
                {
                    storedData.playercounter.Remove(player.userID);
                    SaveData();
                }
            }
            SpawnMyMinicopter(player);
        }

        // Fetch copter
        [ChatCommand("gmini")]
        private void GetMyMiniMyCopterChatCommand(BasePlayer player, string command, string[] args)
        {
            if(player.IsBuildingBlocked() & !allowWhenBlocked)
            {
                PrintMsgL(player, "BlockedMsg");
                return;
            }

            bool canspawn = permission.UserHasPermission(player.UserIDString, MinicopterSpawn);
            bool canfetch = permission.UserHasPermission(player.UserIDString, MinicopterFetch);
            if(!(canspawn & canfetch))
            {
                PrintMsgL(player, "NoPermMsg");
                return;
            }
            if(storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                uint findme;
                storedData.playerminiID.TryGetValue(player.userID, out findme);
                var foundit = BaseNetworkable.serverEntities.Find(findme);
                if(foundit != null)
                {
                    // Distance check
                    if(gminidistance > 0f)
                    {
                        if(Vector3.Distance(player.transform.position, foundit.transform.position) > gminidistance)
                        {
                            PrintMsgL(player, "DistanceMsg", gminidistance);
                            return;
                        }
                    }

                    // Check for and dismount all players before moving the copter
                    var copter = foundit as BaseVehicle;
                    BaseVehicle.MountPointInfo[] mountpoints = copter.mountPoints;
                    for(int i = 0; i < (int)mountpoints.Length; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountpoints[i];
                        if(mountPointInfo.mountable != null)
                        {
                            BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                            if(mounted)
                            {
                                Vector3 player_pos = mounted.transform.position + new Vector3(1,0,1);
                                mounted.DismountObject();
                                mounted.MovePosition(player_pos);
                                mounted.SendNetworkUpdateImmediate(false);
                                mounted.ClientRPCPlayer(null, player, "ForcePositionTo", player_pos);
                                mountPointInfo.mountable._mounted = null;
                            }
                        }
                    }
                    var newLoc = new Vector3((float)(player.transform.position.x + 2f), player.transform.position.y + 2f, (float)(player.transform.position.z + 2f));
                    foundit.transform.position = newLoc;
                    PrintMsgL(player, "FoundMsg", newLoc);
                }
                return;
            }
            else
            {
                PrintMsgL(player, "NoFoundMsg");
                return;
            }
        }

        // Find copter
        [ChatCommand("wmini")]
        private void WhereisMyMiniMyCopterChatCommand(BasePlayer player, string command, string[] args)
        {
            bool canspawn = permission.UserHasPermission(player.UserIDString, MinicopterWhere);
            if(canspawn == false)
            {
                PrintMsgL(player, "NoPermMsg");
                return;
            }
            if(storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                uint findme;
                if(storedData.playerminiID.TryGetValue(player.userID, out findme))
                {
                    var foundit = BaseNetworkable.serverEntities.Find(findme);
                    var loc = foundit.transform.position.ToString();
                    PrintMsgL(player, "FoundMsg", loc);
                }
                return;
            }
            else
            {
                PrintMsgL(player, "NoFoundMsg");
                return;
            }
        }

        // Chat despawn
        [ChatCommand("nomini")]
        private void KillMyMinicopterChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, MinicopterSpawn);
            if(isspawner == false)
            {
                PrintMsgL(player, "NoPermMsg");
                return;
            }
            KillMyMinicopterPlease(player);
        }
        #endregion

        #region consolecommands
        // Console spawn
        [ConsoleCommand("spawnminicopter")]
        private void SpawnMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)
        {
            if(arg.IsRcon)
            {
                if(arg.Args == null)
                {
                    Puts("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if(!HasPermission(arg, MinicopterAdmin))
            {
                SendReply(arg, _("NoPermMsg", arg.Connection.player as BasePlayer));
                return;
            }
            else if(arg.Args == null)
            {
                SendReply(arg, _("SpawnUsage", arg.Connection.player as BasePlayer));
                return;
            }

            if(arg.Args.Length == 1)
            {
                ulong steamid;
                if (ulong.TryParse(arg.Args[0], out steamid))
                {
                    BasePlayer player = BasePlayer.FindByID(steamid);
                    if(player != null)
                    {
                        SpawnMyMinicopter(player);
                    }
                }
            }
        }

        // Console despawn
        [ConsoleCommand("killminicopter")]
        private void KillMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)
        {
            if(arg.IsRcon)
            {
                if(arg.Args == null)
                {
                    Puts("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if(!HasPermission(arg, MinicopterAdmin))
            {
                SendReply(arg, _("NoPermMsg", arg.Connection.player as BasePlayer));
                return;
            }
            else if(arg.Args == null)
            {
                SendReply(arg, _("SpawnUsage", arg.Connection.player as BasePlayer));
                return;
            }

            if(arg.Args.Length == 1)
            {
                ulong steamid = Convert.ToUInt64(arg.Args[0]);
                if(steamid.IsSteamId() == false) return;
                BasePlayer player = BasePlayer.FindByID(steamid);
                if(player != null)
                {
                    KillMyMinicopterPlease(player);
                }
            }
        }
        #endregion

        #region ourhooks
        // Spawn hook
        private void SpawnMyMinicopter(BasePlayer player)
        {
            if(player.IsBuildingBlocked() & !allowWhenBlocked)
            {
                PrintMsgL(player, "BlockedMsg");
                return;
            }

            Quaternion rotation = player.GetNetworkRotation();
            Vector3 forward = rotation * Vector3.forward;
            // Make straight perpendicular to up axis so we don't spawn into ground or above player's head.
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 position = player.transform.position + straight * 5f;
            position.y = player.transform.position.y + 2.5f;

            if(position == null) return;
            BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if(vehicleMini == null) return;
            BaseEntity miniEntity = vehicleMini as BaseEntity;
            miniEntity.OwnerID = player.userID;

            MiniCopter miniCopter = vehicleMini as MiniCopter;
            vehicleMini.Spawn();

            if(permission.UserHasPermission(player.UserIDString, MinicopterUnlimited))
            {
                // Set fuel requirements to 0
                miniCopter.fuelPerSec = 0f;
                if(!allowFuelIfUnlimited)
                {
                    // If the player is not allowed to use the fuel container, add 1 fuel so the copter will start.
                    // Also lock fuel container since there is no point in adding/removing fuel
                    StorageContainer fuelCan = miniCopter.fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                    ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelCan.inventory);
                    fuelCan.SetFlag(BaseEntity.Flags.Locked, true);
                }
            }
            else
            {
                miniCopter.fuelPerSec = stdFuelConsumption;
            }

            PrintMsgL(player, "SpawnedMsg");
            uint minicopteruint = vehicleMini.net.ID;
#if DEBUG
            Puts($"SPAWNED MINICOPTER {minicopteruint.ToString()} for player {player.displayName} OWNER {miniEntity.OwnerID}");
#endif
            storedData.playerminiID.Remove(player.userID);
            storedData.playerminiID.Add(player.userID,minicopteruint);
            SaveData();
            baseplayerminicop.Remove(player.userID);
            baseplayerminicop.Add(player.userID, vehicleMini);

            miniEntity = null;
            miniCopter = null;
        }

        // Kill minicopter hook
        private void KillMyMinicopterPlease(BasePlayer player)
        {
            bool foundcopter = false;
            if(mindistance == 0f)
            {
                foundcopter = true;
            }
            else
            {
                List<BaseEntity> copterlist = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(player.transform.position, mindistance, copterlist);

                foreach(BaseEntity p in copterlist)
                {
                    var foundent = p.GetComponentInParent<MiniCopter>() ?? null;
                    if(foundent != null)
                    {
                        foundcopter = true;
                    }
                }
            }

            if(storedData.playerminiID.ContainsKey(player.userID) == true && foundcopter)
            {
                uint deluint;
                if(storedData.playerminiID.TryGetValue(player.userID, out deluint))
                {
                    var tokill = BaseNetworkable.serverEntities.Find(deluint);
                    tokill.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                storedData.playerminiID.Remove(player.userID);
                baseplayerminicop.Remove(player.userID);

                if(storedData.playercounter.ContainsKey(player.userID) & !useCooldown)
                {
                    storedData.playercounter.Remove(player.userID);
                }
                SaveData();
            }
            else if(foundcopter == false)
            {
#if DEBUG
                Puts($"Player too far from copter to destroy.");
#endif
                PrintMsgL(player, "DistanceMsg", mindistance);
            }
        }
        #endregion

        #region hooks
        // On kill - tell owner
        void OnEntityKill(BaseNetworkable entity)
        {
            if(entity == null) return;
            MiniCopter check = entity as MiniCopter;
            if(check == null) return;
            if(storedData.playerminiID == null) return;
            ulong todelete = 0;
            if(storedData.playerminiID.ContainsValue(entity.net.ID) == false)
            {
#if DEBUG
                Puts($"KILLED non-plugin minicopter");
#endif
                return;
            }
            foreach(var item in storedData.playerminiID)
            {
                if(item.Value == entity.net.ID)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    BasePlayer player = BasePlayer.FindByID(item.Key);
                    if(player != null) baseplayerminicop.Remove(player.userID);
                    todelete = item.Key;
                }
            }
            if(todelete > 0)
            {
                storedData.playerminiID.Remove(todelete);
                SaveData();
            }
        }

        // Disable decay for our copters if so configured
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if(entity == null || hitInfo == null) return;
            if(!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;
            if(storedData.playerminiID == null) return;

            if(storedData.playerminiID.ContainsValue(entity.net.ID))
            {
                if(copterDecay)
                {
#if DEBUG
                    Puts($"Enabling standard decay for spawned minicopter {entity.net.ID.ToString()}.");
#endif
                }
                else
                {
#if DEBUG
                    Puts($"Disabling decay for spawned minicopter {entity.net.ID.ToString()}.");
#endif
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
                }
                return;
            }
            return;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if(!killOnSleep) return;
            if(player == null) return;

            if(storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                uint deluint;
                if (storedData.playerminiID.TryGetValue(player.userID, out deluint))
                {
                    BaseNetworkable foundit = BaseNetworkable.serverEntities.Find(deluint);
                    if (foundit == null) return; // Didn't find it
                }

                // Check for mounted players
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(deluint);
                BaseVehicle copter = tokill as BaseVehicle;
                BaseVehicle.MountPointInfo[] mountpoints = copter.mountPoints;
                for(int i = 0; i < (int)mountpoints.Length; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = mountpoints[i];
                    if(mountPointInfo.mountable != null)
                    {
                        BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                        if(mounted)
                        {
#if DEBUG
                            Puts("Copter owner sleeping but another one is mounted - cannot destroy copter");
#endif
                            return;
                        }
                    }
                }
#if DEBUG
                Puts("Copter owner sleeping - destroying copter");
#endif
                tokill.Kill();
                storedData.playerminiID.Remove(player.userID);
                baseplayerminicop.Remove(player.userID);

                if(storedData.playercounter.ContainsKey(player.userID) & !useCooldown)
                {
                    storedData.playercounter.Remove(player.userID);
                }
                SaveData();
            }
        }

        object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if(player == null) return null;
            if(!Physics.Raycast(new Ray(entity.transform.position, Vector3.down), minDismountHeight, layerMask))
            {
                var copterent = entity.GetComponentInParent<MiniCopter>() ?? null;

                foreach(var item in storedData.playerminiID)
                {
#if DEBUG
                    Puts($"Seat {item.Value} comparing to copterid {copterent.net.ID}");
#endif
                    if(item.Value == copterent.net.ID && !allowDriverDismountWhileFlying)
                    {
#if DEBUG
                        Puts("Deny pilot dismount");
#endif
                        return false;
                    }
                    else if(item.Value -1 == copterent.net.ID && !allowPassengerDismountWhileFlying)
                    {
#if DEBUG
                        Puts("Deny passenger dismount");
#endif
                        return false;
                    }
                }
            }
            return null;
        }
        #endregion
    }
}
