using System;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{

	// I don't know c#

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ModuleGroundEffect : PartModule 
	{ 

		public const float ActivateAltitude = 80;
		public const float RaycastAltitude = 30;

		private float initialLift;
		private float initialLiftCtrl;

		// maybe do something with this later
		private float wingSpan = 4.5f;

		private ModuleLiftingSurface thingThatLiftsParts;
		private ModuleControlSurface thingThatAlsoLiftsPartsButMoves;

		//private bool farremLegacy;
		private PartModule ferramModule;
		private FieldInfo ferramField;

		public static bool ferramEnabled = false;

		public override void OnStart(StartState state) {

			if (ferramEnabled) {
				// Do Ferrem stuff

				// isn't legacy not even stilln't today but not used?
				//if (part.Modules.Contains("FARControllableSurface")) {
				//	ferramModule = part.Modules["FARControllableSurface"];
				//} else if (part.Modules.Contains("FARWingAerodynamicModel")) {
				//	ferramModule = part.Modules["FARWingAerodynamicModel"];
				//}

				if (part.Modules.Contains ("FARAeroPartModule")) {
					ferramModule = part.Modules ["FARAeroPartModule"];
					ferramField = ferramModule.GetType ().GetField ("worldSpaceAeroForce");
				} else {
					print ("FAR module not found");
					Destroy (this);
					return;
				}

				//foreach (PartModule pm in part.Modules) {
				//	print ("module: " + pm.ClassName);
				//}

				//print ("farrem module: " + ferramModule);

				print ("Ground effect module loaded with FAR");
				return;

			} else {
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

			}

			print ("Ground effect module loaded for non-aero part?");
			Destroy (this);

		}

		public override void OnUpdate() {

			float multiplier = 1.0f;

			// Checks to see if ground effect would have any significance
			if (!(((vessel.situation & (Vessel.Situations.FLYING | Vessel.Situations.LANDED | Vessel.Situations.SPLASHED)) == 0)
			    || (vessel.radarAltitude > ActivateAltitude)
			    || !vessel.mainBody.hasSolidSurface
				|| !vessel.mainBody.atmosphere)) {

				float groundDistance = 0;
				Vector3 surfaceNormal = vessel.gravityForPos.normalized;

				// Dot product with surface normal is how aligned the wing is to the ground.
				// Vertical stabilizers would have a dot product of zero
				// Horizontal wings will have 1
				float dot = Math.Abs(Vector3.Dot(surfaceNormal, part.transform.forward));

				// say that wings must be within 45 degrees flat towards the ground to have any effect 
				if (dot > 0.707f) {

					// Check distance from ocean (if planet has one), sea level is 0 (i think)
					if (FlightGlobals.currentMainBody.ocean) {
						// use Max to set to sea level if negative
						groundDistance = Math.Max (FlightGlobals.getAltitudeAtPos (part.transform.position), 0.0f);
					}

					// Check already calculated vessel center terrain height, overwrite if it's closer
					// and don't allow negative altitudes (that would also mean the vessel is probably destroyed)
					groundDistance = Math.Max (Math.Min (groundDistance, (float)vessel.radarAltitude), 0.0f);

					// Raycast terrain if it's close enough
					if (groundDistance < RaycastAltitude) {
						//print ("RAYCAST!!!");
						RaycastHit ray;
						// 1 << 15 hits anything that isn't a vessel or ocean
						if (Physics.Raycast (part.transform.position, surfaceNormal, out ray, wingSpan * 2, 1 << 15)) {
							// Close to ground, override groundDistance if close to terrain, buildings, or anything
							groundDistance = (groundDistance == 0) ? ray.distance : Math.Min (groundDistance, ray.distance);

							// also set surface normal
							surfaceNormal = ray.normal;
						}
					}

					// By now, ground distance has been determined
					// 0 means not close to terrain and no ocean

					if (groundDistance != 0) {
						multiplier = 2.0f / (float)Math.Pow (0.3f * groundDistance + 1.0f, 2) * dot + 1.0f;
						print (multiplier);
					}
				} else {
					multiplier = 1.0f;
				}
			}
				
			if (ferramEnabled) {
				
				// Since there's no values to adjust for FAR, just apply more lift force

				// TODO: THIS DOESN'T WORK

				Vector3 worldSpaceAeroForce = (Vector3)(ferramField.GetValue (ferramModule));
				print (multiplier);
				print (worldSpaceAeroForce);
				part.AddForce (worldSpaceAeroForce * (multiplier - 1.0f));
				//part.AddForce (new Vector3(0, 3000, 0));

			} else {

				thingThatLiftsParts.deflectionLiftCoeff = initialLift * multiplier;
				if (thingThatAlsoLiftsPartsButMoves != null) {
					// Control surfaces use a different PartModule
					thingThatAlsoLiftsPartsButMoves.ctrlSurfaceArea = initialLiftCtrl * multiplier;
				}
			}
		}
	}
}

