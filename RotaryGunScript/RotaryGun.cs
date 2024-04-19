using KeenSoftwareHouse.Library.Extensions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using System;
using System.Collections.Generic;
using System.Dynamic;
using VRageMath;
using VRage.Game.Components;
using VRage;
using Sandbox.Game.Components;
using System.IO;

namespace IngameScript
{
    internal class RotaryGun
    {
        private MyGridProgram program;
        private List<Gun> guns;
        List<Gun> processingGunList;
        public IMyMotorStator rotor;
        private float threshold;
        private float offset;
        private float previousRotorSpeed;

        public RotaryGun(Dictionary<IMyUserControllableGun, float> relativeAngles,  IMyMotorStator rotor, float threshold, float offset, MyGridProgram program, Dictionary<MyDefinitionId, float> knownFireDelays)
        {
            this.program = program;
            guns = new List<Gun>();
            processingGunList = new List<Gun>();
            // create our list of guns
            foreach (var pair in relativeAngles)
            {
                float fireDelay = knownFireDelays.ContainsKey(pair.Key.BlockDefinition) ? knownFireDelays[pair.Key.BlockDefinition] : 0;
                bool requiresCharging = GetHasCapacitorComponent(pair.Key);
                guns.Add(new Gun(pair.Key, pair.Value, requiresCharging, fireDelay, program));
            }

            this.rotor = rotor; 
            this.threshold = threshold;
            this.offset = (float)(offset * (Math.PI / 180)) ; // convert offset from degrees to radians
            while (offset < 0 ) offset += (float)(Math.PI * 2); // normalize offset to be between 0 and 2pi

            previousRotorSpeed = 0;
        }

        private bool GetHasCapacitorComponent(IMyFunctionalBlock block)
        {
            foreach (var component in block.Components)
            {
                if (component.GetType().Name == "MyEntityCapacitorComponent")
                {
                    return true;
                }
            }
            return false;
        }

        public void Update()
        {
            RemoveBadGuns();
            CalculatePreFireAngles();
            SetGunsEnabled();
        }

        private void RemoveBadGuns()
        {
            processingGunList.Clear(); // clear the list that we will be filling
            foreach (var gun in guns)
            {
                if (!gun.actualGun.Closed)
                {
                    processingGunList.Add(gun); // only add the gun to the new list if it is still valid
                }
            }
            List<Gun> tempGunList = guns; // Create a temporary list to hold the reference to the old list
            guns = processingGunList; // set the main list reference to the new list
            processingGunList = tempGunList; // set the processing list reference to the old list
        }

        private void CalculatePreFireAngles()
        {
            float rotorSpeed = rotor.TargetVelocityRad; // get the rotor speed
            
            if (rotorSpeed != previousRotorSpeed) // if the rotor speed has changed, recalculate the start fire angles
            {
                foreach (var gun in guns)
                {
                    gun.CalculatePreFireAngle(rotorSpeed);
                }
                previousRotorSpeed = rotorSpeed;
            }
        }


        private void SetGunsEnabled()
        {
            float rotorAngle = (float)((rotor.Angle + offset) % (Math.PI * 2)); // get the rotor angle and add the offset

            int enabledGuns = 0;
            int rechargingGuns = 0;
            int chargedGuns = 0;
            int prefiringGuns = 0;
            foreach (var gun in guns)
            {

                if (gun.HasFireDelay)
                {
                    // this case handles weapons that take a moment to charge before firing after the fire action is triggered
                    int rotorSign = Math.Sign(rotor.TargetVelocityRad);

                    //float threshold = rotorSign == 1 ? this.threshold : -this.threshold;

                    float fireDelay = gun.fireDelay;

                    float startFireAngle = gun.startFireAngle;
                    float endFireAngle = gun.fireAngle + threshold;
                    if (rotorSign == -1)
                    {
                        endFireAngle = gun.fireAngle;
                        startFireAngle = gun.startFireAngle + threshold;
                    }
                    
                    
                    // all of this to check if the current angle is between the start and end fire angles

                    bool shouldFire = (startFireAngle < endFireAngle && rotorAngle > startFireAngle && rotorAngle < endFireAngle) // normal case
                        || (startFireAngle > endFireAngle && (rotorAngle > startFireAngle || rotorAngle < endFireAngle)); // case where the start and end fire angles wrap around 2pi
                    bool actuallyShouldFire = rotorSign == 1 ? shouldFire : !shouldFire;

                    gun.Enabled(actuallyShouldFire); // And then we parse the result to the gun
                    bool withinThreshold = Math.Abs(rotorAngle - gun.fireAngle) < threshold;
                    enabledGuns += withinThreshold ? 1 : 0;
                    prefiringGuns += shouldFire && !gun.isCharging ? 1 : 0;
                    
                    rechargingGuns += gun.isCharging ? 1 : 0;
                    chargedGuns += !gun.isCharging && !shouldFire && !withinThreshold ? 1 : 0;
                }
                else
                {
                    // this case handles weapons that fire as soon as they are ready

                    bool shouldFire = Math.Abs(rotorAngle - gun.fireAngle) < threshold;
                    gun.Enabled(shouldFire);
                    enabledGuns += shouldFire ? 1 : 0;

                }
            }
            string normalInfo = 
                "    Rotor angle: " + MathHelper.ToDegrees(rotorAngle).ToString("0.00") + 
                "\n    Firing guns: " + enabledGuns;
            string chargedGunInfo = 
                "\n    Prefiring guns: " + prefiringGuns + 
                "\n    Recharging guns: " + rechargingGuns + 
                "\n    Charged(idle) guns: " + chargedGuns;
            string fullInfo = normalInfo + (((rechargingGuns + prefiringGuns + chargedGuns) > 0) ? chargedGunInfo : "");
            program.Echo(fullInfo);
            if (guns.Count == 0)
            {
                program.Echo("    Warning: No guns found in this group!");
            }
        }
    }
}
