using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Kinematics;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsKinematicsApplySystemTests : ECSTestsFixture
    {
        [Test]
        public void AddContinuous_AppendsPendingVelocityEveryFrame()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            // AddContinuous integrates against clip-active time (ElapsedTime - AppliedTime), mirroring
            // PhysicsForceState — the track system accumulates ElapsedTime at render rate.
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false, ElapsedTime = 0.1f });
            Manager.AddBuffer<PendingVelocity>(target);

            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.AddContinuous,
                    Linear = new float3(0, 5, 0),
                    Angular = new float3(0, 0, 1),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingVelocity>(target);
            Assert.AreEqual(1, pendings.Length, "Should have appended exactly one PendingVelocity");

            var dt = WorldUnmanaged.Time.DeltaTime;
            Assert.AreEqual(5f * dt, pendings[0].Linear.y, 0.001f);
            Assert.AreEqual(1f * dt, pendings[0].Angular.z, 0.001f);

            var state = Manager.GetComponentData<PhysicsVelocityState>(target);
            Assert.IsFalse(state.Fired, "AddContinuous should not set Fired to true");
        }

        [Test]
        public void AddInstant_AppendsPendingForceOnce()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsForceState { Fired = false });
            Manager.AddBuffer<PendingForce>(target);

            Manager.AddComponentData(target, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Impulse,
                    Linear = new float3(10, 0, 0),
                    Angular = new float3(0, 2, 0),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(1, pendings.Length, "Should have appended exactly one PendingForce on first frame");
            Assert.AreEqual(10f, pendings[0].Linear.x, 0.001f);
            Assert.AreEqual(2f, pendings[0].Angular.y, 0.001f);

            var state = Manager.GetComponentData<PhysicsForceState>(target);
            Assert.IsTrue(state.Fired, "Impulse should set Fired to true");

            pendings.Clear();

            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            pendings = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(0, pendings.Length, "Should not append again because it was fired");
        }

        [Test]
        public void AddContinuous_AwayFromTarget_AppendsForceInCorrectDirection()
        {
            var body = Manager.CreateEntity();
            var targetEntity = Manager.CreateEntity();

            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            // Continuous integrates against clip-active time; the track accumulates ElapsedTime at render rate.
            Manager.AddComponentData(body, new PhysicsForceState { Fired = false, ElapsedTime = 0.1f });
            Manager.AddBuffer<PendingForce>(body);

            Manager.AddComponentData(targetEntity, LocalTransform.FromPosition(new float3(5, 0, 0)));
            Manager.AddComponentData(targetEntity,
                new LocalToWorld { Value = float4x4.Translate(new float3(5, 0, 0)) });

            Manager.AddComponentData(body, new Targets { Target = targetEntity });

            Manager.AddComponentData(body, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Continuous,
                    DirectionMode = PhysicsForceDirectionMode.AwayFromTarget,
                    DirectionTarget = Target.Target,
                    Magnitude = 10f,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, pendings.Length);

            Assert.AreEqual(-1f, pendings[0].Linear.x, 0.001f);
            Assert.AreEqual(0f, pendings[0].Linear.y, 0.001f);
            Assert.AreEqual(0f, pendings[0].Linear.z, 0.001f);
        }

        // Reproduces the dash bug + locks in the fix: a Continuous force's total impulse must equal
        // force × clip-active-duration, independent of how many fixed steps land inside the window. Pre-fix the
        // consumer used the fixed-step DeltaTime (force × dt × step-count) so the total jittered with the step
        // count (which varies run-to-run because the fixed-step group ticks a variable number of times per
        // rendered frame) — making continuous dashes non-deterministic while impulse stayed reliable.
        [Test]
        public void Continuous_TotalImpulse_IsClipDurationBased_NotFixedStepCount()
        {
            const float force = 10f;
            const float clipDuration = 0.10f;

            var totalFewSteps = SumContinuousForceX(force, clipDuration, 2);
            var totalManySteps = SumContinuousForceX(force, clipDuration, 10);

            Assert.AreEqual(force * clipDuration, totalFewSteps, 1e-4f, "total must be force × duration");
            Assert.AreEqual(totalFewSteps, totalManySteps, 1e-4f, "total must not depend on fixed-step count");
        }

        private float SumContinuousForceX(float force, float clipDuration, int steps)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsForceState { Fired = false });
            Manager.AddBuffer<PendingForce>(body);
            Manager.AddComponentData(body, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Continuous,
                    DirectionMode = PhysicsForceDirectionMode.FixedVector,
                    Linear = new float3(force, 0, 0),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0, 0.016f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();

            var total = 0f;
            var perStepElapsed = clipDuration / steps;
            for (var s = 0; s < steps; s++)
            {
                // Simulate the render-rate track accumulating clip-active time before each fixed-step consume.
                var state = Manager.GetComponentData<PhysicsForceState>(body);
                state.ElapsedTime += perStepElapsed;
                Manager.SetComponentData(body, state);

                sys.Update(WorldUnmanaged);
                Manager.CompleteAllTrackedJobs();

                var pendings = Manager.GetBuffer<PendingForce>(body);
                for (var p = 0; p < pendings.Length; p++) total += pendings[p].Linear.x;
                pendings.Clear();
            }

            Manager.DestroyEntity(body);
            return total;
        }
    }
}