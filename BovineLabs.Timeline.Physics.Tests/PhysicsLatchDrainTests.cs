using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.PIDs;
using BovineLabs.Timeline.Physics.Teleports;
using BovineLabs.Timeline.Physics.VelocityOverrides;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    /// <summary>
    /// The fixed-step drain gate (todo.md deferred items 1 + 2): a fire-once / continuous-motion clip whose active
    /// window straddled no fixed tick inside the one-render-frame-delayed enable window must NOT be dropped — the
    /// render-side stale-disable lingers the (undrained) latch enabled so the fixed apply is guaranteed one tick to
    /// service it, and the continuous tail (unconsumed ElapsedTime - AppliedTime) is drained on that tick instead of
    /// being truncated. Once serviced, the drain-finalize disables it.
    /// </summary>
    public class PhysicsLatchDrainTests : ECSTestsFixture
    {
        // ---- Force: impulse (fire-once hard-drop) -------------------------------------------------------------

        [Test]
        public void ImpulseForce_ClipEndsWithNoFixedTick_LingersEnabled_NotDropped()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsForceTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsKinematicsApplySystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreateForceBody();
            var clip = CreateForceClip(body, new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.FixedVector,
                Linear = new float3(7f, 0f, 0f),
                Present = 1,
            });

            // Activate: latch enabled + config written, but no fixed apply has run yet (Fired is still false).
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveForce>(body));
            Assert.IsFalse(Manager.GetComponentData<PhysicsForceState>(body).Fired);

            // Clip ends before any fixed tick observed the latch. The stale-disable must LINGER, not drop it.
            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsTrue(Manager.IsComponentEnabled<ActiveForce>(body),
                "An unfired impulse latch must linger enabled so the fixed clock still fires it (not be dropped)");
            Assert.IsTrue(Manager.GetComponentData<PhysicsForceState>(body).Orphaned,
                "The lingering latch must be marked Orphaned");

            // The (delayed) fixed apply finally runs: the impulse fires — the effect is delivered, not lost.
            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, pendings.Length, "The lingered impulse must still fire exactly once");
            Assert.AreEqual(7f, pendings[0].Linear.x, 1e-4f);
            Assert.IsTrue(Manager.GetComponentData<PhysicsForceState>(body).Fired);

            // Drain-finalize disables the now-serviced orphan.
            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveForce>(body),
                "Once serviced, the drain-finalize must disable the orphaned latch");
            Assert.IsFalse(Manager.GetComponentData<PhysicsForceState>(body).Orphaned);
        }

        [Test]
        public void ImpulseForce_AlreadyFired_ClipEnds_DisablesImmediately_NoLinger()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsForceTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsKinematicsApplySystem));

            var body = CreateForceBody();
            var clip = CreateForceClip(body, new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.FixedVector,
                Linear = new float3(3f, 0f, 0f),
                Present = 1,
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            // A fixed tick fired the impulse while the clip was active.
            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsTrue(Manager.GetComponentData<PhysicsForceState>(body).Fired);

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            // Already drained -> zero deactivation latency is preserved (immediate disable, no linger).
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveForce>(body),
                "A drained (already fired) impulse latch must disable immediately at clip end");
            Assert.IsFalse(Manager.GetComponentData<PhysicsForceState>(body).Orphaned);
        }

        // ---- Force: continuous tail (item 2) ------------------------------------------------------------------

        [Test]
        public void ContinuousForce_TailRemainder_DrainedOnLinger_NotTruncated()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsForceTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsKinematicsApplySystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreateForceBody();
            var clip = CreateForceClip(body, new PhysicsForceData
            {
                Mode = PhysicsForceMode.Continuous,
                DirectionMode = PhysicsForceDirectionMode.FixedVector,
                Linear = new float3(10f, 0f, 0f),
                Present = 1,
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            // The render side accumulated 0.1s of clip-active time that no fixed tick has consumed yet.
            var s = Manager.GetComponentData<PhysicsForceState>(body);
            s.ElapsedTime = 0.1f;
            s.AppliedTime = 0f;
            Manager.SetComponentData(body, s);

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsTrue(Manager.IsComponentEnabled<ActiveForce>(body),
                "A continuous latch with an unconsumed tail must linger so the tail is not truncated");
            Assert.IsTrue(Manager.GetComponentData<PhysicsForceState>(body).Orphaned);

            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, pendings.Length);
            Assert.AreEqual(10f * 0.1f, pendings[0].Linear.x, 1e-4f,
                "The full tail (force x remaining clip-active time) must be delivered, not discarded");
            Assert.AreEqual(0.1f, Manager.GetComponentData<PhysicsForceState>(body).AppliedTime, 1e-5f);

            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveForce>(body));
        }

        // ---- Velocity: instant add (fire-once) + continuous tail ---------------------------------------------

        [Test]
        public void InstantVelocity_ClipEndsWithNoFixedTick_LingersEnabled_NotDropped()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsVelocityTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsKinematicsApplySystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreateVelocityBody();
            var clip = CreateVelocityClip(body, new PhysicsVelocityData
            {
                Mode = PhysicsVelocityMode.AddInstant,
                Linear = new float3(0f, 5f, 0f),
                Space = Target.None,
                Present = 1,
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveVelocity>(body));

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsTrue(Manager.IsComponentEnabled<ActiveVelocity>(body),
                "An unfired instant-velocity latch must linger, not be dropped");
            Assert.IsTrue(Manager.GetComponentData<PhysicsVelocityState>(body).Orphaned);

            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingVelocity>(body);
            Assert.AreEqual(1, pendings.Length, "The lingered instant velocity must still apply exactly once");
            Assert.AreEqual(5f, pendings[0].Linear.y, 1e-4f);

            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveVelocity>(body));
        }

        [Test]
        public void ContinuousVelocity_TailRemainder_DrainedOnLinger_NotTruncated()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsVelocityTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsKinematicsApplySystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreateVelocityBody();
            var clip = CreateVelocityClip(body, new PhysicsVelocityData
            {
                Mode = PhysicsVelocityMode.AddContinuous,
                Linear = new float3(0f, 8f, 0f),
                Space = Target.None,
                Present = 1,
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var s = Manager.GetComponentData<PhysicsVelocityState>(body);
            s.ElapsedTime = 0.05f;
            s.AppliedTime = 0f;
            Manager.SetComponentData(body, s);

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsTrue(Manager.IsComponentEnabled<ActiveVelocity>(body));
            Assert.IsTrue(Manager.GetComponentData<PhysicsVelocityState>(body).Orphaned);

            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingVelocity>(body);
            Assert.AreEqual(1, pendings.Length);
            Assert.AreEqual(8f * 0.05f, pendings[0].Linear.y, 1e-4f,
                "The full continuous velocity tail must be delivered on the linger tick");

            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveVelocity>(body));
        }

        // ---- Teleport (fire-once hard-drop) ------------------------------------------------------------------

        [Test]
        public void Teleport_ClipEndsWithNoFixedTick_LingersEnabled_NotDropped()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsTeleportTrackSystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreateTeleportBody();
            var clip = CreateTeleportClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveTeleport>(body));

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            // Item 1's most visible hard-drop: a short teleport clip silently doing nothing. The latch must survive
            // enabled so the fixed-step teleport apply still gets to fire it.
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveTeleport>(body),
                "An unfired teleport latch must linger enabled so a short clip still teleports");
            Assert.IsTrue(Manager.GetComponentData<PhysicsTeleportState>(body).Orphaned);

            // Simulate the fixed-step teleport apply servicing it (it sets Fired), then the drain-finalize disables it.
            Manager.SetComponentData(body, new PhysicsTeleportState { Fired = true, Orphaned = true });
            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveTeleport>(body));
            Assert.IsFalse(Manager.GetComponentData<PhysicsTeleportState>(body).Orphaned);
        }

        // ---- PID (continuous controller) ---------------------------------------------------------------------

        [Test]
        public void LinearPid_ClipEndsWithNoFixedTick_LingersEnabled_ThenFinalizeDisables()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsLinearPIDTrackSystem));
            var finalize = World.GetOrCreateSystem(typeof(PhysicsLatchDrainFinalizeSystem));

            var body = CreatePidBody();
            var clip = CreatePidClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveLinearPid>(body));

            EndClip(clip);
            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            // A PID controller has no one-shot terminal state, so an orphaned latch always lingers one fixed tick to
            // guarantee a short clip still applies at least one control step instead of being a silent no-op.
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveLinearPid>(body),
                "An orphaned PID latch must linger one fixed tick so a short clip is not dropped");
            Assert.IsTrue(Manager.GetComponentData<PhysicsLinearPIDState>(body).Orphaned);

            finalize.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveLinearPid>(body),
                "After the serviced linger tick the drain-finalize must disable the PID latch");
        }

        // ---- helpers -----------------------------------------------------------------------------------------

        private void EndClip(Entity clip)
        {
            Manager.SetComponentEnabled<ClipActive>(clip, false);
            if (!Manager.HasComponent<ClipActivePrevious>(clip))
                Manager.AddComponent<ClipActivePrevious>(clip);
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
        }

        private Entity CreateForceBody()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveForce());
            Manager.SetComponentEnabled<ActiveForce>(body, false);
            Manager.AddComponentData(body, new PhysicsForceState());
            Manager.AddBuffer<PendingForce>(body);
            return body;
        }

        private Entity CreateForceClip(Entity body, PhysicsForceData config)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = body });
            Manager.AddComponentData(clip, new PhysicsForceAnimated { AuthoredData = config, Value = config });
            Manager.AddComponent<TimelineActive>(clip);
            Manager.SetComponentEnabled<TimelineActive>(clip, true);
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, true);
            return clip;
        }

        private Entity CreateVelocityBody()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveVelocity());
            Manager.SetComponentEnabled<ActiveVelocity>(body, false);
            Manager.AddComponentData(body, new PhysicsVelocityState());
            Manager.AddBuffer<PendingVelocity>(body);
            return body;
        }

        private Entity CreateVelocityClip(Entity body, PhysicsVelocityData config)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = body });
            Manager.AddComponentData(clip, new PhysicsVelocityAnimated { AuthoredData = config, Value = config });
            Manager.AddComponent<TimelineActive>(clip);
            Manager.SetComponentEnabled<TimelineActive>(clip, true);
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, true);
            return clip;
        }

        private Entity CreateTeleportBody()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveTeleport());
            Manager.SetComponentEnabled<ActiveTeleport>(body, false);
            Manager.AddComponentData(body, new PhysicsTeleportState());
            return body;
        }

        private Entity CreateTeleportClip(Entity body)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = body });
            Manager.AddComponentData(clip,
                new PhysicsTeleportAnimated { AuthoredData = default, Value = default });
            Manager.AddComponent<TimelineActive>(clip);
            Manager.SetComponentEnabled<TimelineActive>(clip, true);
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, true);
            return clip;
        }

        private Entity CreatePidBody()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveLinearPid());
            Manager.SetComponentEnabled<ActiveLinearPid>(body, false);
            Manager.AddComponentData(body, new PhysicsLinearPIDState());
            Manager.AddBuffer<PendingForce>(body);
            return body;
        }

        private Entity CreatePidClip(Entity body)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = body });
            Manager.AddComponentData(clip,
                new PhysicsLinearPIDAnimated { AuthoredData = default, Value = default });
            Manager.AddComponent<TimelineActive>(clip);
            Manager.SetComponentEnabled<TimelineActive>(clip, true);
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, true);
            return clip;
        }
    }
}
