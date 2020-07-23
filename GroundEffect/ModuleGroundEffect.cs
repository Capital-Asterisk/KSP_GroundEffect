using System;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{

    // I don't know c#

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ModuleGroundEffect : PartModule
    {
        [KSPField()]
        public float groundEffectMultiplier = 1.0f;

        public override void OnStart(StartState state)
        {

        }

        public override void OnUpdate()
        {
            //print("*notices " + groundEffectMultiplier + "* OwO what's this?");
        }

    }
}