using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Drags;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Kinematics;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    /// <summary>
    /// The external (knockback) velocity channel: a hit deposited into <see cref="PendingExternalForce"/> must ride
    /// <see cref="PhysicsVelocity"/> through the solver, then be removed so braking/drag/reset (which run after the
    /// solver) only ever see locomotion — and it must decay on its own so it still goes away.
    /// </summary>
    public class ExternalVelocityChannelTests : ECSTestsFixture
    {
        private float _savedDecayRate;

        // The decay-rate ConfigVar is only seeded by ConfigVarManager at runtime init, which does NOT run for this
        // EditMode asmdef — so pin it explicitly, or the SharedStatic reads 0 (no decay) and tests pass/fail by luck.
        [SetUp]
        public void PinDecayRate()
        {
            _savedDecayRate = ExternalVelocityConfig.DecayRate.Data;
            ExternalVelocityConfig.DecayRate.Data = 8f;
        }

        [TearDown]
        public void RestoreDecayRate()
        {
            ExternalVelocityConfig.DecayRate.Data = _savedDecayRate;
        }

        private Entity CreateBody(float3 intentVelocity)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsVelocity { Linear = intentVelocity, Angular = float3.zero });
            Manager.AddComponentData(body,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1, 1, 1), Transform = RigidTransform.identity });
            Manager.AddComponentData(body, default(ExternalVelocity));
            Manager.AddBuffer<PendingExternalForce>(body);

            Manager.AddComponent<PendingVelocityReset>(body);
            Manager.SetComponentEnabled<PendingVelocityReset>(body, false);
            Manager.AddBuffer<PendingForce>(body);
            Manager.AddBuffer<PendingVelocity>(body);
            return body;
        }

        private void RunCompose()
        {
            World.GetOrCreateSystem<PhysicsExternalVelocityComposeSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunDecompose()
        {
            World.GetOrCreateSystem<PhysicsExternalVelocityDecomposeSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunAccumulator()
        {
            World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunDrag()
        {
            World.GetOrCreateSystem<PhysicsDragApplySystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        [Test]
        public void Compose_DrainsInboxIntoChannel_AndRidesVelocity()
        {
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(new float3(2, 0, 0));
            Manager.GetBuffer<PendingExternalForce>(body).Add(new PendingExternalForce { Linear = new float3(0, 0, 10) });

            RunCompose();

            // InverseMass = 1, so a 10 impulse is a 10 velocity delta.
            Assert.AreEqual(10f, Manager.GetComponentData<ExternalVelocity>(body).Linear.z, 1e-4f);
            // Intent (2 on x) plus external (10 on z) ride together for the solver.
            var v = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(2f, v.x, 1e-4f);
            Assert.AreEqual(10f, v.z, 1e-4f);
            Assert.AreEqual(0, Manager.GetBuffer<PendingExternalForce>(body).Length, "Inbox must be drained");
        }

        [Test]
        public void Decompose_RemovesChannelFromVelocity_LeavingIntentOnly()
        {
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(new float3(2, 0, 0));
            Manager.SetComponentData(body, new ExternalVelocity { Linear = new float3(0, 0, 10) });
            // Velocity as it would be post-compose: intent + external.
            Manager.SetComponentData(body, new PhysicsVelocity { Linear = new float3(2, 0, 10) });

            RunDecompose();

            // Velocity is back to intent only -> drag/clamp/reset downstream never see the hit.
            var v = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(2f, v.x, 1e-4f);
            Assert.AreEqual(0f, v.z, 1e-4f);
            // Channel decayed but still alive (will re-apply next frame).
            var ext = Manager.GetComponentData<ExternalVelocity>(body).Linear.z;
            Assert.Less(ext, 10f);
            Assert.Greater(ext, 0f);
        }

        [Test]
        public void Knockback_SurvivesABrakeThatZeroesIntentVelocity()
        {
            // The whole point. Frame 1: hit lands. A brake (drag/override) then hard-zeroes PhysicsVelocity, exactly
            // as it would post-decompose. Frame 2: the hit must still be there.
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(float3.zero);
            Manager.GetBuffer<PendingExternalForce>(body).Add(new PendingExternalForce { Linear = new float3(0, 0, 10) });

            RunCompose();   // external now rides velocity
            RunDecompose(); // ...and is pulled back out, leaving channel standing

            // A brake nukes the (intent) velocity — simulating drag/override running after decompose.
            Manager.SetComponentData(body, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });

            RunCompose();   // next frame: the surviving channel re-applies
            var v = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.Greater(v.z, 0f, "Knockback must survive a brake that zeroed the intent velocity");
        }

        [Test]
        public void Channel_DecaysToZeroOverTime()
        {
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(float3.zero);
            Manager.SetComponentData(body, new ExternalVelocity { Linear = new float3(0, 0, 10) });

            // Pair compose+decompose each step (the real per-frame cycle): velocity returns to ~0 every frame while
            // the standing channel fades — so this also proves no intent-velocity drift accumulates.
            for (var i = 0; i < 200; i++)
            {
                RunCompose();
                RunDecompose();
            }

            Assert.AreEqual(0f, Manager.GetComponentData<ExternalVelocity>(body).Linear.z, 1e-4f,
                "External channel must fully decay to rest, not leave a permanent drift");
            Assert.AreEqual(0f, Manager.GetComponentData<PhysicsVelocity>(body).Linear.z, 1e-4f,
                "Intent velocity must not drift as the channel cycles");
        }

        [Test]
        public void Knockback_SurvivesTheRealDragSystem()
        {
            // Integration: the real PhysicsDragApplySystem (a heavy brake) runs against the post-decompose velocity.
            // It must NOT touch the external channel, so the hit re-applies undiminished next compose.
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(float3.zero);
            Manager.AddComponentData(body, new ActiveDrag
            {
                Config = new PhysicsDragData { Linear = 10f, Angular = 10f, Strength = default },
            });
            Manager.GetBuffer<PendingExternalForce>(body).Add(new PendingExternalForce { Linear = new float3(0, 0, 10) });

            RunCompose();   // hit -> external (10), rides velocity
            RunDecompose(); // velocity back to intent (0); external decays to ~4.49
            RunDrag();      // heavy brake on intent only

            var ext = Manager.GetComponentData<ExternalVelocity>(body).Linear.z;
            Assert.Greater(ext, 3f, "A heavy drag clip must not touch the external channel");

            RunCompose();   // the surviving channel re-applies to velocity
            var v = Manager.GetComponentData<PhysicsVelocity>(body).Linear.z;
            Assert.Greater(v, 3f, "Knockback must survive the real drag system, not be braked away");
        }

        [Test]
        public void ExternalReset_EatsAHitLandingTheSameFrame()
        {
            // Parry/super-armor: a VelocityResetFlags.External on the same frame a hit arrives must cancel BOTH the
            // standing channel and the fresh inbox impulse (else compose would resurrect the hit after the reset).
            World.SetTime(new TimeData(0.1, 0.1f));
            var body = CreateBody(float3.zero);
            Manager.SetComponentData(body, new ExternalVelocity { Linear = new float3(0, 0, 5) });   // residual
            Manager.GetBuffer<PendingExternalForce>(body).Add(new PendingExternalForce { Linear = new float3(0, 0, 10) }); // fresh hit
            Manager.SetComponentData(body, new PendingVelocityReset { Flags = VelocityResetFlags.External });
            Manager.SetComponentEnabled<PendingVelocityReset>(body, true);

            RunAccumulator(); // consumes the reset: clears standing channel + inbox
            RunCompose();      // drains inbox (now empty) and re-applies channel (now zero)

            Assert.AreEqual(0f, Manager.GetComponentData<ExternalVelocity>(body).Linear.z, 1e-4f,
                "Standing channel must be cleared");
            Assert.AreEqual(0, Manager.GetBuffer<PendingExternalForce>(body).Length,
                "The same-frame inbox hit must be eaten, not resurrected by compose");
            Assert.AreEqual(0f, Manager.GetComponentData<PhysicsVelocity>(body).Linear.z, 1e-4f,
                "No knockback should reach velocity through a parry");
        }

        [Test]
        public void Reset_DefaultLeavesChannelAlone_ExternalFlagClearsIt()
        {
            World.SetTime(new TimeData(0.1, 0.1f));

            // Default reset (Linear) zeroes intent but the incoming hit survives.
            var a = CreateBody(new float3(3, 0, 0));
            Manager.SetComponentData(a, new ExternalVelocity { Linear = new float3(0, 0, 7) });
            Manager.SetComponentData(a, new PendingVelocityReset { Flags = VelocityResetFlags.Linear });
            Manager.SetComponentEnabled<PendingVelocityReset>(a, true);

            // External-flagged reset (parry/super-armor) also wipes the hit.
            var b = CreateBody(new float3(3, 0, 0));
            Manager.SetComponentData(b, new ExternalVelocity { Linear = new float3(0, 0, 7) });
            Manager.SetComponentData(b, new PendingVelocityReset { Flags = VelocityResetFlags.External });
            Manager.SetComponentEnabled<PendingVelocityReset>(b, true);

            RunAccumulator();

            Assert.AreEqual(7f, Manager.GetComponentData<ExternalVelocity>(a).Linear.z, 1e-4f,
                "A plain reset must NOT eat the knockback channel");
            Assert.AreEqual(0f, Manager.GetComponentData<ExternalVelocity>(b).Linear.z, 1e-4f,
                "An External-flagged reset MUST clear the knockback channel");
        }

        // Item 4: a kinematic override with ZeroVelocityOnEnter freezes the body — but the external (knockback) channel
        // is re-added on top of PhysicsVelocity every producer tick, so zeroing only PhysicsVelocity leaves the body
        // sliding. The override must also clear ExternalVelocity AND its inbox so a frozen body actually stops.
        [Test]
        public void KinematicFreeze_ZeroVelocityOnEnter_ClearsExternalChannelAndInbox()
        {
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            World.SetTime(new TimeData(0.1, 0.1f));

            var body = CreateBody(float3.zero);
            Manager.SetComponentData(body, new ExternalVelocity { Linear = new float3(0, 0, 10) }); // standing knockback
            Manager.GetBuffer<PendingExternalForce>(body)
                .Add(new PendingExternalForce { Linear = new float3(0, 0, 5) }); // fresh same-frame hit

            // Give it PhysicsMassOverride so the override's OnEnter uses the in-place mass lane (no ECB playback needed).
            Manager.AddComponentData(body, new PhysicsMassOverride { IsKinematic = 0 });
            Manager.AddComponentData(body, new ActiveKinematicOverride
            {
                Config = new PhysicsKinematicOverrideData
                    { IsKinematic = true, ZeroVelocityOnEnter = true, ZeroGravity = false }
            });
            Manager.SetComponentEnabled<ActiveKinematicOverride>(body, true);
            Manager.AddComponentData(body, default(PhysicsKinematicOverrideState));

            World.GetOrCreateSystem<PhysicsKinematicOverrideApplySystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(0f, Manager.GetComponentData<ExternalVelocity>(body).Linear.z, 1e-4f,
                "Freeze must clear the standing external (knockback) channel");
            Assert.AreEqual(0, Manager.GetBuffer<PendingExternalForce>(body).Length,
                "Freeze must clear the external inbox so a same-frame hit is not composed in next tick");

            RunCompose(); // next producer tick: nothing should re-apply
            Assert.AreEqual(0f, Manager.GetComponentData<PhysicsVelocity>(body).Linear.z, 1e-4f,
                "A frozen body must not keep sliding via the external channel");
        }
    }
}
