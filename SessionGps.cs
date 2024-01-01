using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using MyGpsAlias = VRage.MyTuple<int, string, string, VRageMath.Vector3D, bool, VRageMath.Color>;


namespace RadioSigs
{
    partial class RadioSigSession : MySessionComponentBase
    {

        //TODO: IMyGps isn't in the Sandbox.ModAPI.Ingame namespace so presumably it isn't available to PB scripts.  Need to find a workaround.
        protected ICollection<MyGpsAlias> GetGpsList(IMyTerminalBlock owner)
        {
            var playerIdentityId = GetPlayerIdentityFromGrid(owner);
            //MyLog.Default.WriteLine($"TIN.RadioSigSession.GetGpsList: PlayerIdentityId: {playerIdentityId}");
            //IMyGps
            // Hash:int
            // Name:string
            // Description:string
            // Coords:Vector3D
            // GPSColor:Color
            // ShowOnHud:bool
            // DiscardAt:timespan?
            // ContainerRemainingTime:string

            //Convert IMyGps to a MyTuple because IMyGps isn't available to PB scripts.
            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(playerIdentityId);
            //MyLog.Default.WriteLine($"TIN.RadioSigSession.GetGpsList: gpsList.Count: {gpsList.Count}");

            var result = gpsList.Select(ConvertToMyGpsAlias).ToList();

            return result;
        }

        protected MyGpsAlias CreateGps(IMyTerminalBlock owner, string name, string description, Vector3D coords, Color color, bool showOnHud = true, bool temporary = false)
        {   
            var gps = MyAPIGateway.Session.GPS.Create(name, description, coords, showOnHud, temporary);
            gps.GPSColor = color;

            var playerIdentityId = GetPlayerIdentityFromGrid(owner);
            MyAPIGateway.Session.GPS.AddGps(playerIdentityId, gps);

            return ConvertToMyGpsAlias(gps);
        }

        protected MyGpsAlias UpdateGps(IMyTerminalBlock owner, int hash, string name, string description, Vector3D coords, Color color, bool showOnHud = true, bool temporary = false)
        {
            var playerIdentityId = GetPlayerIdentityFromGrid(owner);

            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(playerIdentityId);

            var gps = gpsList.FirstOrDefault(g => g.Hash == hash);
            if(gps == null)
            {
                //unable to find the gps to update
                //TODO: create a new gps instead?
                //CreateGps(owner, name, description, coords, showOnHud, temporary);
                throw new KeyNotFoundException($"Unable to find GPS with hash {hash}");
            }

            gps.Name = name;
            gps.Description = description;
            gps.Coords = coords;
            gps.ShowOnHud = showOnHud;
            gps.GPSColor = color;
            gps.DiscardAt = temporary ? TimeSpan.FromSeconds(180) : (TimeSpan?)null;

            MyAPIGateway.Session.GPS.ModifyGps(playerIdentityId, gps);

            return ConvertToMyGpsAlias(gps);
        }

        protected bool DeleteGps(IMyTerminalBlock owner, int hash)
        {
            var playerIdentityId = GetPlayerIdentityFromGrid(owner);

            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(playerIdentityId);

            var gps = gpsList.FirstOrDefault(g => g.Hash == hash);
            if (gps == null)
            {
                //unable to find the gps to update
                //TODO: create a new gps instead?
                //CreateGps(owner, name, description, coords, showOnHud, temporary);
                return false;
            }

            MyAPIGateway.Session.GPS.RemoveGps(playerIdentityId, gps);
            return true;
        }

        protected static MyGpsAlias ConvertToMyGpsAlias(IMyGps gps)
        {
            return new MyGpsAlias(gps.Hash, gps.Name, gps.Description, gps.Coords, gps.ShowOnHud, gps.GPSColor);
        }

        protected static long GetPlayerIdentityFromGrid(VRage.Game.ModAPI.Ingame.IMyEntity entity)
        {
            if (entity is IMyCubeBlock)
            {
                var cubeGrid = (entity as IMyCubeBlock).CubeGrid;
                if (cubeGrid != null) return (cubeGrid.BigOwners.Count > 0) ? cubeGrid.BigOwners[0] : 0;
            }

            if (entity is VRage.Game.ModAPI.Interfaces.IMyControllableEntity)
            {
                var myControllableEntity = entity as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
                var controllerInfo = myControllableEntity.ControllerInfo;
                if (controllerInfo != null)
                {
                    return controllerInfo.ControllingIdentityId;
                }
            }

            return 0L;
        }
    }
}
