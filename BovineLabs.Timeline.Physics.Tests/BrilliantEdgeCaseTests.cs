using System.Diagnostics;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Data.Mixers;
using BovineLabs.Timeline.Physics.Drags;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.Sockets;
using BovineLabs.Timeline.Physics.TriggerEvents;
using BovineLabs.Timeline.Physics.VelocityOverrides;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    using Debug = UnityEngine.Debug;
    using Entity = Entity;

    public class BrilliantEdgeCaseTests : ECSTestsFixture
    {
        #region Kinematics Apply Edge Cases

        [Test]
        public void KinematicsApply_ZeroDeltaTime_DoesNotAddContinuousForce()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddBuffer<PendingForce>(target);
            Manager.AddComponentData(target, new PhysicsForceState { Fired = false });

            Manager.AddComponentData(target, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Continuous,
                    Linear = new float3(0, 10, 0)
                }
            });

            World.SetTime(new TimeData(0.1, 0f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(0, pendings.Length, "Continuous forces should not be applied if dt <= 0.0001f");
        }

        #endregion

        #region Performance

        [Test]
        public void Performance_Accumulator_10000Entities()
        {
            var archetype = Manager.CreateArchetype(
                typeof(LocalToWorld),
                typeof(PhysicsVelocity),
                typeof(PhysicsMass),
                typeof(PendingForce)
            );

            var entities = new NativeArray<Entity>(10000, Allocator.TempJob);
            try
            {
                Manager.CreateEntity(archetype, entities);

                for (var i = 0; i < entities.Length; i++)
                {
                    Manager.SetComponentData(entities[i], new LocalToWorld { Value = float4x4.identity });
                    Manager.SetComponentData(entities[i], new PhysicsVelocity { Linear = float3.zero });
                    Manager.SetComponentData(entities[i],
                        new PhysicsMass
                            { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

                    var buffer = Manager.GetBuffer<PendingForce>(entities[i]);
                    buffer.Add(new PendingForce { Linear = new float3(0, 1, 0) });
                }

                var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
                sys.Update(WorldUnmanaged);
                Manager.CompleteAllTrackedJobs();

                var sw = Stopwatch.StartNew();
                sys.Update(WorldUnmanaged);
                Manager.CompleteAllTrackedJobs();
                sw.Stop();

                Debug.Log($"Accumulator System took {sw.Elapsed.TotalMilliseconds:F4}ms for 10,000 entities");
                Assert.Less(sw.Elapsed.TotalMilliseconds, 50.0, "System should be fast enough");
            }
            finally
            {
                entities.Dispose();
            }
        }

        #endregion

        #region Drag Edge Cases

        [Test]
        public void Drag_NegativeDrag_CausesVelocityGrowth()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target,
                new PhysicsVelocity { Linear = new float3(0, 10, 0), Angular = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

            Manager.AddComponentData(target, new ActiveDrag
            {
                Config = new PhysicsDragData
                {
                    Linear = -1.0f
                }
            });

            World.SetTime(new TimeData(0.1, 1.0f));
            var sys = World.GetOrCreateSystem<PhysicsDragApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10f * math.exp(1f), vel.Linear.y, 0.001f);
        }

        #endregion

        #region Trigger Filtering Edge Cases

        [Test]
        public void TriggerFilter_IgnoreTargetSelf_IgnoresEventsWithSelf()
        {
            var triggerEntity = Manager.CreateEntity();
            Manager.AddComponentData(triggerEntity, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(triggerEntity, new TrackBinding { Value = triggerEntity });
            Manager.AddComponentData(triggerEntity, new ClipActive());

            Manager.AddComponentData(triggerEntity, new PhysicsTriggerForceData
            {
                EventState = StatefulEventState.Enter,
                Magnitude = 10f,
                ForceType = PhysicsTriggerForceType.Directional,
                Direction = new float3(0, 0, 1),
                ApplyTo = new EntityLinkRef { ReadRootFrom = Target.Target }
            });

            Manager.AddComponentData(triggerEntity, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.Self
            });

            var triggerBuffer = Manager.AddBuffer<StatefulTriggerEvent>(triggerEntity);
            triggerBuffer.Add(new StatefulTriggerEvent
            {
                EntityB = triggerEntity,
                State = StatefulEventState.Enter
            });

            Manager.AddBuffer<PendingForce>(triggerEntity);

            var sys = World.GetOrCreateSystem<PhysicsTriggerForceSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(triggerEntity);
            Assert.AreEqual(0, pendings.Length, "Trigger should have been filtered out");
        }

        #endregion

        #region Recent Bug Fix

        [Test]
        public void VelocityMixer_DifferingSpaces_AddsVectors()
        {
            var mixer = new PhysicsVelocityMixer();
            var a = new PhysicsVelocityData
            {
                Space = Target.Self,
                Linear = new float3(1, 0, 0),
                Angular = new float3(0, 1, 0)
            };
            var b = new PhysicsVelocityData
            {
                Space = Target.Owner,
                Linear = new float3(0, 2, 0),
                Angular = new float3(0, 0, 2)
            };

            var result = mixer.Add(a, b);

            Assert.AreEqual(Target.Self, result.Space, "Dominant space should be preserved.");
            Assert.AreEqual(new float3(1, 2, 0), result.Linear,
                "Linear vectors should be added even if spaces differ.");
            Assert.AreEqual(new float3(0, 1, 2), result.Angular,
                "Angular vectors should be added even if spaces differ.");
        }

        // B1: a designer's zero-velocity "stop"/brake clip (SetContinuous, 0) is byte-identical to
        // default(PhysicsVelocityData). Before the Present marker the mixer treated it as an empty slot, so the brake
        // lost every crossfade against another clip even when it was the dominant weight. The blend framework hands
        // the dominant slot as `a` with s = the OTHER slot's weight (< 0.5), so Lerp(stop, other, 0.4) is the exact
        // discrete pick the bug corrupted.
        [Test]
        public void VelocityMixer_DominantZeroStopClip_WinsDiscretePick()
        {
            var stop = new PhysicsVelocityData
            {
                Mode = PhysicsVelocityMode.SetContinuous, Linear = float3.zero, Present = 1,
            };
            var other = new PhysicsVelocityData
            {
                Mode = PhysicsVelocityMode.AddContinuous, Linear = new float3(5, 0, 0), Present = 1,
            };

            var result = new PhysicsVelocityMixer().Lerp(stop, other, 0.4f);

            Assert.AreEqual(PhysicsVelocityMode.SetContinuous, result.Mode,
                "The dominant zero-velocity stop clip must win the discrete Mode, not be discarded as an empty slot.");
        }

        // B1 (DiscreteMixer consumer): an all-zero filter override ("belongs to nothing / collides with nothing" =
        // phase through everything) is byte-identical to default apart from the Present marker. Without it the
        // dominant phase-through clip loses the crossfade to any non-zero filter clip.
        [Test]
        public void FilterOverrideMixer_DominantPhaseThrough_WinsDiscretePick()
        {
            var phaseThrough = new PhysicsFilterOverrideData
            {
                BelongsToOverride = 0, CollidesWithOverride = 0, Present = 1,
            };
            var solid = new PhysicsFilterOverrideData
            {
                BelongsToOverride = 5, CollidesWithOverride = 5, Present = 1,
            };

            var result = new DiscreteMixer<PhysicsFilterOverrideData>().Lerp(phaseThrough, solid, 0.4f);

            Assert.AreEqual(0u, result.BelongsToOverride,
                "The dominant all-zero phase-through override must win the blend, not be treated as an empty slot.");
        }

        // Item 1: an eased velocity-clamp clip blends against the empty-slot default. In the kernel a max speed of 0
        // is the STRONGEST clamp (freeze), so lerping toward the zeroed default froze the body at the blend edges
        // (lerp(0, 10, 0.1) = 1). With the Present marker the empty slot relaxes the clamp by weight (max / weight):
        // near-authored at high weight, rising toward "no clamp" as the clip fades to 0.
        [Test]
        public void VelocityClampMixer_PartialWeightAgainstEmpty_RelaxesInsteadOfFreezing()
        {
            var clip = new PhysicsVelocityClampData { MaxLinearSpeed = 10f, MaxAngularSpeed = 5f, Present = 1 };
            var empty = default(PhysicsVelocityClampData); // Present == 0

            // High clip weight (single-clip partial fold hands the clip as a, default as b with s = default weight).
            var highWeight = new PhysicsVelocityClampMixer().Lerp(clip, empty, 0.1f);
            Assert.AreEqual(10f / 0.9f, highWeight.MaxLinearSpeed, 1e-3f,
                "At 90% clip weight the clamp is near its authored max, not frozen toward 0.");

            // Low clip weight -> the clamp relaxes far past the authored max (effect vanishes at the blend edge).
            var lowWeight = new PhysicsVelocityClampMixer().Lerp(clip, empty, 0.9f);
            Assert.Greater(lowWeight.MaxLinearSpeed, highWeight.MaxLinearSpeed,
                "Lower clip weight must relax the clamp further (higher max speed), never drag it toward the freeze.");

            // Empty as slot a (the 2-clip fold hands the default as a with a low s = present weight).
            var emptyAsA = new PhysicsVelocityClampMixer().Lerp(empty, clip, 0.1f);
            Assert.Greater(emptyAsA.MaxLinearSpeed, 10f,
                "The empty slot must relax the clamp regardless of which argument it lands in.");

            Assert.AreEqual(1, lowWeight.Present, "Present must propagate through the blend.");
        }

        [Test]
        public void VelocityClampMixer_BothPresent_LerpsNormallyAndKeepsAuthoredFreeze()
        {
            var slow = new PhysicsVelocityClampData { MaxLinearSpeed = 10f, MaxAngularSpeed = 4f, Present = 1 };
            var fast = new PhysicsVelocityClampData { MaxLinearSpeed = 20f, MaxAngularSpeed = 8f, Present = 1 };

            var mid = new PhysicsVelocityClampMixer().Lerp(slow, fast, 0.5f);
            Assert.AreEqual(15f, mid.MaxLinearSpeed, 1e-4f, "Two present clips must still lerp normally.");

            // An authored freeze (max 0, Present = 1) is a real value and must survive against another authored clip.
            var freeze = new PhysicsVelocityClampData { MaxLinearSpeed = 0f, MaxAngularSpeed = 0f, Present = 1 };
            var freezeBlend = new PhysicsVelocityClampMixer().Lerp(freeze, freeze, 0.5f);
            Assert.AreEqual(0f, freezeBlend.MaxLinearSpeed, "An authored freeze between two present clips is preserved.");
        }

        // Item 2: an eased Impulse force clip blends against the empty-slot default. Without the Present marker the
        // default's discrete Mode (Continuous) won at s >= 0.5, so the clip integrated a spurious continuous force
        // during its low-weight prelude. The discrete fields must always come from the present side.
        [Test]
        public void ForceMixer_EasedImpulseAgainstEmpty_KeepsImpulseDiscreteFields()
        {
            var impulse = new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.TowardTarget,
                Magnitude = 100f,
                Present = 1,
            };
            var empty = default(PhysicsForceData); // Present == 0, Mode == Continuous

            // Low clip weight -> high default weight (s), the exact prelude the bug corrupted.
            var prelude = new PhysicsForceMixer().Lerp(impulse, empty, 0.9f);
            Assert.AreEqual(PhysicsForceMode.Impulse, prelude.Mode,
                "The present Impulse clip must keep its Mode; the empty default must never inject Continuous.");
            Assert.AreEqual(PhysicsForceDirectionMode.TowardTarget, prelude.DirectionMode,
                "Discrete DirectionMode must come from the present side.");
            Assert.AreEqual(1, prelude.Present, "Present must propagate.");

            // Empty as slot a with low s (the 2-clip fold order).
            var emptyAsA = new PhysicsForceMixer().Lerp(empty, impulse, 0.1f);
            Assert.AreEqual(PhysicsForceMode.Impulse, emptyAsA.Mode,
                "The present side wins the discrete Mode regardless of argument order.");
        }

        [Test]
        public void ForceMixer_AdditiveWithEmptyDefault_TakesPresentDiscreteFields()
        {
            // The additive fold calls Add(defaultValue, result) with the empty default in slot a.
            var empty = default(PhysicsForceData);
            var result = new PhysicsForceData { Mode = PhysicsForceMode.Impulse, Magnitude = 50f, Present = 1 };

            var added = new PhysicsForceMixer().Add(empty, result);
            Assert.AreEqual(PhysicsForceMode.Impulse, added.Mode,
                "Add must pull discrete fields from the present side, not the empty default in slot a.");
            Assert.AreEqual(50f, added.Magnitude, 1e-4f, "Numeric fields still sum (empty contributes 0).");
        }

        // Item 3: PhysicsVelocityMixer previously dropped the Present flag from its outputs, so a mixed intermediate
        // read as an empty slot in a 3+ clip fold and lost its discrete fields.
        [Test]
        public void VelocityMixer_Outputs_PreservePresentFlag()
        {
            var a = new PhysicsVelocityData { Mode = PhysicsVelocityMode.SetInstant, Present = 1 };
            var b = new PhysicsVelocityData { Mode = PhysicsVelocityMode.AddContinuous, Present = 1 };

            Assert.AreEqual(1, new PhysicsVelocityMixer().Lerp(a, b, 0.5f).Present,
                "Lerp must propagate Present so the intermediate isn't mistaken for an empty slot.");
            Assert.AreEqual(1, new PhysicsVelocityMixer().Add(a, b).Present,
                "Add must propagate Present as well.");
        }

        // Item 4: gravity-override blending used the wrong neutral. The neutral for a gravity SCALE is 1 (normal
        // gravity), so an empty slot must pull the scale toward 1, not toward the default 0 (zero-G).
        [Test]
        public void GravityOverrideMixer_PartialWeightAgainstEmpty_BlendsTowardOne()
        {
            var heavy = new PhysicsGravityOverrideData { GravityScale = 2f, RestoreOnExit = true, Present = 1 };
            var empty = default(PhysicsGravityOverrideData); // Present == 0, GravityScale == 0

            var mostlyEmpty = new PhysicsGravityOverrideMixer().Lerp(heavy, empty, 0.9f);
            Assert.AreEqual(1.1f, mostlyEmpty.GravityScale, 1e-4f,
                "A mostly-empty blend must ease toward normal gravity (1), not toward zero-G (0).");
            Assert.IsTrue(mostlyEmpty.RestoreOnExit, "RestoreOnExit must come from the present side.");

            var emptyAsA = new PhysicsGravityOverrideMixer().Lerp(empty, heavy, 0.1f);
            Assert.AreEqual(1.1f, emptyAsA.GravityScale, 1e-4f,
                "Neutral-toward-1 must hold regardless of which argument is the empty slot.");
        }

        // Item 5: SocketReturnMixer slerped LocalRotation against the zero quaternion at partial weight, producing a
        // non-unit rotation goal, and lerped the halflives/attach distance toward 0 (max stiffness / unreachable).
        [Test]
        public void SocketReturnMixer_PartialWeightAgainstEmpty_KeepsUnitRotationAndPositiveHalflife()
        {
            var clip = new SocketReturnData
            {
                Socket = Target.Owner,
                LocalRotation = quaternion.AxisAngle(math.up(), 1.2f),
                PositionHalflife = 0.2f,
                RotationHalflife = 0.15f,
                AttachDistance = 0.5f,
                Present = 1,
            };
            var empty = default(SocketReturnData); // Present == 0, zero quaternion

            var blend = new SocketReturnMixer().Lerp(clip, empty, 0.5f);

            Assert.AreEqual(1f, math.length(blend.LocalRotation.value), 1e-4f,
                "The rotation goal must stay unit-length, never slerp toward the zero quaternion.");
            Assert.AreEqual(0.2f, blend.PositionHalflife, 1e-4f,
                "An empty slot must fall back to the present halflife, not drag it toward 0 (infinite stiffness).");
            Assert.AreEqual(0.5f, blend.AttachDistance, 1e-4f,
                "AttachDistance must fall back to the present value, not lerp toward an unreachable 0.");
            Assert.AreEqual(1, blend.Present, "Present must propagate.");
        }

        #endregion

        #region Force Accumulator Edge Cases

        [Test]
        public void Accumulator_MissingLocalToWorld_SkipsEntity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, 10, 0) });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var postForceBuffer = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(1, postForceBuffer.Length,
                "Entity without LocalToWorld should be ignored by the system, leaving buffer untouched");
        }

        [Test]
        public void Accumulator_MissingPhysicsMass_DefaultsToKinematic()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });

            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, 10, 0), Angular = float3.zero });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(0f, vel.Linear.y,
                "Forces should not affect entities without PhysicsMass (defaults to kinematic)");
        }

        [Test]
        public void Accumulator_InfiniteMass_ForcesDoNotAffectVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 0f, InverseInertia = float3.zero, Transform = RigidTransform.identity });

            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, 10, 0), Angular = float3.zero });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(0f, vel.Linear.y, "Forces should not affect entities with infinite mass (InverseMass = 0)");
        }

        [Test]
        public void Accumulator_InfiniteMass_VelocityDeltasStillAffectVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 0f, InverseInertia = float3.zero, Transform = RigidTransform.identity });

            Manager.AddBuffer<PendingForce>(target);
            var velBuffer = Manager.AddBuffer<PendingVelocity>(target);
            velBuffer.Add(new PendingVelocity { Linear = new float3(0, 10, 0), Angular = float3.zero });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10f, vel.Linear.y, "Velocity deltas SHOULD affect entities even with infinite mass");
        }

        #endregion

        #region Velocity Override Edge Cases

        [Test]
        public void VelocityOverride_MissingStat_DefaultsToMultiplierOne()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(0, 5, 0),
                    Strength = new StatSource { Stat = 999, Link = new EntityLinkRef { ReadRootFrom = Target.Self } }
                }
            });

            var sys = World.GetOrCreateSystem<PhysicsVelocityOverrideSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel.Linear.y, "Should default to multiplier 1 if stats are missing");
        }

        [Test]
        public void VelocityOverride_InvalidTarget_DefaultsToSelf()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(0, 5, 0),
                    Space = Target.Owner
                }
            });

            var sys = World.GetOrCreateSystem<PhysicsVelocityOverrideSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel.Linear.y);
        }

        #endregion

        #region Extreme Values

        [Test]
        public void VelocityOverride_NaNInConfig_IsRejected()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(0, float.NaN, 0)
                }
            });

            var sys = World.GetOrCreateSystem<PhysicsVelocityOverrideSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            // PhysicsVelocityOverrideSystem.OverrideJob guards `math.isfinite` and skips non-finite writes, so a NaN
            // in the config must NOT poison the body's velocity — it stays at its prior (zero) value.
            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.IsFalse(float.IsNaN(vel.Linear.y), "NaN in config must be rejected, not written to velocity");
            Assert.AreEqual(0f, vel.Linear.y, "velocity should be unchanged when the override is non-finite");
        }

        [Test]
        public void Accumulator_ExtremeForces_HandlesLargeValues()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, float.MaxValue, 0), Angular = float3.zero });
            forceBuffer.Add(new PendingForce { Linear = new float3(0, float.MaxValue, 0), Angular = float3.zero });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.IsTrue(float.IsInfinity(vel.Linear.y), "Two MaxValues should result in Infinity");
        }

        #endregion

        #region System Interactions

        [Test]
        public void Override_Overwrites_AccumulatedForcesInSameGroup()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            Manager.AddBuffer<PendingForce>(target);

            var forces = Manager.GetBuffer<PendingForce>(target);
            forces.Add(new PendingForce { Linear = new float3(0, 10, 0) });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(5, 0, 0)
                }
            });

            var accumulatorSys = World.GetOrCreateSystem<PhysicsModifierForceAccumulatorSystem>();
            var overrideSys = World.GetOrCreateSystem<PhysicsVelocityOverrideSystem>();

            accumulatorSys.Update(WorldUnmanaged);
            overrideSys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel.Linear.x);
            Assert.AreEqual(0f, vel.Linear.y, "Override should have overwritten the accumulated force");

            Manager.SetComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.GetBuffer<PendingForce>(target).Add(new PendingForce { Linear = new float3(0, 10, 0) });

            overrideSys.Update(WorldUnmanaged);
            accumulatorSys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel2 = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel2.Linear.x);
            Assert.AreEqual(10f, vel2.Linear.y, "Accumulator should have added to the overridden velocity");
        }

        [Test]
        public void FullFrame_VelocityAccumulationOrder()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, new PhysicsForceState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            Manager.AddBuffer<PendingForce>(target);
            Manager.AddBuffer<PendingVelocity>(target);

            Manager.AddComponentData(target, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Impulse,
                    Linear = new float3(0, 10, 0)
                }
            });

            var producerApply = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            var producerAccumulator = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();

            World.SetTime(new TimeData(0.1, 0.1f));
            producerApply.Update(WorldUnmanaged);
            producerAccumulator.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var velAfterProducer = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10f, velAfterProducer.Linear.y, "Producer should have added 10");

            velAfterProducer.Linear.y -= 2f;
            Manager.SetComponentData(target, velAfterProducer);

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(5, 0, 0)
                }
            });

            var forces = Manager.GetBuffer<PendingForce>(target);
            forces.Add(new PendingForce { Linear = new float3(0, 0, 7) });

            var overrideSys = World.GetOrCreateSystem<PhysicsVelocityOverrideSystem>();
            var modifierAccumulator = World.GetOrCreateSystem<PhysicsModifierForceAccumulatorSystem>();

            overrideSys.Update(WorldUnmanaged);
            modifierAccumulator.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var finalVel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, finalVel.Linear.x);
            Assert.AreEqual(0f, finalVel.Linear.y);
            Assert.AreEqual(7f, finalVel.Linear.z);
        }

        #endregion
    }
}