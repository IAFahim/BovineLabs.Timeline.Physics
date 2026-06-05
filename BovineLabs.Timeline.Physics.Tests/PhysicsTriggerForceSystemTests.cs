using BovineLabs.Core.Collections;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Essence.Debug;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
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
                ApplyTo = Target.Target
            });

            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.None
            });

            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });

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
                ApplyTo = Target.Target
            });

            Manager.AddComponentData(trigger, new PhysicsTriggerFilterData
            {
                IgnoreTarget = Target.None
            });

            Manager.AddComponentData(trigger, new ClipActive());
            Manager.AddComponentData(trigger, new TrackBinding { Value = trigger });

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
    }
}