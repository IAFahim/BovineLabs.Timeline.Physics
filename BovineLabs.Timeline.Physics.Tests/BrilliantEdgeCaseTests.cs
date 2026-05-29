using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Essence.Data;
using BovineLabs.Core.PhysicsStates;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class BrilliantEdgeCaseTests : ECSTestsFixture
    {
        #region Force Accumulator Edge Cases

        [Test]
        public void Accumulator_MissingLocalToWorld_SkipsEntity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
            
            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, 10, 0) });

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var postForceBuffer = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(1, postForceBuffer.Length, "Entity without LocalToWorld should be ignored by the system, leaving buffer untouched");
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
            Assert.AreEqual(0f, vel.Linear.y, "Forces should not affect entities without PhysicsMass (defaults to kinematic)");
        }

        [Test]
        public void Accumulator_InfiniteMass_ForcesDoNotAffectVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 0f, InverseInertia = float3.zero, Transform = RigidTransform.identity });
            
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
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 0f, InverseInertia = float3.zero, Transform = RigidTransform.identity });
            
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
                    Strength = new StatStrengthConfig { Stat = (StatKey)999, ReadFrom = Target.Self }
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

        #region Extreme Values

        [Test]
        public void VelocityOverride_NaNInConfig_PropagatesNaN()
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

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.IsTrue(float.IsNaN(vel.Linear.y), "NaN should propagate (system should not crash, but output is NaN)");
        }

        [Test]
        public void Accumulator_ExtremeForces_HandlesLargeValues()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
            
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
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });

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
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
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
            Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < entities.Length; i++)
            {
                Manager.SetComponentData(entities[i], new LocalToWorld { Value = float4x4.identity });
                Manager.SetComponentData(entities[i], new PhysicsVelocity { Linear = float3.zero });
                Manager.SetComponentData(entities[i], new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
                
                var buffer = Manager.GetBuffer<PendingForce>(entities[i]);
                buffer.Add(new PendingForce { Linear = new float3(0, 1, 0) });
            }

            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            sw.Stop();

            UnityEngine.Debug.Log($"Accumulator System took {sw.Elapsed.TotalMilliseconds:F4}ms for 10,000 entities");
            Assert.Less(sw.Elapsed.TotalMilliseconds, 50.0, "System should be fast enough");

            entities.Dispose();
        }

        #endregion

        #region Drag Edge Cases

        [Test]
        public void Drag_NegativeDrag_CausesVelocityGrowth()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = new float3(0, 10, 0), Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1), Transform = RigidTransform.identity });
            
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
                ApplyTo = Target.Target
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
            Assert.AreEqual(new float3(1, 2, 0), result.Linear, "Linear vectors should be added even if spaces differ.");
            Assert.AreEqual(new float3(0, 1, 2), result.Angular, "Angular vectors should be added even if spaces differ.");
        }

        #endregion
    }
}
