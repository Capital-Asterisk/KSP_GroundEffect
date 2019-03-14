using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{
    public class VesselGroundEffect : VesselModule
    {

        public const float ActivateAltitude = 80;
        //CenterOfLiftQuery colQuery;

        bool inGroundEffect;
        float wingSpan;
        Plane groundPlane;
        Vector3 gravityDir;

        // ModuleLiftingSurface and ModuleControlSurface use different
        // variables for lift coefficient. They should be dealt with separately

        List<ModuleLiftingSurface> liftingSurfaces;
        List<ModuleControlSurface> controlSurfaces;

        List<float> liftingInitLift;
        List<float> controlInitLift;

        protected override void OnStart()
        {
            base.OnStart();

            // nothing is really done here

            //print ("HEWWO WORLD x3 " + vessel.GetName() + " " + (vessel.state == Vessel.State.ACTIVE));
        }

        public override void OnLoadVessel()
        {
            base.OnLoadVessel();
            //print(vessel.GetName() + " LOADED: " + vessel.loaded);
            groundPlane = new Plane();

            GameEvents.onVesselStandardModification.Add(VesselStandardModification);
        }

        public override void OnUnloadVessel()
        {
            base.OnUnloadVessel();
            //print(vessel.GetName() + " UNLOADED: " + vessel.loaded);

            GameEvents.onVesselStandardModification.Remove(VesselStandardModification);

            // Don't permanently save any of the lift values added
            ResetLiftValues();
        }

        public void VesselStandardModification(Vessel ves)
        {
            if (ves == vessel)
            {
                // This vessel just exploded, undocked or something

                // Set all lift values to default, as the new VesselGroundEffect
                // that would be added to it will use modified values as defaults

                //print("Vessel Modified!!!!");

                ResetLiftValues();
            }
        }

        private void FixedUpdate()
        {
            //print(vessel.GetName() + " " + vessel.loaded + " " + Time.time);
            if (vessel.loaded)
            {

                // Checks to see if ground effect would have any significance
                if (!(((vessel.situation & (Vessel.Situations.FLYING | Vessel.Situations.LANDED | Vessel.Situations.SPLASHED)) == 0)
                    || (vessel.radarAltitude > ActivateAltitude)
                    || !vessel.mainBody.hasSolidSurface
                    || !vessel.mainBody.atmosphere))
                {
                    if (liftingSurfaces == null)
                    {
                        // Count the control surfaces if not done so
                        print(vessel.GetName() + " Entered Ground Effect");
                        CountSurfaces();
                    }

                    gravityDir = vessel.gravityForPos.normalized;

                    // Find out where the ground is
                    // Note: the side of a cliff counts too

                    // Get all nearby terrain
                    //Collider[] terrain = Physics.OverlapSphere(vessel.transform.position, wingSpan, 1 << 15);

                    //print(terrain.Length);

                    //foreach (Collider collider in terrain)
                    //{
                    //   Collider.
                    //}

                    // The calculations above don't seem to work yet, use ones below

                    // Raycast terrain if it's close enough
                    if (vessel.radarAltitude < wingSpan * 2)
                    {
                        RaycastHit ray;
                        // 1 << 15 hits anything that isn't a vessel or ocean
                        if (Physics.Raycast(vessel.CoM, gravityDir, out ray, wingSpan * 2, 1 << 15))
                        {
                            groundPlane.SetNormalAndPosition(ray.normal, ray.point);

                        }
                    }
                    else
                    {
                        groundPlane.distance = 0;
                    }


                    float lift;
                    float newWingSpan = 0;

                    for (int i = 0; i < liftingSurfaces.Count; i++)
                    {
                        Part part = liftingSurfaces[i].part;
                        lift = CalculateLift(part, liftingInitLift[i]);
                        liftingSurfaces[i].deflectionLiftCoeff = lift;

                        newWingSpan = Math.Max(newWingSpan, ApproximateWingSpan(part));

                    }

                    for (int i = 0; i < controlSurfaces.Count; i++)
                    {
                        Part part = controlSurfaces[i].part;
                        lift = CalculateLift(part, controlInitLift[i]);
                        controlSurfaces[i].ctrlSurfaceArea = lift;

                        newWingSpan = Math.Max(newWingSpan, ApproximateWingSpan(part));
                    }

                    wingSpan = newWingSpan;
                    //print("WingSpan: " + newWingSpan);

                }
                else if (inGroundEffect)
                {
                    print(vessel.GetName() + " Exited Ground Effect");
                    inGroundEffect = false;
                    ResetLiftValues();
                }
            }
        }

        private float ApproximateWingSpan(Part part)
        {
            Vector3 localPos = (part.transform.position - vessel.CoM);
            float mag = localPos.magnitude;
            return 2.0f * (mag * (1.0f - Math.Abs(Vector3.Dot(part.Rigidbody.velocity.normalized, localPos / mag))) + part.collider.bounds.size.magnitude / 2);
        }

        private float CalculateLift(Part part, float initialValue)
        {
            float groundDistance = 0;
            Vector3 surfaceNormal = vessel.gravityForPos.normalized;


            // say that wings must be within 45 degrees flat towards the ground to have any effect 
            //if (dot > 0.707f) {

            // Check distance from ocean (if planet has one), sea level is 0 (i think)
            if (FlightGlobals.currentMainBody.ocean)
            {
                // use Max to set to sea level if negative
                groundDistance = Math.Max(FlightGlobals.getAltitudeAtPos(part.transform.position), 0.0f);
            }

            if (groundPlane.distance != 0.0f)
            {
                groundDistance = Math.Min(groundDistance, groundPlane.GetDistanceToPoint(part.transform.position));
                print("Ground Plane distance: " + groundDistance);
            }

            // By now, ground distance has been determined
            // 0 means not close to terrain and no ocean

            if (groundDistance != 0.0f)
            {

                // Dot product with surface normal is how aligned the wing is to the ground.
                // Vertical stabilizers would have a dot product of zero
                // Horizontal wings will have 1
                float dot = Math.Abs(Vector3.Dot(surfaceNormal, part.transform.forward));

                groundDistance = Math.Min(1, groundDistance / wingSpan);
                // Ground distance is now in wings spans to the ground

                // Multiplier when ground distance is 0 wingspans
                // 6 * more lift when close on ground
                float mul = 4;

                // y = m(x - 1)^2 + 1
                print("Extra Lift: " + ((mul - 1) * (float)Math.Pow(groundDistance - 1.0f, 2.0f)));
                return initialValue * dot * ((mul - 1) * (float)Math.Pow(groundDistance - 1.0f, 2.0f) + 1.0f);
            }

            return initialValue;
        }

        private void ResetLiftValues()
        {

            // Set all values back to default

            if (liftingSurfaces != null)
            {
                for (int i = 0; i < liftingSurfaces.Count; i++)
                {
                    liftingSurfaces[i].deflectionLiftCoeff = liftingInitLift[i];
                }

                for (int i = 0; i < controlSurfaces.Count; i++)
                {
                    controlSurfaces[i].ctrlSurfaceArea = controlInitLift[i];
                }

                liftingSurfaces = null;
                controlSurfaces = null;

                liftingInitLift = null;
                controlInitLift = null;
            }
        }

        private void CountSurfaces()
        {

            print("ASDASDASD Counting wings");

            liftingSurfaces = new List<ModuleLiftingSurface>();
            controlSurfaces = new List<ModuleControlSurface>();

            liftingInitLift = new List<float>();
            controlInitLift = new List<float>();

            inGroundEffect = true;

            // This will be recalculated later
            wingSpan = 42 / 42;

            foreach (Part part in vessel.Parts)
            {
           
                ModuleControlSurface thingThatLiftsPartsAndMoves = null;
                ModuleLiftingSurface thingThatLiftsParts = null;

                // Look through the list of part modules to find anything that inherits ModuleLiftingSurface
                foreach (PartModule module in part.Modules)
                {

                    if (typeof(ModuleLiftingSurface).IsAssignableFrom(module.GetType()))
                    {
                        //thingThatLiftsParts = (ModuleLiftingSurface)(module);
                        //initialLift = thingThatLiftsParts.deflectionLiftCoeff;

                        thingThatLiftsParts = (ModuleLiftingSurface)(module);

                        if (module is ModuleControlSurface)
                        {
                            thingThatLiftsPartsAndMoves = (ModuleControlSurface)(module);
                        }
                    }
                }

                if (thingThatLiftsParts != null)
                {
                    // A Lifting Surface has been found, use to calculate initial wingspan
                    //thingThatLiftsParts.OnCenterOfLiftQuery(colQuery);
                    //colQuery.pos;
                    //foreach (Bounds bounds in part.GetColliderBounds())
                    //{
                    //    bounds.
                    //}

                    if (thingThatLiftsPartsAndMoves != null)
                    {
                        // It's a control surface, add to control surface arrays
                        controlSurfaces.Add(thingThatLiftsPartsAndMoves);
                        controlInitLift.Add(thingThatLiftsPartsAndMoves.ctrlSurfaceArea);
                    }
                    else
                    {
                        // It's just a lifting surface, add to lifting surface arrays
                        liftingSurfaces.Add(thingThatLiftsParts);
                        liftingInitLift.Add(thingThatLiftsParts.deflectionLiftCoeff);
                    }
                }
               
                //thingThatLiftsPartsAndMoves.OnCenterOfLiftQuery();

            }

            print("LiftingSurfaces counted for " + vessel.GetName() + ": " + liftingSurfaces.Count);
            print("ControlSurfaces counted for " + vessel.GetName() + ": " + controlSurfaces.Count);
        }
    }
}

