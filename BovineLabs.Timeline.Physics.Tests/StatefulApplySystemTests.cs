using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Filters;
using BovineLabs.Timeline.Physics.Gravities;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.VelocityClamps;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using SphereCollider = Unity.Physics.SphereCollider;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class StatefulApplySystemTests : ECSTestsFixture
    {
        [Test]
        public void VelocityClamp_Active_ClampsLinearAndAngularSpeed()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new PhysicsVelocity
            {
                Linear = new float3(10f, 0f, 0f),
                Angular = new float3(0f, 5f, 0f)
            });
            Manager.AddComponentData(body, new PhysicsVelocityClampState { Fired = false });
            Manager.AddComponentData(body, new ActiveVelocityClamp
            {
                Config = new PhysicsVelocityClampData { MaxLinearSpeed = 3f, MaxAngularSpeed = 2f }
            });

            var sys = World.GetOrCreateSystem<PhysicsVelocityClampApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(3f, math.length(velocity.Linear), 0.0001f);
            Assert.AreEqual(2f, math.length(velocity.Angular), 0.0001f);
            Assert.IsTrue(Manager.GetComponentData<PhysicsVelocityClampState>(body).Fired);
        }

        [Test]
        public void VelocityClamp_NegativeLimits_LeaveVelocityUntouched()
        {
            var body = Manager.CreateEntity();
            var initial = new PhysicsVelocity
            {
                Linear = new float3(10f, 0f, 0f),
                Angular = new float3(0f, 5f, 0f)
            };
            Manager.AddComponentData(body, initial);
            Manager.AddComponentData(body, new PhysicsVelocityClampState { Fired = false });
            Manager.AddComponentData(body, new ActiveVelocityClamp
            {
                Config = new PhysicsVelocityClampData { MaxLinearSpeed = -1f, MaxAngularSpeed = -1f }
            });

            var sys = World.GetOrCreateSystem<PhysicsVelocityClampApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(initial.Linear, velocity.Linear);
            Assert.AreEqual(initial.Angular, velocity.Angular);
        }

        [Test]
        public void VelocityClamp_Inactive_ResetsFiredAndSkipsClamp()
        {
            var body = Manager.CreateEntity();
            var initial = new PhysicsVelocity { Linear = new float3(10f, 0f, 0f) };
            Manager.AddComponentData(body, initial);
            Manager.AddComponentData(body, new PhysicsVelocityClampState { Fired = true });
            Manager.AddComponentData(body, new ActiveVelocityClamp
            {
                Config = new PhysicsVelocityClampData { MaxLinearSpeed = 3f, MaxAngularSpeed = 3f }
            });
            Manager.SetComponentEnabled<ActiveVelocityClamp>(body, false);

            var sys = World.GetOrCreateSystem<PhysicsVelocityClampApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(initial.Linear, Manager.GetComponentData<PhysicsVelocity>(body).Linear);
            Assert.IsFalse(Manager.GetComponentData<PhysicsVelocityClampState>(body).Fired);
        }

        [Test]
        public void GravityOverride_ExistingFactor_OverridesThenRestoresOnExit()
        {
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new PhysicsGravityFactor { Value = 1f });
            Manager.AddComponentData(body, new PhysicsGravityOverrideState());
            Manager.AddComponentData(body, new ActiveGravityOverride
            {
                Config = new PhysicsGravityOverrideData { GravityScale = 0.25f, RestoreOnExit = true }
            });

            var sys = World.GetOrCreateSystem<PhysicsGravityOverrideApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(0.25f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 0.0001f);
            var state = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsTrue(state.Fired);
            Assert.AreEqual(1f, state.OriginalGravityScale, 0.0001f);

            Manager.SetComponentEnabled<ActiveGravityOverride>(body, false);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(1f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 0.0001f);
            Assert.IsFalse(Manager.GetComponentData<PhysicsGravityOverrideState>(body).Fired);
        }

        [Test]
        public void GravityOverride_MissingFactor_AddsComponentThroughEcb()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new PhysicsGravityOverrideState());
            Manager.AddComponentData(body, new ActiveGravityOverride
            {
                Config = new PhysicsGravityOverrideData { GravityScale = 0.25f, RestoreOnExit = true }
            });

            var sys = World.GetOrCreateSystem<PhysicsGravityOverrideApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            ecbSystem.Update();

            Assert.IsTrue(Manager.HasComponent<PhysicsGravityFactor>(body));
            Assert.AreEqual(0.25f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 0.0001f);
            Assert.IsTrue(Manager.GetComponentData<PhysicsGravityOverrideState>(body).AddedComponent);
        }

        [Test]
        public void KinematicOverride_AddsMassOverrideAndZeroesVelocity_ThenRemovesOnExit()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new PhysicsVelocity { Linear = new float3(4f, 0f, 0f) });
            Manager.AddComponentData(body, new PhysicsKinematicOverrideState());
            Manager.AddComponentData(body, new ActiveKinematicOverride
            {
                Config = new PhysicsKinematicOverrideData
                {
                    IsKinematic = true,
                    ZeroVelocityOnEnter = true,
                    ZeroGravity = false
                }
            });

            var sys = World.GetOrCreateSystem<PhysicsKinematicOverrideApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            ecbSystem.Update();

            Assert.AreEqual(float3.zero, Manager.GetComponentData<PhysicsVelocity>(body).Linear);
            Assert.IsTrue(Manager.HasComponent<PhysicsMassOverride>(body));
            Assert.AreEqual(1, Manager.GetComponentData<PhysicsMassOverride>(body).IsKinematic);

            Manager.SetComponentEnabled<ActiveKinematicOverride>(body, false);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            ecbSystem.Update();

            Assert.IsFalse(Manager.HasComponent<PhysicsMassOverride>(body));
            Assert.IsFalse(Manager.GetComponentData<PhysicsKinematicOverrideState>(body).Fired);
        }

        [Test]
        public void KinematicOverride_DisabledGravityOverride_DoesNotBlockZeroGravity()
        {
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new PhysicsGravityFactor { Value = 0.75f });
            Manager.AddComponentData(body, new PhysicsKinematicOverrideState());
            Manager.AddComponentData(body, new ActiveKinematicOverride
            {
                Config = new PhysicsKinematicOverrideData
                {
                    IsKinematic = true,
                    ZeroGravity = true
                }
            });
            Manager.AddComponentData(body, new ActiveGravityOverride());
            Manager.SetComponentEnabled<ActiveGravityOverride>(body, false);

            var sys = World.GetOrCreateSystem<PhysicsKinematicOverrideApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(0f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 0.0001f);

            Manager.SetComponentEnabled<ActiveKinematicOverride>(body, false);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(0.75f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 0.0001f);
        }

        [Test]
        public void FilterOverride_UniqueCollider_OverridesThenRestoresOnExit()
        {
            var blob = SphereCollider.Create(
                new SphereGeometry { Center = float3.zero, Radius = 0.5f },
                CollisionFilter.Default);

            try
            {
                var body = Manager.CreateEntity();
                Manager.AddComponentData(body, new PhysicsCollider { Value = blob });
                Manager.AddComponentData(body, new PhysicsFilterOverrideState());
                Manager.AddComponentData(body, new ActiveFilterOverride
                {
                    Config = new PhysicsFilterOverrideData
                    {
                        BelongsToOverride = 0x2u,
                        CollidesWithOverride = 0x4u,
                        RestoreOnExit = true
                    }
                });

                var sys = World.GetOrCreateSystem<PhysicsFilterOverrideApplySystem>();
                sys.Update(WorldUnmanaged);
                Manager.CompleteAllTrackedJobs();

                var filter = Manager.GetComponentData<PhysicsCollider>(body).Value.Value.GetCollisionFilter();
                Assert.AreEqual(0x2u, filter.BelongsTo);
                Assert.AreEqual(0x4u, filter.CollidesWith);

                var state = Manager.GetComponentData<PhysicsFilterOverrideState>(body);
                Assert.IsTrue(state.Fired);
                Assert.AreEqual(CollisionFilter.Default.BelongsTo, state.OriginalBelongsTo);
                Assert.AreEqual(CollisionFilter.Default.CollidesWith, state.OriginalCollidesWith);

                Manager.SetComponentEnabled<ActiveFilterOverride>(body, false);
                sys.Update(WorldUnmanaged);
                Manager.CompleteAllTrackedJobs();

                filter = Manager.GetComponentData<PhysicsCollider>(body).Value.Value.GetCollisionFilter();
                Assert.AreEqual(CollisionFilter.Default.BelongsTo, filter.BelongsTo);
                Assert.AreEqual(CollisionFilter.Default.CollidesWith, filter.CollidesWith);
                Assert.IsFalse(Manager.GetComponentData<PhysicsFilterOverrideState>(body).Fired);
            }
            finally
            {
                blob.Dispose();
            }
        }
    }
}