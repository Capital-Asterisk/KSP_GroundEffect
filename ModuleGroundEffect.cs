using System;

using UnityEngine;

namespace KSP_GroundEffect
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ModuleGroundEffect : PartModule 
	{ 
		ModuleLiftingSurface thingThatLiftsParts;
		float initialLift;

		float wingSpan = 4.5f; // strange assumption m8

		public override void OnStart(StartState state) {
			// Look through the list of part modules to find anything that inherits ModuleLiftingSurface
			foreach (PartModule module in part.Modules) {
				if (module is ModuleLiftingSurface) {
					thingThatLiftsParts = (ModuleLiftingSurface)(module);
					initialLift = thingThatLiftsParts.deflectionLiftCoeff;
					print("Ground effect module loaded");
					return;
				}
			}
			print("Ground effect module loaded for non-aero part?");

		}

		public override void OnUpdate() {
			
			if (!(vessel.situation == Vessel.Situations.FLYING) || !vessel.mainBody.hasSolidSurface || !vessel.mainBody.atmosphere) {
				return;
			}

			thingThatLiftsParts.deflectionLiftCoeff = initialLift;

			float groundDistance = 0;

			// Distance from ocean,
			if (FlightGlobals.currentMainBody.ocean) {
				groundDistance = Math.Max(FlightGlobals.getAltitudeAtPos (part.transform.position), 0);
			}

			// Test for terrain
			RaycastHit ray;
			if (Physics.Raycast (part.transform.position, vessel.gravityForPos.normalized, out ray, wingSpan * 2, 1 << 15)) {
				// Close to ground, override groundDistance if close to terrain, buildings, or anything
				groundDistance = (groundDistance == 0) ? ray.distance : Math.Min(groundDistance, ray.distance);
			}

			if (groundDistance != 0) {
				thingThatLiftsParts.deflectionLiftCoeff *= 2.0f / (float)Math.Pow (0.3f * groundDistance + 1.0f, 2) + 1.0f;
			}

			//print ("Lift " + thingThatLiftsParts.deflectionLiftCoeff);
		}

	}

}

