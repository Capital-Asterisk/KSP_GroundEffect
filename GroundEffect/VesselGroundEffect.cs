using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace KSP_GroundEffect
{
    struct WingEntry
    {
        public ModuleLiftingSurface surface;
        public float groundEffectMultiplier;
    }


    public class VesselGroundEffect : VesselModule
    {

        // Minimum Radar Altitude to start testing
        public const float ActivateAltitude = 80;

        // How much lift is multiplied at maximum proximity
        public const float DefaultLiftMultiplier = 2;

        // Situation at which ground effect can occur
        const Vessel.Situations LowFlying = (
            Vessel.Situations.FLYING
            | Vessel.Situations.LANDED
            | Vessel.Situations.SPLASHED
        );

        // ModuleLiftingSurface uses deflectionLiftCoeff
        // ModuleControlSurface uses ctrlSurfaceArea

        // Lift of aerodynamic surfaces
        List<WingEntry> liftingSurfaces;
        //List<ModuleControlSurface> controlSurfaces;

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


        protected override void OnStart()
        {
            base.OnStart();

            //print ("HEWWO WORLD x3 " + vessel.GetName() + " " + (vessel.state == Vessel.State.ACTIVE));
            TimingManager.FixedUpdateAdd(TimingManager.TimingStage.Late,
                                            FixedUpdateLateLate);
        }


        public void OnDestroy()
        {

            TimingManager.FixedUpdateRemove(TimingManager.TimingStage.Late,
                                            FixedUpdateLateLate);
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

           

            GameEvents.onVesselStandardModification
                        .Remove(VesselStandardModification);


            base.OnUnloadVessel();

        }



        public void VesselStandardModification(Vessel ves)
        {
            if (ves == vessel)
            {
                // Vessel modified, list of parts changed
                liftingSurfaces = null;
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

            // Use raytrace below to set the ground plane

            if (vessel.radarAltitude < wingSpan * 2.0f)
            {
                RaycastHit ray;
                // 1 << 15 hits anything that isn't a vessel or ocean
                if (Physics.Raycast(vessel.CoM, groundDir, out ray,
                            wingSpan * 2.0f, 1 << 15))
                {
                    groundPlane.SetNormalAndPosition(ray.normal, ray.point);

                }
                else
                {
                    groundPlane.distance = 0.0f;
                }
            }
            else
            {
                groundPlane.distance = 0.0f;
            }
           
            float newWingSpan = 1.0f;

            bool prevInGroundEffect = inGroundEffect;

            // Set true in AddGroundEffectForce, if any wing is close enough to the
            // ground
            inGroundEffect = false;

            // Loop trough all surfaces and change their lift
            // Also get the max wingspan


            for (int i = 0; i < liftingSurfaces.Count; i++)
            {
                ModuleLiftingSurface surface = liftingSurfaces[i].surface;
                float mul = liftingSurfaces[i].groundEffectMultiplier;
                Part part = surface.part;

               

                Vector3 newLift = AddGroundEffectForce(part, surface.liftForce,
                        mul);

                // set aerodynamic overlay
                if (surface.liftArrow)
                {
                    // arrow length is 2x smaller than lift force as of 1.10
                    surface.liftArrow.Length = newLift.magnitude * 0.5f;
                    surface.liftArrow.Direction = newLift;
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

            //print("wingspan: " + newWingSpan);

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
            float localPosMag = localPos.magnitude;
            Vector3 vnorm = part.Rigidbody.velocity.normalized;


            float span = (localPosMag * (1.0f - Math.Abs(
                                Vector3.Dot(vnorm, localPos / localPosMag)))
                            + part.collider.bounds.size.magnitude / 2);

            if (float.IsNaN(span))
            {
                return 0;
            }

            return span;
        }

        private Vector3 AddGroundEffectForce(Part part, Vector3 originalLift,
                    float multiplier)
        {
            Vector3 normal = oceanNormal;

            // groundPlane.distance and groundDistance are different btw.
            // groundDistance is measured from an individual part
            float groundDistance = Single.MaxValue;


            // Check distance from ocean (if planet has one)
            if (FlightGlobals.currentMainBody.ocean)
            {
                groundDistance = Math.Max(FlightGlobals.getAltitudeAtPos(
                                            part.transform.position), 0.0f);
            }

            // groundPlane.distance is zero if ocean is too far away
            if (groundPlane.distance != 0.0f)
            {
                // Set ground distance to approximated terrain proximity.
                // If the ocean is closer, then the ocean distance will be used
                groundDistance = Math.Min(groundDistance,
                        groundPlane.GetDistanceToPoint(part.transform.position));
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


            // Dot product with surface normal is how aligned the wing is to the
            // ground. ertical stabilizers would have a dot product of zero.
            // Horizontal wings will have 1
            float horizontalness
                    = Math.Abs(Vector3.Dot(groundDir, part.transform.forward));

            // ranking of how much ground effect is in effect from 0.0 ... 1.0
            float groundness = horizontalness * (1.0f - groundDistance);



            // Induced drag:
            // get horizontal velocity = velocity - normal * velocity.dot(normal)
            // normalize horizontal velocity
            // induced drag = horzVelocity * lift.dot(horzVelocity)

            Vector3 velocity = part.Rigidbody.velocity;
            Vector3 horzVelocityNorm
                    = (velocity - normal * Vector3.Dot(velocity, normal))
                        .normalized;
            Vector3 inducedDrag = horzVelocityNorm
                                * Vector3.Dot(originalLift, horzVelocityNorm);
            Vector3 verticalLift = originalLift - inducedDrag;

            // Apply Reverse of induced drag
            // and Add more Vertical Lift
            Vector3 force = -inducedDrag * groundness
                          + verticalLift * multiplier * groundness;

            // This adds to the net force on the wing, without replacing the
            // origial lift force
            part.Rigidbody.AddForce(force, ForceMode.Force);

            return force + originalLift;

            // at groundDistance = 1.0, groundEffectMul is 1.0
            // as it gets closer to the ground...
            // at groundDistance = 0.0, groundEffectMul = LiftMultiplier
            // y = m(x - 1)^2 + 1
            //float groundEffectMul = (DefaultLiftMultiplier - 1) 
            //                      * (float)(Math.Pow(groundDistance - 1.0f, 2.0f))
            //                      + 1;

            // force = -inducedDrag * groundness + (originalLift - inducedDrag) * DefaultLiftMultiplier * groundness;
            // force = ((originalLift - inducedDrag) * DefaultLiftMultiplier - inducedDrag) * groundness;

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

            liftingSurfaces = new List<WingEntry>();


            inRange = true;

            groundPlane = new Plane();

            // This will be calculated later
            wingSpan = 42 / 42;

            foreach (Part part in vessel.Parts)
            {
           
                ModuleLiftingSurface thingThatLiftsParts = null;
                float multiplyingLiftWhatever = DefaultLiftMultiplier;

                // Look through the list of part modules to find anything that
                // inherits ModuleLiftingSurface
                foreach (PartModule module in part.Modules)
                {

                    if (typeof(ModuleLiftingSurface).IsAssignableFrom(module.GetType()))
                    {
                        thingThatLiftsParts = (ModuleLiftingSurface)(module);
                        //initialLift = thingThatLiftsParts.deflectionLiftCoeff;



                        if (module is ModuleControlSurface)
                        {
                            // maybe do specific for control surfaces
                        }
                    }
                    else if (module is ModuleGroundEffect)
                    {
                        multiplyingLiftWhatever = ((ModuleGroundEffect) module)
                                .groundEffectMultiplier;
                    }
                }

                if (thingThatLiftsParts != null)
                {
                    WingEntry entry = new WingEntry();
                    entry.groundEffectMultiplier = multiplyingLiftWhatever;
                    entry.surface = thingThatLiftsParts;
                    liftingSurfaces.Add(entry);
                }


                //thingThatLiftsPartsAndMoves.OnCenterOfLiftQuery();

            }

            print("LiftingSurfaces counted for " + vessel.GetName()
                    + ": " + liftingSurfaces.Count);
        }
    }
}

