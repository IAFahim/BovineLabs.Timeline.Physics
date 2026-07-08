using BovineLabs.Core.Collections;
using BovineLabs.Nerve.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Essence.Debug;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.TriggerEvents;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsTriggerForceSystemTests : ECSTestsFixture
    {
        public override void Setup()
        {
            base.Setup();
            CreateDebugSingletons();
        }

        private void CreateDebugSingletons()
        {
            var essenceConfigBuilder = new BlobBuilder(Allocator.Temp);
            ref var essenceConfigRoot = ref essenceConfigBuilder.ConstructRoot<EssenceConfig.Data>();
            essenceConfigBuilder.AllocateHashMap(ref essenceConfigRoot.IntrinsicDatas, 0);
            essenceConfigBuilder.AllocateMultiHashMap(ref essenceConfigRoot.StatsLimitIntrinsics, 0);
            var essenceConfigBlob =
                essenceConfigBuilder.CreateBlobAssetReference<EssenceConfig.Data>(Allocator.Persistent);
            essenceConfigBuilder.Dispose();

            var essenceConfigEntity = Manager.CreateSingleton<EssenceConfig>();
            Manager.SetComponentData(essenceConfigEntity, new EssenceConfig { Value = essenceConfigBlob });
            BlobAssetStore.TryAdd(ref essenceConfigBlob);

            var debugNamesBuilder = new BlobBuilder(Allocator.Temp);
            ref var debugNamesRoot = ref debugNamesBuilder.ConstructRoot<EssenceDebugNames.Data>();
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.StatNames, 0);
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.IntrinsicNames, 0);
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.EventNames, 0);
            var debugNamesBlob =
                debugNamesBuilder.CreateBlobAssetReference<EssenceDebugNames.Data>(Allocator.Persistent);
            debugNamesBuilder.Dispose();

            var debugNamesEntity = Manager.CreateSingleton<EssenceDebugNames>();
            Manager.SetComponentData(debugNamesEntity, new EssenceDebugNames { Value = debugNamesBlob });
            BlobAssetStore.TryAdd(ref debugNamesBlob);

            Manager.SetComponentData(Manager.CreateSingleton<DrawSystem.Singleton>(), new DrawSystem.Singleton());
        }

        [Test]
        public void StepFalloff_MaintainsMagnitudeWithinEndRadius()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 3)));
            Manager.AddBuffer<PendingForce>(target);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.Translate(new float3(0, 0, 3)) });

            var trigger = Manager.CreateEntity();
            Manager.AddComponentData(trigger, LocalTransform.FromPosition(new float3(0, 0, 0)));
            Manager.AddComponentData(trigger, new LocalToWorld { Value = float4x4.identity });

            Manager.AddComponentData(trigger, new PhysicsTriggerForceData
            {
                EventState = StatefulEventState.Stay,
                ForceType = PhysicsTriggerForceType.Directional,
                Mode = PhysicsForceMode.Impulse,
                Magnitude = 10f,
                Direction = new float3(0, 0, 1),
                OriginMode = PhysicsTriggerPositionMode.MatchSelf,
                FalloffCurve = PhysicsTriggerFalloffCurve.Step,
                FalloffStartRadius = 1f,
                FalloffEndRadius = 5f,
                ApplyTo = new EntityLinkRef { ReadRootFrom = Target.Target }
            });

            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.None
            });

            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });
            Manager.AddComponentData(trigger, new PhysicsClipGate()); // required by the system query; enabled on add

            var events = Manager.AddBuffer<StatefulTriggerEvent>(trigger);
            events.Add(new StatefulTriggerEvent
            {
                EntityB = target,
                State = StatefulEventState.Stay
            });

            var sys = World.GetOrCreateSystem<PhysicsTriggerForceSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendingForces = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(1, pendingForces.Length);
            Assert.AreEqual(10f, pendingForces[0].Linear.z, 0.001f);
        }

        [Test]
        public void StepFalloff_DropsToZeroOutsideEndRadius()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 6)));
            Manager.AddBuffer<PendingForce>(target);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.Translate(new float3(0, 0, 6)) });

            var trigger = Manager.CreateEntity();
            Manager.AddComponentData(trigger, LocalTransform.FromPosition(new float3(0, 0, 0)));
            Manager.AddComponentData(trigger, new LocalToWorld { Value = float4x4.identity });

            Manager.AddComponentData(trigger, new PhysicsTriggerForceData
            {
                EventState = StatefulEventState.Stay,
                ForceType = PhysicsTriggerForceType.Directional,
                Mode = PhysicsForceMode.Impulse,
                Magnitude = 10f,
                Direction = new float3(0, 0, 1),
                OriginMode = PhysicsTriggerPositionMode.MatchSelf,
                FalloffCurve = PhysicsTriggerFalloffCurve.Step,
                FalloffStartRadius = 1f,
                FalloffEndRadius = 5f,
                ApplyTo = new EntityLinkRef { ReadRootFrom = Target.Target }
            });

            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.None
            });

            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });
            Manager.AddComponentData(trigger, new PhysicsClipGate()); // required by the system query; enabled on add

            var events = Manager.AddBuffer<StatefulTriggerEvent>(trigger);
            events.Add(new StatefulTriggerEvent
            {
                EntityB = target,
                State = StatefulEventState.Stay
            });

            var sys = World.GetOrCreateSystem<PhysicsTriggerForceSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendingForces = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(0, pendingForces.Length);
        }

        [Test]
        public void HitMode_AllContacts_AppliesForcePerShape()
        {
            Assert.AreEqual(3, RunMultiShape(PhysicsTriggerHitMode.AllContacts));
        }

        [Test]
        public void HitMode_FirstPerRoot_AppliesForceOncePerRoot()
        {
            Assert.AreEqual(1, RunMultiShape(PhysicsTriggerHitMode.FirstPerRoot));
        }

        private int RunMultiShape(PhysicsTriggerHitMode hitMode)
        {
            var root = Manager.CreateEntity();
            var shapeA = CreateShape(root, new float3(0f, 0f, 3f));
            var shapeB = CreateShape(root, new float3(0.1f, 0f, 3f));
            var shapeC = CreateShape(root, new float3(0.2f, 0f, 3f));

            var trigger = Manager.CreateEntity();
            Manager.AddComponentData(trigger, LocalTransform.FromPosition(float3.zero));
            Manager.AddComponentData(trigger, new LocalToWorld { Value = float4x4.identity });

            Manager.AddComponentData(trigger, new PhysicsTriggerForceData
            {
                EventState = StatefulEventState.Stay,
                ForceType = PhysicsTriggerForceType.Directional,
                Mode = PhysicsForceMode.Impulse,
                Magnitude = 10f,
                Direction = new float3(0f, 0f, 1f),
                OriginMode = PhysicsTriggerPositionMode.MatchSelf,
                FalloffCurve = PhysicsTriggerFalloffCurve.None,
                ApplyTo = new EntityLinkRef { ReadRootFrom = Target.Target }
            });

            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.None,
                HitMode = hitMode
            });

            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });
            Manager.AddComponentData(trigger, new PhysicsClipGate()); // required by the system query; enabled on add

            var events = Manager.AddBuffer<StatefulTriggerEvent>(trigger);
            events.Add(new StatefulTriggerEvent { EntityB = shapeA, State = StatefulEventState.Stay });
            events.Add(new StatefulTriggerEvent { EntityB = shapeB, State = StatefulEventState.Stay });
            events.Add(new StatefulTriggerEvent { EntityB = shapeC, State = StatefulEventState.Stay });

            var sys = World.GetOrCreateSystem<PhysicsTriggerForceSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            return Manager.GetBuffer<PendingForce>(shapeA).Length
                   + Manager.GetBuffer<PendingForce>(shapeB).Length
                   + Manager.GetBuffer<PendingForce>(shapeC).Length;
        }

        [Test]
        public void Channel_RoutesToIntentByDefault_AndExternalWhenFlagged()
        {
            // Default channel (Intent) lands in PendingForce; Channel=External lands in PendingExternalForce so the
            // hit rides the knockback channel. Target carries BOTH buffers so routing — not buffer presence — decides.
            AssertChannelRoutes(MotionChannel.Intent, expectIntent: 1, expectExternal: 0);
            AssertChannelRoutes(MotionChannel.External, expectIntent: 0, expectExternal: 1);
        }

        private void AssertChannelRoutes(MotionChannel channel, int expectIntent, int expectExternal)
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 3)));
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.Translate(new float3(0, 0, 3)) });
            Manager.AddBuffer<PendingForce>(target);
            Manager.AddBuffer<PendingExternalForce>(target);

            var trigger = Manager.CreateEntity();
            Manager.AddComponentData(trigger, LocalTransform.FromPosition(float3.zero));
            Manager.AddComponentData(trigger, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(trigger, new PhysicsTriggerForceData
            {
                EventState = StatefulEventState.Stay,
                ForceType = PhysicsTriggerForceType.Directional,
                Mode = PhysicsForceMode.Impulse,
                Channel = channel,
                Magnitude = 10f,
                Direction = new float3(0, 0, 1),
                OriginMode = PhysicsTriggerPositionMode.MatchSelf,
                FalloffCurve = PhysicsTriggerFalloffCurve.None,
                ApplyTo = new EntityLinkRef { ReadRootFrom = Target.Target },
            });
            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData { IgnoreTarget = Target.None });
            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });
            Manager.AddComponentData(trigger, new PhysicsClipGate());
            Manager.AddBuffer<StatefulTriggerEvent>(trigger)
                .Add(new StatefulTriggerEvent { EntityB = target, State = StatefulEventState.Stay });

            World.GetOrCreateSystem<PhysicsTriggerForceSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(expectIntent, Manager.GetBuffer<PendingForce>(target).Length, $"{channel}: PendingForce");
            Assert.AreEqual(expectExternal, Manager.GetBuffer<PendingExternalForce>(target).Length,
                $"{channel}: PendingExternalForce");
        }

        private Entity CreateShape(Entity root, float3 pos)
        {
            var e = Manager.CreateEntity();
            Manager.AddComponentData(e, LocalTransform.FromPosition(pos));
            Manager.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });
            Manager.AddComponentData(e, new EntityLinkSource { Root = root });
            Manager.AddBuffer<PendingForce>(e);
            return e;
        }
    }
}