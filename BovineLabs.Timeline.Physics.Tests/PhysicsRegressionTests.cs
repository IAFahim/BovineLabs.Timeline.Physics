using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Authoring;
using BovineLabs.Timeline.Physics.Chains;
using BovineLabs.Timeline.Physics.PIDs;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PidTargetResolutionTests : ECSTestsFixture
    {
        [Test]
        public void LinearPid_TracksValidTargetAtWorldOrigin()
        {
            var body = CreateBody(new float3(5f, 0f, 0f));
            var target = CreateTransformEntity(float3.zero, quaternion.identity);
            Manager.AddComponentData(body, new Targets { Target = target });
            Manager.AddComponentData(body, new PhysicsLinearPIDState());
            Manager.AddComponentData(body, new ActiveLinearPid
            {
                Config = new PhysicsLinearPIDData
                {
                    Tuning = ProportionalTuning(),
                    TrackingTarget = Target.Target,
                    TargetMode = PidLinearTargetMode.TargetLocal,
                    Strength = 1f
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var system = World.GetOrCreateSystem<PhysicsPidApplySystem>();
            system.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var forces = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, forces.Length);
            Assert.Less(forces[0].Linear.x, 0f);
        }

        [Test]
        public void AngularPid_MatchesValidTargetRotationAtWorldOrigin()
        {
            var body = CreateBody(new float3(5f, 0f, 0f));
            var target = CreateTransformEntity(float3.zero, quaternion.RotateY(math.PI * 0.5f));
            Manager.AddComponentData(body, new Targets { Target = target });
            Manager.AddComponentData(body, new PhysicsAngularPIDState());
            Manager.AddComponentData(body, new ActiveAngularPid
            {
                Config = new PhysicsAngularPIDData
                {
                    Tuning = ProportionalTuning(),
                    TrackingTarget = Target.Target,
                    TargetMode = PidAngularTargetMode.MatchTarget,
                    TargetRotation = quaternion.identity,
                    Strength = 1f
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var system = World.GetOrCreateSystem<PhysicsPidApplySystem>();
            system.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var forces = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, forces.Length);
            Assert.Greater(math.lengthsq(forces[0].Angular), 0f);
        }

        private Entity CreateBody(float3 position)
        {
            var body = CreateTransformEntity(position, quaternion.identity);
            Manager.AddBuffer<PendingForce>(body);
            return body;
        }

        private Entity CreateTransformEntity(float3 position, quaternion rotation)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, LocalTransform.FromPositionRotation(position, rotation));
            Manager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(position, rotation, 1f)
            });
            return entity;
        }

        private static PidTuning ProportionalTuning()
        {
            return new PidTuning
            {
                Proportional = new float3(1f),
                MaxOutput = 100f
            };
        }
    }

    public class ChainReleaseSystemTests : ECSTestsFixture
    {
        [Test]
        public void Release_RearmsAndClearsGrabbedState()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var link = Manager.CreateEntity(typeof(ChainGrabArmed), typeof(ChainLinkGrabbed));
            Manager.SetComponentEnabled<ChainGrabArmed>(link, false);
            Manager.SetComponentEnabled<ChainLinkGrabbed>(link, true);

            var joint = Manager.CreateEntity();
            var root = Manager.CreateEntity(typeof(ChainReleaseRequest));
            Manager.AddBuffer<ChainAnchor>(root).Add(new ChainAnchor { Joint = joint, Link = link });

            var system = World.GetOrCreateSystem<ChainReleaseSystem>();
            system.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            ecbSystem.Update();

            Assert.IsFalse(Manager.Exists(joint));
            Assert.IsTrue(Manager.IsComponentEnabled<ChainGrabArmed>(link));
            Assert.IsFalse(Manager.IsComponentEnabled<ChainLinkGrabbed>(link));
            Assert.IsFalse(Manager.IsComponentEnabled<ChainReleaseRequest>(root));
            Assert.AreEqual(0, Manager.GetBuffer<ChainAnchor>(root).Length);
        }
    }

    public class AutoPhysicsForceAccumulatorBakingSystemTests : ECSTestsFixture
    {
        [Test]
        public void AddsOnlyMissingBuffersAndHonorsOptOut()
        {
            var missingBoth = Manager.CreateEntity(typeof(PhysicsVelocity));

            var missingVelocity = Manager.CreateEntity(typeof(PhysicsVelocity));
            Manager.AddBuffer<PendingForce>(missingVelocity);

            var missingForce = Manager.CreateEntity(typeof(PhysicsVelocity));
            var existingVelocity = Manager.AddBuffer<PendingVelocity>(missingForce);
            existingVelocity.Add(new PendingVelocity { Linear = new float3(1f, 0f, 0f) });

            var optedOut = Manager.CreateEntity(typeof(PhysicsVelocity), typeof(PhysicsForceAccumulatorOptOut));

            var system = World.GetOrCreateSystem<AutoPhysicsForceAccumulatorBakingSystem>();
            system.Update(WorldUnmanaged);

            Assert.IsTrue(Manager.HasBuffer<PendingForce>(missingBoth));
            Assert.IsTrue(Manager.HasBuffer<PendingVelocity>(missingBoth));
            Assert.IsTrue(Manager.HasBuffer<PendingForce>(missingVelocity));
            Assert.IsTrue(Manager.HasBuffer<PendingVelocity>(missingVelocity));
            Assert.IsTrue(Manager.HasBuffer<PendingForce>(missingForce));
            Assert.AreEqual(1, Manager.GetBuffer<PendingVelocity>(missingForce).Length);
            Assert.IsFalse(Manager.HasBuffer<PendingForce>(optedOut));
            Assert.IsFalse(Manager.HasBuffer<PendingVelocity>(optedOut));
        }
    }
}
