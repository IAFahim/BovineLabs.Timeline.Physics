using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Gravities;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
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

            var body = CreateGravityBody(false,
                new PhysicsGravityOverrideState { Fired = true, OriginalGravityScale = 0.5f, AddedComponent = true });
            CreateGravityClip(body);

            track.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            begin.Update();

            var state = Manager.GetComponentData<PhysicsGravityOverrideState>(body);
            Assert.IsFalse(state.Fired,
                "At a true span start (Active disabled after a gap) the override must re-arm to re-capture");
            Assert.AreEqual(1f, state.OriginalGravityScale, 1e-6f,
                "Re-arm resets the captured original to its sentinel so the next OnEnter captures the real value");
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