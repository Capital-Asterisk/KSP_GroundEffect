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
        public const float DefaultLiftMultiplier = 8;

        // Situation at which ground effect can occur
        const Vessel.Situations LowFlying = (
            Vessel.Situations.FLYING
            | Vessel.Situations.LANDED
            | Vessel.Situations.SPLASHED
        );

        // ModuleLiftingSurface uses deflectionLiftCoeff
        // ModuleControlSurface uses ctrlSurfaceArea

        // Lift of aerodynamic surfaces
        List<ModuleLiftingSurface> liftingSurfaces;
        List<ModuleControlSurface> controlSurfaces;

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

        Vector3 oceanNormal;

        public GameObject gameBreaker;

        protected override void OnStart()
        {
            base.OnStart();

            gameBreaker = new GameObject("Fucker");

            print("Activation: " + base.GetActivation());

            // nothing is really done here

            //print ("HEWWO WORLD x3 " + vessel.GetName() + " " + (vessel.state == Vessel.State.ACTIVE));
            TimingManager.FixedUpdateAdd(TimingManager.TimingStage.Late, FixedUpdateLateLate);
        }


        public void OnDestroy()
        {
            TimingManager.FixedUpdateRemove(TimingManager.TimingStage.Late, FixedUpdateLateLate);
        }

        public override void OnLoadVessel()
        {
            base.OnLoadVessel();

            GameEvents.onVesselStandardModification.Add(VesselStandardModification);

        }

        public override void OnUnloadVessel()
        {
            // this method doesn't seem to get called anywhere

            print("Vessel unloaded");

           

            GameEvents.onVesselStandardModification.Remove(VesselStandardModification);


            base.OnUnloadVessel();

        }



        public void VesselStandardModification(Vessel ves)
        {
            if (ves == vessel)
            {
                // Vessel modified, list of parts changed
                liftingSurfaces = null;
                controlSurfaces = null;
            }
        }

        private void FixedUpdate()
        {

        }

        private void FixedUpdateLateLate()
        {

            if (!vessel.loaded)
            {
                return;
            }

            // Checks to see if ground effect would have any significance
            if (   ((vessel.situation & LowFlying) == 0)
                || (vessel.radarAltitude > ActivateAltitude)
                || !vessel.mainBody.hasSolidSurface
                || !vessel.mainBody.atmosphere)
            {
                if (inRange)
                {
                    // Previously in ground effect, just exited
                    print(vessel.GetName() + " Exited Ground Effect range");
                    inRange = false;
                    //ResetLiftValues();
                    liftingSurfaces = null;
                    controlSurfaces = null;
                }

                return;
            }


            if (liftingSurfaces == null)
            {
                // Count the control surfaces if not done so
                //print(vessel.GetName() + " Entered Ground Effect range");
                CountSurfaces();
            }

            groundDir = vessel.gravityForPos.normalized;
            oceanNormal = -groundDir;

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

            bool prevInGroundEffect = inGroundEffect;

            // Set true in CalculateLift, if a wing is close enough
            inGroundEffect = false;

            // Loop trough all surfaces and change their lift
            // Also get the max wingspan


            for (int i = 0; i < liftingSurfaces.Count; i++)
            {
                Part part = liftingSurfaces[i].part;
                Vector3 newLift = AddGroundEffectForce(part, liftingSurfaces[i].liftForce);
                if (liftingSurfaces[i].liftArrow)
                {
                    liftingSurfaces[i].liftArrow.Direction = newLift;
                    liftingSurfaces[i].liftArrow.Length = newLift.magnitude;
                }


                newWingSpan = Math.Max(newWingSpan, ApproximateWingSpan(part));

            }

            for (int i = 0; i < controlSurfaces.Count; i++)
            {
                Part part = controlSurfaces[i].part;
                Vector3 newLift = AddGroundEffectForce(part, controlSurfaces[i].liftForce);

                if (controlSurfaces[i].liftArrow)
                {
                    controlSurfaces[i].liftArrow.Direction = newLift;
                    controlSurfaces[i].liftArrow.Length = newLift.magnitude;
                }


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
            float span = 2.0f * (mag * (1.0f - Math.Abs(Vector3.Dot(part.Rigidbody.velocity.normalized, localPos / mag))) + part.collider.bounds.size.magnitude / 2);

            if (float.IsNaN(span))
            {
                return 0;
            }

            return span;
        }

        private Vector3 AddGroundEffectForce(Part part, Vector3 originalLift)
        {
            Vector3 normal = oceanNormal;

            // groundPlane.distance and groundDistance are different btw.
            // groundDistance is measured from an individual part
            float groundDistance = Single.MaxValue;

            // say that wings must be within 45 degrees flat towards the ground to have any effect 
            //if (dot > 0.707f) {

            // Check distance from ocean (if planet has one), sea level is 0 (i think)
            if (FlightGlobals.currentMainBody.ocean)
            {
                groundDistance = Math.Max(FlightGlobals.getAltitudeAtPos(part.transform.position), 0.0f);
            }

            // groundPlane.distance is zero if ground is too far away
            if (groundPlane.distance != 0.0f)
            {
                // Set ground distance to approximated terrain proximity
                // If the ocean is closer, then the ocean distance will be used
                groundDistance = Math.Min(groundDistance, groundPlane.GetDistanceToPoint(part.transform.position));
                normal = groundPlane.normal;
            }

            if (Vector3.Dot(originalLift, normal) < 0)
            {
                // ignore downwards facing wings
                return originalLift;
            }

            // By now, ground distance has been determined

            // Convert ground distance to wing spans between 0.0 .. 1.0
            groundDistance = Math.Min(1.0f, groundDistance / wingSpan);

            if (groundDistance == 1.0f)
            {
                // not close enough to the ground, return lift unchanged
                return originalLift;
            }

            // Confirmed wing is in ground effect

            inGroundEffect = true;


            // Dot product with surface normal is how aligned the wing is to the ground.
            // Vertical stabilizers would have a dot product of zero
            // Horizontal wings will have 1
            float horizontalness = Math.Abs(Vector3.Dot(groundDir, part.transform.forward));


            // at groundDistance = 1.0, groundEffectMul is 1.0
            // as it gets closer to the ground...
            // at groundDistance = 0.0, groundEffectMul = LiftMultiplier
            // y = m(x - 1)^2 + 1
            //float groundEffectMul = (DefaultLiftMultiplier - 1) 
            //                      * (float)(Math.Pow(groundDistance - 1.0f, 2.0f))
            //                      + 1;

            // Induced drag:
            // get horizontal velocity = velocity - normal * velocity.dot(normal)
            // normalize horizontal velocity
            // induced drag = horzVelocity * lift.dot(horzVelocity)

            Vector3 velocity = part.Rigidbody.velocity;
            Vector3 horzVelocity = (velocity - normal * Vector3.Dot(velocity, normal)).normalized;
            Vector3 inducedDrag = horzVelocity * Vector3.Dot(originalLift, horzVelocity);

            float groundness = horizontalness * (1.0f - groundDistance);
            groundness *= groundness;

            // force = -inducedDrag * groundness + (originalLift - inducedDrag) * DefaultLiftMultiplier * groundness;
            // force = ((originalLift - inducedDrag) * DefaultLiftMultiplier - inducedDrag) * groundness;
            Vector3 force = -inducedDrag * groundness + (originalLift - inducedDrag) * DefaultLiftMultiplier * groundness;
            part.Rigidbody.AddForce(force, ForceMode.Force);

            return force + originalLift;

            //Vector3 extraLift = (originalLift - inducedDrag) * DefaultLiftMultiplier * (groundDistance - horizontalness + 1.0f);

            //Vector3 extraLift = normal * horizontalness * equation;

            //float totalMag = originalLift.magnitude * DefaultLiftMultiplier;
            //Vector3 newLift = Vector3.Lerp(normal * totalMag, originalLift,
            //                               groundDistance - horizontalness + 1.0f);

            //part.Rigidbody.AddForce(newLift - originalLift, ForceMode.Force);

            //part.Rigidbody.AddForce(newLift - originalLift, ForceMode.Force);

            // return new lift force
            //return newLift;
        }


        private void CountSurfaces()
        {

            print("Counting wings for " + vessel.GetName());

            liftingSurfaces = new List<ModuleLiftingSurface>();
            controlSurfaces = new List<ModuleControlSurface>();


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
                    if (thingThatLiftsPartsAndMoves != null)
                    {
                        // It's a control surface, add to control surface arrays
                        controlSurfaces.Add(thingThatLiftsPartsAndMoves);
                    }
                    else
                    {
                        // It's just a lifting surface, add to lifting surface arrays
                        liftingSurfaces.Add(thingThatLiftsParts);
                    }
                }
               
                //thingThatLiftsPartsAndMoves.OnCenterOfLiftQuery();

            }

            print("LiftingSurfaces counted for " + vessel.GetName() + ": " + liftingSurfaces.Count);
            print("ControlSurfaces counted for " + vessel.GetName() + ": " + controlSurfaces.Count);
        }
    }
}

