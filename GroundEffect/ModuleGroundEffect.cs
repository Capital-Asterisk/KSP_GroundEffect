using System;

using UnityEngine;

namespace KSP_GroundEffect
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ModuleGroundEffect : PartModule 
	{ 

		const float ActivateAltitude = 400;

		ModuleLiftingSurface thingThatLiftsParts;
		ModuleControlSurface thingThatAlsoLiftsPartsButMoves;
		float initialLift;
		float initialLiftCtrl;
		float wingSpan = 4.5f; // strange assumption m8

		public override void OnStart(StartState state) {
			// Look through the list of part modules to find anything that inherits ModuleLiftingSurface
			foreach (PartModule module in part.Modules) {
				if (typeof(ModuleLiftingSurface).IsAssignableFrom(module.GetType())) {
					thingThatLiftsParts = (ModuleLiftingSurface)(module);
					initialLift = thingThatLiftsParts.deflectionLiftCoeff;
					if (module is ModuleControlSurface) {
						//print ("SURFAAAACE!");
						thingThatAlsoLiftsPartsButMoves = (ModuleControlSurface)(module);
						initialLiftCtrl = thingThatAlsoLiftsPartsButMoves.ctrlSurfaceArea;
					}
					print ("Ground effect module loaded");
					return;
				}
			}
			// This means there's a module manager error?
			print("Ground effect module loaded for non-aero part?");
		}

		public override void OnUpdate() {

			float multiplier = 0;

			// Checks to see if ground effect would have any significance
			if (thingThatLiftsParts == null
			    || ((vessel.situation & (Vessel.Situations.FLYING | Vessel.Situations.LANDED | Vessel.Situations.SPLASHED)) == 0)
			    || (vessel.radarAltitude > ActivateAltitude)
			    || !vessel.mainBody.hasSolidSurface
			    || !vessel.mainBody.atmosphere) {
				// End immediately if it doesnt
				//return;

				// nevermind, just set multiplier to 1
				// so that coefficients are set back to default values
				multiplier = 1.0f;
			} else {
				
				float groundDistance = 0;
				Vector3 surfaceNormal = vessel.gravityForPos.normalized;

				// Distance from ocean,
				if (FlightGlobals.currentMainBody.ocean) {
					groundDistance = Math.Max(FlightGlobals.getAltitudeAtPos (part.transform.position), 0);
				}

				// Test for terrain
				RaycastHit ray;
				if (Physics.Raycast (part.transform.position, surfaceNormal, out ray, wingSpan * 2, 1 << 15)) {
					// Close to ground, override groundDistance if close to terrain, buildings, or anything
					groundDistance = (groundDistance == 0) ? ray.distance : Math.Min (groundDistance, ray.distance);

					// also set surface normal
					surfaceNormal = ray.normal;
				}
				if (groundDistance != 0) {
					//print ("Wing up: " + thingThatLiftsParts.part.gameObject.transform.forward + " Srf: " + surfaceNormal);
					float dot = Vector3.Dot(surfaceNormal, thingThatLiftsParts.part.gameObject.transform.forward);
					//print ("Dot: " + dot);
					multiplier = 2.0f / (float)Math.Pow (0.3f * groundDistance + 1.0f, 2) * Math.Abs(dot) + 1.0f;
				}
			}

			//print ("Lift Multiplier " + multiplier + " alt: " + vessel.radarAltitude);

			//((thingThatAlsoLiftsPartsButMoves == null) ? thingThatLiftsParts.deflectionLiftCoeff : thingThatAlsoLiftsPartsButMoves.ctrlSurfaceArea) = nextLift;
			thingThatLiftsParts.deflectionLiftCoeff = initialLift * multiplier;

			if (thingThatAlsoLiftsPartsButMoves != null) {
				//print ("CTRL SURFACE!!");
				thingThatAlsoLiftsPartsButMoves.ctrlSurfaceArea = initialLiftCtrl * multiplier;
			}

		}

	}

}

