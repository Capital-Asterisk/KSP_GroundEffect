using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{
    public class VesselGroundEffect : VesselModule
    {

        // Minimum Radar Altitude to start testing
        public const float ActivateAltitude = 80;

        // How much lift is multiplied at maximum proximity
        public const float LiftMultiplier = 4;

        // Situation at which ground effect can occur
        Vessel.Situations LowFlying = (
            Vessel.Situations.FLYING
            | Vessel.Situations.LANDED
            | Vessel.Situations.SPLASHED
        );

        // ModuleLiftingSurface uses deflectionLiftCoeff
        // ModuleControlSurface uses ctrlSurfaceArea

        // Lift of aerodynamic surfaces
        List<ModuleLiftingSurface> liftingSurfaces;
        List<ModuleControlSurface> controlSurfaces;

        // Initial lift values
        // Original values are modified, so they have to be stored
        List<float> liftingInitLift;
        List<float> controlInitLift;

        // Keep track of when the vessel enters/exits the ActivateAltitude
        bool inRange;

        // Keep track of when ground effect forces are actually active
        bool inGroundEffect;

        // Approximated every FixedUpdate
        float wingSpan;

        // Plane used to approximate the ground
        Plane groundPlane;

        // Direction towards the ground
        Vector3 groundDir;

        protected override void OnStart()
        {
            base.OnStart();

            // nothing is really done here

            //print ("HEWWO WORLD x3 " + vessel.GetName() + " " + (vessel.state == Vessel.State.ACTIVE));
        }

        public override void OnLoadVessel()
        {
            base.OnLoadVessel();

            // Got this technique from Ferram
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

                ResetLiftValues();
            }
        }

        private void FixedUpdate()
        {

            if (vessel.loaded)
            {

                // Checks to see if ground effect would have any significance
                if (!(((vessel.situation & LowFlying) == 0)
                    || (vessel.radarAltitude > ActivateAltitude)
                    || !vessel.mainBody.hasSolidSurface
                    || !vessel.mainBody.atmosphere))
                {
                    if (liftingSurfaces == null)
                    {
                        // Count the control surfaces if not done so
                        //print(vessel.GetName() + " Entered Ground Effect range");
                        CountSurfaces();
                    }

                    groundDir = vessel.gravityForPos.normalized;

                    // Find out where the ground is
                    // Note: the side of a cliff counts too

                    // Get all nearby terrain
                    //Collider[] terrain = Physics.OverlapSphere(vessel.transform.position, wingSpan, 1 << 15);

                    //print(terrain.Length);

                    //foreach (Collider collider in terrain)
                    //{
                    //   Collider.
                    //}

                    // The calculations above aren't done yet
                    // Use raytrace below to set the ground plane

                    if (vessel.radarAltitude < wingSpan * 2)
                    {
                        RaycastHit ray;
                        // 1 << 15 hits anything that isn't a vessel or ocean
                        if (Physics.Raycast(vessel.CoM, groundDir, out ray, wingSpan * 2, 1 << 15))
                        {
                            groundPlane.SetNormalAndPosition(ray.normal, ray.point);

                        }
                    }
                    else
                    {
                        groundPlane.distance = 0;
                    }
                   
                    float newWingSpan = 1;
                    float lift;

                    bool prevInGroundEffect = inGroundEffect;

                    // Set true in CalculateLift, if a wing is close enough
                    inGroundEffect = false;

                    // Loop trough all surfaces and change their lift
                    // Also get the max wingspan

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

                    if (prevInGroundEffect && !inGroundEffect)
                    {
                        print(vessel.GetName() + " Exited Ground Effect");
                    }
                    else if (!prevInGroundEffect && inGroundEffect)
                    {
                        print(vessel.GetName() + " Entered Ground Effect");
                    }

                    wingSpan = newWingSpan;
                }
                else if (inRange)
                {
                    //print(vessel.GetName() + " Exited Ground Effect range");
                    inRange = false;
                    ResetLiftValues();
                }
            }
        }

        private float ApproximateWingSpan(Part part)
        {
            // Problem: there isn't a good way to get 'Forward'
            // Which wings are left and right?
            // What if it's a spinning rotor?
           
            // This part checks how perpendicular the wing's position is with
            // its velocity, using dot product.

            // If velocity is [Forward], then
            // Wings directly in front or behind the CoM will have 0 wingspan
            // Wings to the side of center of mass, will have a wingspan equal
            // to their distance from the CoM + size

            if (part == null || part.collider == null || part.Rigidbody == null)
            {
                return 0;
            }

            // CoL moves around a bit too much, so I thought CoM would be better
            Vector3 localPos = (part.transform.position - vessel.CoM);

            float mag = localPos.magnitude;
            return 2.0f * (mag * (1.0f - Math.Abs(Vector3.Dot(part.Rigidbody.velocity.normalized, localPos / mag))) + part.collider.bounds.size.magnitude / 2);
        }

        private float CalculateLift(Part part, float initialValue)
        {

            // groundPlane.distance and groundDistance are different btw.
            // groundDistance is measured from an individual part
            float groundDistance = Single.MaxValue;

            // say that wings must be within 45 degrees flat towards the ground to have any effect 
            //if (dot > 0.707f) {

            // Check distance from ocean (if planet has one), sea level is 0 (i think)
            if (FlightGlobals.currentMainBody.ocean)
            {
                groundDistance = FlightGlobals.getAltitudeAtPos(part.transform.position);
            }

            // groundPlane.distance is zero if ground is too far away
            if (groundPlane.distance != 0.0f)
            {
                groundDistance = Math.Min(groundDistance, groundPlane.GetDistanceToPoint(part.transform.position));
                //print("Ground Plane distance: " + groundDistance);
            }

            // By now, ground distance has been determined
            // negative means not close to terrain

            groundDistance = Math.Min(1.0f, groundDistance / wingSpan);

            if (groundDistance != 1.0f)
            {
                inGroundEffect = true;

                // Dot product with surface normal is how aligned the wing is to the ground.
                // Vertical stabilizers would have a dot product of zero
                // Horizontal wings will have 1
                float dot = Math.Abs(Vector3.Dot(groundDir, part.transform.forward));

                // Ground distance is now in wings spans to the ground

                // y = m(x - 1)^2 + 1
                float equation = (
                            (LiftMultiplier - 1)
                            * (float)(Math.Pow(groundDistance - 1.0f, 2.0f))
                            + 1.0f);
                //print("Extra Lift: " + ((LiftMultiplier - 1) * (float)Math.Pow(groundDistance - 1.0f, 2.0f)));
                return initialValue * dot * equation;
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

            print("Counting wings for " + vessel.GetName());

            liftingSurfaces = new List<ModuleLiftingSurface>();
            controlSurfaces = new List<ModuleControlSurface>();

            liftingInitLift = new List<float>();
            controlInitLift = new List<float>();

            inRange = true;

            groundPlane = new Plane();

            // This will be calculated later
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

