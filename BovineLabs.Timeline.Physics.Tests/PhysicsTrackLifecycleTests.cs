using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Gravities;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsTrackLifecycleTests : ECSTestsFixture
    {
        [Test]
        public void ContinuousForce_DisablesActiveForce_WhenClipEnds_WhileTimelineStillActive()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsForceTrackSystem));

            var body = CreateForceBody();
            var clip = CreateForceClip(body, new PhysicsForceData
            {
                Mode = PhysicsForceMode.Continuous,
                DirectionMode = PhysicsForceDirectionMode.FixedVector,
                Linear = new float3(0f, 1f, 0f),
                Magnitude = 1f
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveForce>(body),
                "ActiveForce should be enabled while the clip is active");

            Manager.SetComponentEnabled<ClipActive>(clip, false);
            Manager.AddComponent<ClipActivePrevious>(clip);
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsFalse(Manager.IsComponentEnabled<ActiveForce>(body),
                "ActiveForce must disable at clip end, not persist until the timeline ends");
        }

        [Test]
        public void ImpulseForce_ReArmsFiredLatch_OnClipActivation_EvenWhileActive()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsForceTrackSystem));

            var body = CreateForceBody();

            Manager.SetComponentEnabled<ActiveForce>(body, true);
            Manager.SetComponentData(body, new PhysicsForceState { Fired = true });

            CreateForceClip(body, new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.FixedVector,
                Linear = new float3(1f, 0f, 0f),
                Magnitude = 1f
            });

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            Assert.IsFalse(Manager.GetComponentData<PhysicsForceState>(body).Fired,
                "A new clip activation must re-arm the impulse latch even while ActiveForce is still enabled " +
                "so adjacent impulse clips each fire");
        }

        [Test]
        public void GravityOverride_DoesNotReArmState_WhileActive()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsGravityOverrideTrackSystem));

            var body = CreateGravityBody(true,
                new PhysicsGravityOverrideState { Fired = true, OriginalGravityScale = 0.5f, AddedComponent = true });
            CreateGravityClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var state = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsTrue(state.Fired,
                "A capture-restore override must NOT re-arm mid-span; otherwise it re-captures the overridden value");
            Assert.AreEqual(0.5f, state.OriginalGravityScale, 1e-6f,
                "The captured original gravity must survive across touching clips on the same body");
        }

        [Test]
        public void GravityOverride_ReArmsState_WhenActiveDisabled()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsGravityOverrideTrackSystem));

            // A true span start after the previous span fully exited: OnExit already ran (Fired = false) but left a
            // stale captured original behind. The re-arm must scrub it back to the sentinel so the next OnEnter
            // captures the real live value rather than the stale one.
            var body = CreateGravityBody(false,
                new PhysicsGravityOverrideState { Fired = false, OriginalGravityScale = 0.5f, AddedComponent = true });
            CreateGravityClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var state = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsFalse(state.Fired,
                "At a true span start (Active disabled, no restore pending) the override re-arms to re-capture");
            Assert.AreEqual(1f, state.OriginalGravityScale, 1e-6f,
                "Re-arm resets the captured original to its sentinel so the next OnEnter captures the real value");
        }

        [Test]
        public void GravityOverride_KeepsPendingRestore_OnZeroFixedTickGap()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsGravityOverrideTrackSystem));

            // The bug scenario: Active is disabled (clip vanished this render frame) but its OnExit has NOT run yet
            // (Fired still set) — a clip gap with zero fixed ticks in it. The span-start reset for the next clip must
            // NOT wipe the pending restore, or the captured original is stranded and the body's gravity sticks.
            var body = CreateGravityBody(false,
                new PhysicsGravityOverrideState { Fired = true, OriginalGravityScale = 0.5f, AddedComponent = true });
            CreateGravityClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var state = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsTrue(state.Fired,
                "A pending exit restore must survive the span-start reset when no fixed tick ran OnExit in the gap");
            Assert.AreEqual(0.5f, state.OriginalGravityScale, 1e-6f,
                "The captured original must be kept intact so the eventual real exit can restore it");
        }

        [Test]
        public void GravityOverride_ZeroFixedTickGap_StillRestoresOriginal_WhenClipTrulyEnds()
        {
            var begin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            var track = World.GetOrCreateSystem(typeof(PhysicsGravityOverrideTrackSystem));
            var apply = World.GetOrCreateSystem(typeof(PhysicsGravityOverrideApplySystem));

            // Mid-override: OnEnter already captured Original = 1 and drove the live factor to 0 (zero-G). Active is
            // disabled this frame (clip A gone) with OnExit not yet run — the zero-fixed-tick gap — and a new clip B
            // activates the same window, which is what fires the span-start reset.
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsGravityFactor { Value = 0f });
            Manager.AddComponentData(body, new ActiveGravityOverride());
            Manager.SetComponentEnabled<ActiveGravityOverride>(body, false);
            Manager.AddComponentData(body, new PhysicsGravityOverrideState
                { Fired = true, OriginalGravityScale = 1f, AddedComponent = false });
            CreateGravityClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var mid = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsTrue(mid.Fired, "Zero-fixed-tick gap must not clear the pending restore");
            Assert.AreEqual(1f, mid.OriginalGravityScale, 1e-6f, "Captured original must survive the gap");

            // The clip truly ends now (Active disabled); the fixed-step apply's OnExit restores the pre-override value.
            // Give the active config RestoreOnExit so the exit branch actually restores (the default clip data does not).
            Manager.SetComponentData(body, new ActiveGravityOverride
            {
                Config = new PhysicsGravityOverrideData { GravityScale = 0f, RestoreOnExit = true, Present = 1 }
            });
            Manager.SetComponentEnabled<ActiveGravityOverride>(body, false);
            apply.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(1f, Manager.GetComponentData<PhysicsGravityFactor>(body).Value, 1e-6f,
                "Pre-override gravity must be restored; without the skip the reset would have stranded it at 0");
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

        private Entity CreateGravityBody(bool active, PhysicsGravityOverrideState state)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveGravityOverride());
            Manager.SetComponentEnabled<ActiveGravityOverride>(body, active);
            Manager.AddComponentData(body, state);
            return body;
        }

        private Entity CreateGravityClip(Entity body)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = body });
            Manager.AddComponentData(clip,
                new PhysicsGravityOverrideAnimated { AuthoredData = default, Value = default });
            Manager.AddComponent<TimelineActive>(clip);
            Manager.SetComponentEnabled<TimelineActive>(clip, true);
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, true);
            return clip;
        }
    }
}