using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.AI;
using VRage.Utils;
using VRageMath;

using MyGpsAlias = VRage.MyTuple<int, string, string, VRageMath.Vector3D, bool, VRageMath.Color>;

namespace RadioSigs
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    partial class RadioSigSession : MySessionComponentBase
    {
        private long _lastRunTicks = 0L;
        private int _msBetweenUpdates = 1000;

		private bool PbApiInited = false;

        public override void BeforeStart()
        {
            //MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(int.MinValue, AfterDamageHandler);
        }

        public override void LoadData()
        {
            MyLog.Default.WriteLine("TIN.RadioSigSession: loading v1");
            if (!MyAPIGateway.Session.IsServer)
            {
                MyLog.Default.WriteLine("TIN.RadioSigSession: Canceling load, not a server.");
                return;
            }

            MyLog.Default.WriteLine("TIN.RadioSigSession: loaded...");
        }

        public override void UpdateAfterSimulation()
        {
            if( (_lastRunTicks + _msBetweenUpdates * TimeSpan.TicksPerMillisecond) < DateTime.Now.Ticks)
            { 
				if (!PbApiInited) 
				{
					var PbApiMethods = new Dictionary<string, Delegate>
					{
                        ["GetAllBroadcasters"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<MyDetectedEntityInfo>, int>(GetAllBroadcasters),
                        ["GetGpsList"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<MyGpsAlias>>(GetGpsList),
                        ["CreateGps"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, string, string, Vector3D, Color, bool, bool, MyGpsAlias>(CreateGps),
                        ["UpdateGps"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string, string, Vector3D, Color, bool, bool, MyGpsAlias>(UpdateGps),
                        ["DeleteGps"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, bool>(DeleteGps)
                    };					
					
					var pb = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, Sandbox.ModAPI.IMyTerminalBlock>("RadioPbAPI");
					pb.Getter = b => PbApiMethods;
					MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
					PbApiInited = true;					
				}

                _lastRunTicks = DateTime.Now.Ticks;
            }
        }

        protected int GetAllBroadcasters(Sandbox.ModAPI.Ingame.IMyTerminalBlock antenna, ICollection<MyDetectedEntityInfo> output)
        {
            if (antenna == null)
            {
                MyLog.Default.WriteLine("TIN.RadioSigSession: not valid antenna");
                return -1;
            }

            var receiver = antenna.Components.Get<MyDataReceiver>();
            if (receiver == null)
            {
                MyLog.Default.WriteLine("TIN.RadioSigSession: no valid receiver");
                return -2; 
            }

            MyLog.Default.WriteLine("TIN.RadioSigSession: Found a valid receiver");

            var identityId = antenna.OwnerId;
            if (identityId == 0)
            {
                MyLog.Default.WriteLine("TIN.RadioSigSession: Found a valid identity");
                return -3;
            }

            var mutual = false;
            if (antenna is Sandbox.ModAPI.Ingame.IMyLaserAntenna)
                mutual = true;

            GetAllRelayedBroadcasters(receiver, identityId, mutual, radioBroadcasters);

            MyLog.Default.WriteLine($"TIN.RadioSigSession: Found {radioBroadcasters.Count} broadcasters");

            foreach (var broadcaster in radioBroadcasters)
            {
                if (broadcaster == null || broadcaster.Entity == null)
                {
                    MyLog.Default.WriteLine("TIN.RadioSigSession: Null broadcaster or broadcaster.Entity found");
                    continue;
                }

                string name = "unknown";
                MyDetectedEntityType gridType = MyDetectedEntityType.Unknown;

                if (broadcaster.Entity is IMyCharacter)
                {
                    MyLog.Default.WriteLine($"TIN.RadioSigSession: Broadcaster is a character, entity ID {broadcaster.Entity.EntityId}");
                    name = (broadcaster.Entity as IMyCharacter).DisplayName;
                    gridType = MyDetectedEntityType.CharacterHuman;
                }
                else {
                    var grid = broadcaster.Entity.GetTopMostParent(null) as IMyCubeGrid;
                    if (grid == null)
                    {
                        if (broadcaster.Entity is IMyFloatingObject)
                        {
                            MyLog.Default.WriteLine($"TIN.RadioSigSession: Broadcaster is a floating object, entity ID {broadcaster.Entity.EntityId}");
                        }
                        else
                        {
                            MyLog.Default.WriteLine($"TIN.RadioSigSession: Broadcaster is an unknown type '{broadcaster.Entity.GetType()}', entity ID {broadcaster.Entity.EntityId}");
                        }
                        continue;
                    }

                    name = grid.DisplayName;
                    gridType = grid.GridSizeEnum == VRage.Game.MyCubeSize.Large ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid;
                }

                if (broadcaster.Entity.PositionComp == null)
                {
                    MyLog.Default.WriteLine($"TIN.RadioSigSession: PositionComp is null for broadcaster entity ID {broadcaster.Entity.EntityId}");
                    continue;
                }

                // Below this point, grid and PositionComp are not null.
                var entityId = broadcaster.Entity.EntityId;
                var hitPosition = broadcaster.Entity.PositionComp.GetPosition();
                var orientation = broadcaster.Entity.WorldMatrix.GetOrientation();
                var velocity = broadcaster.Entity.Physics?.LinearVelocity ?? Vector3.Zero;
                var boundingBox = broadcaster.Entity.PositionComp.WorldAABB;

                MyRelationsBetweenPlayerAndBlock relationship;
                try
                {
                    relationship = broadcaster.CanBeUsedByPlayer(identityId) ?
                        VRage.Game.MyRelationsBetweenPlayerAndBlock.Owner :
                        VRage.Game.MyRelationsBetweenPlayerAndBlock.NoOwnership;
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine($"TIN.RadioSigSession: Exception in CanBeUsedByPlayer for broadcaster entity ID {broadcaster.Entity.EntityId}: {ex.Message}");
                    continue;
                }

                var info = new MyDetectedEntityInfo(entityId, name, gridType, hitPosition, orientation, velocity, relationship, boundingBox, DateTime.Now.Ticks);
                output.Add(info);
            }

            MyLog.Default.WriteLine($"TIN.RadioSigSession: Processed {output.Count} broadcasters successfully");


            radioBroadcasters.Clear();

            return output.Count;
        }

        protected override void UnloadData()
        {
            
        }

		private HashSet<MyDataBroadcaster> radioBroadcasters = new HashSet<MyDataBroadcaster>();
        private void GetAllRelayedBroadcasters(MyDataReceiver receiver, long identityId, bool mutual, HashSet<MyDataBroadcaster> output = null)
        {
            if(output == null)
            {
                output = radioBroadcasters;
                output.Clear();
            }

            foreach(MyDataBroadcaster current in receiver.BroadcastersInRange)
            {
                if(!output.Contains(current) && !current.Closed && (!mutual || (current.Receiver != null && receiver.Broadcaster != null && current.Receiver.BroadcastersInRange.Contains(receiver.Broadcaster))))
                {
                    output.Add(current);

                    if(current.Receiver != null && current.CanBeUsedByPlayer(identityId))
                    {
                        GetAllRelayedBroadcasters(current.Receiver, identityId, mutual, output);
                    }
                }
            }
        }
    }
}
