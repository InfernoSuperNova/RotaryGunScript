
using KeenSoftwareHouse.Library.Extensions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using System;
using System.Collections.Generic;
using System.Dynamic;
using VRageMath;
using VRage.Game.ModAPI.Ingame.Utilities;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        float threshold = 0.05f; // threshold between 0 and 1, 1 will pretty much never fire, 0 will always fire
        string groupName = "RotaryGuns";
        Dictionary<MyDefinitionId, float> knownFireDelays = new Dictionary<MyDefinitionId, float>
        {
            [MyDefinitionId.Parse("SmallMissileLauncherReload/SmallRailgun")] = 0.5f,
            [MyDefinitionId.Parse("SmallMissileLauncherReload/LargeRailgun")] = 2.0f,
        };

        List<RotaryGun> RotaryGunList;
        MyIni _ini = new MyIni();
        public Program()
        {
            SyncConfig();
            IMyBlockGroup Guns = GridTerminalSystem.GetBlockGroupWithName(groupName);

            if (Guns == null)
            {
                Echo("No group found with name: " + groupName + ". Create a group and recompile.");
                return;
            }
            var guns = new List<IMyUserControllableGun>();


            var rotors = new List<IMyMotorStator>();
            Guns.GetBlocksOfType(guns);
            Guns.GetBlocksOfType(rotors);

            if (rotors.Count == 0)
            {
                Echo("No rotors found in group! Add some rotors and recompile.");
                return;
            }
            if (guns.Count == 0)
            {
                Echo("No guns found in group! Add some guns and recompile.");
                return;
            }
            var gunGroups = new Dictionary<IMyMotorStator, List<IMyUserControllableGun>>();

            foreach (IMyMotorStator rotor in rotors)
            {
                gunGroups[rotor] = new List<IMyUserControllableGun>();
            }
            foreach (IMyUserControllableGun gun in guns)
            {
                IMyMotorStator rotor = FindMatchingRotorTop(rotors, gun);
                if (rotor != null)
                {
                    gunGroups[rotor].Add(gun);
                }
            }

            RotaryGunList = new List<RotaryGun>();
            foreach (var pair in gunGroups)
            {
                IMyMotorStator rotor = pair.Key;
                List<IMyUserControllableGun> gunList = pair.Value;
                CreateRotaryGun(gunList, rotor, ref RotaryGunList);
            }
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        private void SyncConfig()
        {
            // Grab text from custom data
            _ini.TryParse(Me.CustomData);

            // Get the threshold and group name
            threshold = _ini.Get("RotaryGunGeneralConfig", "Threshold").ToSingle(threshold);
            groupName = _ini.Get("RotaryGunGeneralConfig", "GroupName").ToString(groupName);

            // Set the threshold, group name, and comment
            _ini.Set("RotaryGunGeneralConfig", "Threshold", threshold);
            _ini.Set("RotaryGunGeneralConfig", "GroupName", groupName);
            _ini.SetSectionComment("RotaryGunGeneralConfig", 
                "\n\nGeneral configuration for the rotary gun script. See the Custom Data\n" +
                "of your rotors to edit the offsets.\n\n" +
                "Threshold: The value between the absolute fire angle and the current angle,\n" +
                "increase if weapons aren't firing, decrease if weapons are firing too broadly.\n\n" +
                "GroupName: Name of the group in the ship terminal containing the blocks of\n" +
                "every Rotary Gun.\n\nEDIT HERE:");
            
            // Create a list of knownFireDelayKeys
            var knownFireDelayKeys = new List<MyIniKey>();

            if (_ini.ContainsSection("RotaryGunKnownFireDelays"))
            {
                _ini.GetKeys("RotaryGunKnownFireDelays", knownFireDelayKeys);

                foreach (var key in knownFireDelayKeys)
                {
                    MyDefinitionId id;
                    bool result = MyDefinitionId.TryParse(key.Name, out id);
                    if (!result)
                    {
                        continue;
                    }
                    knownFireDelays[id] = _ini.Get("RotaryGunKnownFireDelays", key.Name).ToSingle(0);
                }
            }
            knownFireDelayKeys.Clear();
            
            foreach (var pair in knownFireDelays)
            {
                _ini.Set("RotaryGunKnownFireDelays", ConvertDefinitionIdToString(pair.Key), pair.Value);
            }
            _ini.SetSectionComment("RotaryGunKnownFireDelays", "\n\n\nKnown fire delays for existing weapons (by default it is just the railguns.\nIf mods change these (or on the off chance keen changes them), you will\nneed to change them to match. You can also add values for modded\nweapons.\n\nEDIT HERE:");
            Me.CustomData = _ini.ToString();
        }

        private string ConvertDefinitionIdToString(MyDefinitionId id)
        {
            return id.ToString().Substring("MyObjectBuilder_".Length);
        }

        private void CreateRotaryGun(List<IMyUserControllableGun> guns, IMyMotorStator rotor, ref List<RotaryGun> rotaryGunList)
        {
            var relativeAngles = new Dictionary<IMyUserControllableGun, float>();

            foreach (IMyUserControllableGun gun in guns)
            {
                Vector3D worldDirection = gun.GetPosition() - rotor.Top.GetPosition();
                Vector3D angleVector = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(rotor.Top.WorldMatrix)); // Note that we transpose to go from world -> body
                angleVector.Y = 0;
                Vector3D angleVectorNormalized = Vector3D.Normalize(angleVector);
                double angle = Math.Atan2(angleVector.X, angleVector.Z) + Math.PI;
                relativeAngles[gun] = (float)angle;
            }
            _ini.Clear();
            _ini.TryParse(rotor.CustomData);
            int offset = _ini.Get("RotaryGun", "Offset").ToInt32();
            _ini.Set("RotaryGun", "Offset", offset);
            _ini.SetComment("rotaryGun", "Offset", "Offset, in degrees, accepts any number and will map to a reasonable range.");
            rotor.CustomData = _ini.ToString();
            var rotaryGun = new RotaryGun(relativeAngles, rotor, threshold, offset, this, knownFireDelays);
            rotaryGunList.Add(rotaryGun);
        }

        private IMyMotorStator FindMatchingRotorTop(List<IMyMotorStator> rotors, IMyFunctionalBlock block)
        {
            foreach (var rotor in rotors)
            {
                if (block.CubeGrid == rotor.Top.CubeGrid)
                {
                    return rotor;
                }
            }
            return null;
        }

        // Called every time the program is run
        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update1) != 0)
            {
                ContinuousUpdate();
            }
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
            {
                RunArgument(argument);
            }
        }

        // Called every frame
        public void ContinuousUpdate()
        {
            Echo("Edit script variables in the Custom Data of this Programmable Block, and recompile.\n");
            foreach (RotaryGun rotaryGun in RotaryGunList)
            {
                Echo("Rotary Gun running on rotor: " + rotaryGun.rotor.CustomName + "\n{");
                rotaryGun.Update();
                Echo("}");
            }
        }

        // Called when arguments are run
        public void RunArgument(string arg)
        {
        }
    }
}


