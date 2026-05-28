using BovineLabs.Core.Collections;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Essence.Debug;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
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
            // Create stub debug singletons that EssenceTelemetrySystem requires.
            // Without these, the debug system (which runs in Editor world) throws
            // GetSingleton<EssenceDebugNames>() errors when it tries to update.
            CreateDebugSingletons();
        }

        private void CreateDebugSingletons()
        {
            // Create EssenceConfig singleton with empty intrinsic data
            var essenceConfigBuilder = new BlobBuilder(Allocator.Temp);
            ref var essenceConfigRoot = ref essenceConfigBuilder.ConstructRoot<EssenceConfig.Data>();
            essenceConfigBuilder.AllocateHashMap(ref essenceConfigRoot.IntrinsicDatas, 0);
            essenceConfigBuilder.AllocateMultiHashMap(ref essenceConfigRoot.StatsLimitIntrinsics, 0);
            var essenceConfigBlob = essenceConfigBuilder.CreateBlobAssetReference<EssenceConfig.Data>(Allocator.Persistent);
            essenceConfigBuilder.Dispose();

            var essenceConfigEntity = Manager.CreateSingleton<EssenceConfig>();
            Manager.SetComponentData(essenceConfigEntity, new EssenceConfig { Value = essenceConfigBlob });
            BlobAssetStore.TryAdd(ref essenceConfigBlob);

            // Create EssenceDebugNames singleton with empty name maps
            var debugNamesBuilder = new BlobBuilder(Allocator.Temp);
            ref var debugNamesRoot = ref debugNamesBuilder.ConstructRoot<EssenceDebugNames.Data>();
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.StatNames, 0);
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.IntrinsicNames, 0);
            debugNamesBuilder.AllocateHashMap(ref debugNamesRoot.EventNames, 0);
            var debugNamesBlob = debugNamesBuilder.CreateBlobAssetReference<EssenceDebugNames.Data>(Allocator.Persistent);
            debugNamesBuilder.Dispose();

            var debugNamesEntity = Manager.CreateSingleton<EssenceDebugNames>();
            Manager.SetComponentData(debugNamesEntity, new EssenceDebugNames { Value = debugNamesBlob });
            BlobAssetStore.TryAdd(ref debugNamesBlob);

            // Create DrawSystem.Singleton stub (required by TimelineDebugUtility checks)
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
                FalloffStartRadius = 1f, // The target is at dist 3, which is > 1. This tests the Step curve logic!
                FalloffEndRadius = 5f,   // dist < 5, so it should apply full force.
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
            Manager.AddComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 6))); // dist = 6
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
                FalloffEndRadius = 5f,   // dist = 6, which is > 5. Force should be zero!
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
