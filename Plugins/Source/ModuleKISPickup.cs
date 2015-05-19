using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KIS
{
    public class ModuleKISPickup : PartModule
    {
        [KSPField]
        public bool canDetach = false;
        [KSPField]
        public float detachMaxMass = Mathf.Infinity;
        /// <summary>
        /// Specifies whether scaling should be used for calculating the maximum allowed mass.
        /// </summary>
        [KSPField]
        public bool enableMassScaling = false;
        /// <summary>
        /// The factor by which grabbable mass is increased for a 5-star engineer in freefall.
        /// </summary>
        [KSPField]
        public float engineerMassFactor = 2f;
        /// <summary>
        /// Positive value make the engineer's mass factor increase exponentially up to [engineerMassFactor].
        /// Zero or negative value makes the relationship linear.
        /// <remarks>The greater the value, the more linear the relationship.</remarks>
        /// </summary>
        [KSPField]
        public float experienceExponent = 0.2;
        /// <summary>
        /// The factor by which grabbable mass in increased when in freefall.
        /// </summary>
        [KSPField]
        public float freeFallMassFactor = 1.5f;
        [KSPField]
        public float maxDistance = 2;
        [KSPField]
        public float maxMass = 1;
        [KSPField]
        public string dropSndPath = "KIS/Sounds/drop";
        [KSPField]
        public string attachSndPath = "KIS/Sounds/attach";
        [KSPField]
        public string detachSndPath = "KIS/Sounds/detach";
        public FXGroup sndFx;

        /// <summary>
        /// Gets the maximum allowed mass.
        /// </summary>
        /// <remarks>
        /// This is dependent on the kerbal's situation, and what engineering experience level they have.
        /// </remarks>
        public float ScaledMaxMass
        {
            get
            {
                // Save ourselves some cycles
                if (!enableMassScaling)
                    return maxMass;

                // This really shouldn't happen - there should be a Kerbal, but I'm being paranoid
                if (vessel.GetCrewCount() == 0)
                    return maxMass;

                float factor = 1;

                // Assume that while on EVA high in the atmosphere you're probably in freefall
                ExperimentSituations situation = ScienceUtil.GetExperimentSituation(vessel);
                bool freefall = (situation == ExperimentSituations.InSpaceHigh) || (situation == ExperimentSituations.InSpaceLow) | (situation == ExperimentSituations.FlyingHigh);
                if (freefall)
                {
                    bool isEngineer = false;
                    ProtoCrewMember kerbal = vessel.GetVesselCrew()[0];
                    foreach (var skill in kerbal.experienceTrait.Effects)
                    {
                        if (skill is Experience.Effects.RepairSkill)
                        {
#if DEBUG
                            print(string.Format("{0} is an engineer with an experience level of {1}", kerbal.name, kerbal.experienceLevel));
#endif
                            isEngineer = true;
                            break;
                        }
                    }

                    // For engineers, the mass factor goes from 1 (0 stars) to [engineerMassFactor] (5 stars)
                    if (isEngineer)
                    {
                        if (experienceExponent > 0)
                        {
                            // Increase exponentially based on experience
                            float a = 1 - experienceExponent;
                            float c = Mathf.Log((engineerMassFactor - a) / experienceExponent) / 5;
                            factor *= a + (experienceExponent * Mathf.Exp(c * kerbal.experienceLevel));
                        }
                        else
                        {
                            // Increase linearly based on experience
                            factor *= 1 + ((engineerMassFactor - 1) * (float)kerbal.experienceLevel / 5);
                        }

#if DEBUG
                        print(string.Format("{0}'s grabbable mass has been increased {1} to {2:P2} because they're {3}",
                            kerbal.name, experienceExponent > 0 ? "exponentially" : "linearly", factor, situation));
#endif
                    }

                    // In freefall, you're able to move more mass
                    factor *= freeFallMassFactor;
                }
                else
                {
                    // The allowed mass is proportional to the inverse exponent of the gravity
                    // It's scaled, so that at zero-g it is equal to the freeFallMassFactor
                    // It also means that in deep gravity wells - Eve for example - you can grab less mass
                    // f = freeFallMassFactor * e^(-n * g)
                    // Where n = -log(1 / freeFallMassFactor)
#if DEBUG
                    print(string.Format("Local gravity is {0:P2} of Kerbin surface gravity", vessel.geeForce));
#endif
                    factor = freeFallMassFactor * Mathf.Exp(Mathf.Log(1 / freeFallMassFactor) * (float)vessel.geeForce);
                }

#if DEBUG
                print(string.Format("Grabbable mass has been scaled to {0:P2} of {1} tonnes", factor, maxMass));
#endif
                return factor * maxMass;
            }
        }
    }
}
