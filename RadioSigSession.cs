using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;

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
                        ["GetAllBroadcasters"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<string>, int>(GetAllBroadcasters)
                    };					
					
					var pb = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, Sandbox.ModAPI.IMyTerminalBlock>("RadioPbAPI");
					pb.Getter = b => PbApiMethods;
					MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
					PbApiInited = true;					
				}

                _lastRunTicks = DateTime.Now.Ticks;
            }
        }

        protected int GetAllBroadcasters(Sandbox.ModAPI.Ingame.IMyTerminalBlock antenna, ICollection<string> output)
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
            if (antenna is IMyLaserAntenna)
                mutual = true;

            GetAllRelayedBroadcasters(receiver, identityId, mutual, radioBroadcasters);

            MyLog.Default.WriteLine($"TIN.RadioSigSession: Found {radioBroadcasters.Count} broadcasters");

            foreach (var broadcaster in radioBroadcasters)
            {
                var grid = broadcaster.Entity.GetTopMostParent(null) as MyCubeGrid;
                string name = grid.DisplayName;
                
                output.Add(name);
                //if (broadcaster.Entity is IMyCubeGrid)
                //{
                //    var grid = broadcaster.Entity as IMyCubeGrid;
                //    var gridName = grid.DisplayName;
                //    if (gridName == null)
                //        gridName = grid.Name;

                //    output.Add(gridName);
                //}
            }

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
