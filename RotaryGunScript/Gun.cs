using KeenSoftwareHouse.Library.Extensions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using System;
using System.Collections.Generic;
using System.Dynamic;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.Game.Components;

namespace IngameScript
{
   
    internal class Gun
    {
        static readonly MyDefinitionId ElectricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        static readonly float IdlePowerDraw = 0.0020001f;

        
        private bool enableCheck;
        private bool requiresCharging;

        public IMyUserControllableGun actualGun;
        public float fireAngle;
        public bool HasFireDelay;
        public float fireDelay;
        public float startFireAngle;
        public bool isCharging;

        private MyGridProgram program;
        public Gun(IMyUserControllableGun actualGun, float fireAngle, bool requiresCharging, float fireDelay, MyGridProgram program)
        {
            this.actualGun = actualGun;
            this.fireAngle = fireAngle;
            enableCheck = true;
            this.requiresCharging = requiresCharging;

            this.fireDelay = fireDelay + 1/60; // 1 frame lenience
            HasFireDelay = fireDelay > 0;
            startFireAngle = 0;
            this.program = program;
            isCharging = false;
        }

        public void CalculatePreFireAngle(float rotorSpeed)
        {
            if (!HasFireDelay) return;
            startFireAngle = fireAngle - (rotorSpeed * fireDelay);
            if (startFireAngle < 0)
            {
                startFireAngle += (float)(Math.PI * 2); // Backwards modulo?
            }
        }
        public void Enabled(bool enabled)
        {
            if (requiresCharging && !enabled)
            {
                isCharging = actualGun.Components.Get<MyResourceSinkComponent>().MaxRequiredInputByType(ElectricityId) > IdlePowerDraw;
                SetState(isCharging);
            }
            else
            {
                SetState(enabled);
            }

            
        }

        private void SetState(bool enabled)
        {
            if (enableCheck != enabled)
            {
                actualGun.Enabled = enabled;
            }
            enableCheck = enabled;
        }
    }
}
