using System;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{

	// I don't know c#

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ModuleGroundEffect : PartModule 
	{ 

		public const float ActivateAltitude = 400;

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

			float multiplier = 0;

			// Checks to see if ground effect would have any significance
			if (((vessel.situation & (Vessel.Situations.FLYING | Vessel.Situations.LANDED | Vessel.Situations.SPLASHED)) == 0)
			    || (vessel.radarAltitude > ActivateAltitude)
			    || !vessel.mainBody.hasSolidSurface
			    || !vessel.mainBody.atmosphere) {

				// Set multiplier to 1 so that coefficients would have no change
				multiplier = 1.0f;
			} else {

				// 

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

				// By now, ground distance has been determined
				// 0 means not close to terrain and no ocean

				if (groundDistance != 0) {
					// Dot product with surface normal is how aligned the wing is to the ground.
					// Vertical stabilizers would have a dot product of zero
					// Horizontal wings will have 1
					float dot = Math.Abs(Vector3.Dot(surfaceNormal, part.transform.forward));

					multiplier = 2.0f / (float)Math.Pow (0.3f * groundDistance + 1.0f, 2) * dot + 1.0f;
				}
			}
				
			if (ferramEnabled) {
				
				// Since there's no values to adjust for FAR, just apply more lift force
				Rigidbody rb = part.Rigidbody;
				Vector3 worldSpaceAeroForce = (Vector3)(ferramField.GetValue (ferramModule));
				rb.AddForce (worldSpaceAeroForce * (multiplier - 1.0f));

			} else {

				thingThatLiftsParts.deflectionLiftCoeff = initialLift * multiplier;
				if (thingThatAlsoLiftsPartsButMoves != null) {
					//print ("CTRL SURFACE!!");
					thingThatAlsoLiftsPartsButMoves.ctrlSurfaceArea = initialLiftCtrl * multiplier;
				}
			}
		}
	}
}

