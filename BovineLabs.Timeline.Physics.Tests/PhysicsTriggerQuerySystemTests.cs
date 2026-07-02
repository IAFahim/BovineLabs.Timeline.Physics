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
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsTriggerQuerySystemTests : ECSTestsFixture
    {
        private PhysicsWorld physicsWorld;
        private bool physicsWorldCreated;

        public override void Setup()
        {
            base.Setup();
            CreateDebugSingletons();
            CreatePhysicsWorldSingleton();
        }

        public override void TearDown()
        {
            if (physicsWorldCreated)
            {
                physicsWorld.Dispose();
                physicsWorldCreated = false;
            }

            base.TearDown();
        }

        private void CreatePhysicsWorldSingleton()
        {
            physicsWorld = new PhysicsWorld(0, 0, 0);
            physicsWorldCreated = true;
            var entity = Manager.CreateSingleton<PhysicsWorldSingleton>();
            Manager.SetComponentData(entity, new PhysicsWorldSingleton { PhysicsWorld = physicsWorld });
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

        private Entity CreateQueryClip(PhysicsTriggerQueryData config)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, LocalTransform.Identity);
            Manager.AddComponentData(clip, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(clip, new Targets());
            Manager.AddComponentData(clip, new TrackBinding { Value = clip });
            Manager.AddComponentData(clip, new ClipActive());
            Manager.AddComponentData(clip, config);
            Manager.AddComponentData(clip, new PhysicsTriggerFilterData { IgnoreTarget = Target.None });
            Manager.AddComponentData(clip, new PhysicsTriggerQueryState());
            // The query system requires an enabled PhysicsClipGate (the fixed-step activation gate); enableable
            // components added via AddComponentData start enabled, and FirstFrame/LastFrame default to 0.
            Manager.AddComponentData(clip, new PhysicsClipGate());
            Manager.AddBuffer<StatefulTriggerEvent>(clip);
            return clip;
        }

        private Entity CreateCandidate(float3 position)
        {
            var candidate = Manager.CreateEntity();
            Manager.AddComponentData(candidate, new LocalToWorld { Value = float4x4.Translate(position) });
            return candidate;
        }

        private void AddEvent(Entity clip, Entity candidate)
        {
            Manager.GetBuffer<StatefulTriggerEvent>(clip).Add(new StatefulTriggerEvent
            {
                EntityB = candidate,
                State = StatefulEventState.Stay
            });
        }

        private void RunQuery()
        {
            var sys = World.GetOrCreateSystem<PhysicsTriggerQuerySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        [Test]
        public void Nearest_PicksClosestCandidate_AndRoutesIntoCustomTarget()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = Target.Self
            });

            var far = CreateCandidate(new float3(0, 0, 5));
            var near = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, far);
            AddEvent(clip, near);

            RunQuery();

            Assert.AreEqual(near, Manager.GetComponentData<Targets>(clip).Custom);
        }

        [Test]
        public void Farthest_PicksMostDistantCandidate()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Farthest,
                RouteTo = Target.Self
            });

            var far = CreateCandidate(new float3(0, 0, 5));
            var near = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, far);
            AddEvent(clip, near);

            RunQuery();

            Assert.AreEqual(far, Manager.GetComponentData<Targets>(clip).Custom);
        }

        [Test]
        public void MostAligned_PicksCandidateClosestToForward()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.MostAligned,
                RouteTo = Target.Self
            });

            var ahead = CreateCandidate(new float3(0, 0, 5));
            var beside = CreateCandidate(new float3(5, 0, 0));
            AddEvent(clip, beside);
            AddEvent(clip, ahead);

            RunQuery();

            Assert.AreEqual(ahead, Manager.GetComponentData<Targets>(clip).Custom);
        }

        [Test]
        public void MaxDistance_ExcludesCandidatesOutsideRange()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = Target.Self,
                MaxDistance = 3f
            });

            var outside = CreateCandidate(new float3(0, 0, 5));
            var inside = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, outside);
            AddEvent(clip, inside);

            RunQuery();

            Assert.AreEqual(inside, Manager.GetComponentData<Targets>(clip).Custom);

            Manager.GetBuffer<StatefulTriggerEvent>(clip).Clear();
            AddEvent(clip, outside);
            Manager.SetComponentData(clip, new PhysicsTriggerQueryState());
            Manager.SetComponentData(clip, new Targets());

            RunQuery();

            Assert.AreEqual(Entity.Null, Manager.GetComponentData<Targets>(clip).Custom,
                "No candidate inside range — Custom must stay untouched");
        }

        [Test]
        public void MaxAngle_ExcludesCandidatesOutsideViewCone()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = Target.Self,
                MaxAngle = math.radians(30f)
            });

            var behind = CreateCandidate(new float3(0, 0, -2));
            var ahead = CreateCandidate(new float3(0, 0, 4));
            AddEvent(clip, behind);
            AddEvent(clip, ahead);

            RunQuery();

            Assert.AreEqual(ahead, Manager.GetComponentData<Targets>(clip).Custom);
        }

        [Test]
        public void ExcludeRoles_SkipsRoutedRoleEntities()
        {
            var owner = CreateCandidate(new float3(0, 0, 2));
            var other = CreateCandidate(new float3(0, 0, 3));

            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = Target.Self,
                ExcludeRoles = PhysicsTriggerRoleMask.Owner
            });

            // Give the clip a Targets.Owner pointing at `owner`, so ExcludeRoles must skip it.
            Manager.SetComponentData(clip, new Targets { Owner = owner });
            AddEvent(clip, owner);
            AddEvent(clip, other);

            RunQuery();

            // owner is nearest but excluded → `other` wins.
            Assert.AreEqual(other, Manager.GetComponentData<Targets>(clip).Custom);
        }

        [Test]
        public void AllSurvivorsFanout_FiresPerSurvivorAndFillsHitBuffer()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.AllSurvivorsFanout,
                RouteTo = Target.Self,
                MaxTargets = 8,
                WriteHitBuffer = true,
                ValueMode = PhysicsTriggerQueryValueMode.DirectionSector,
                SectorCount = 8,
                SectorReference = PhysicsTriggerSectorReference.SelfForward,
                SectorPlane = PhysicsTriggerSectorPlane.XZ,
                SectorCustomUp = new float3(0, 1, 0)
            });
            Manager.AddBuffer<TriggerQueryHit>(clip);

            var front = CreateCandidate(new float3(0, 0, 3)); // sector 0
            var right = CreateCandidate(new float3(3, 0, 0)); // sector 2
            var back = CreateCandidate(new float3(0, 0, -3)); // sector 4
            AddEvent(clip, front);
            AddEvent(clip, right);
            AddEvent(clip, back);

            RunQuery();

            var hits = Manager.GetBuffer<TriggerQueryHit>(clip);
            Assert.AreEqual(3, hits.Length, "all three survivors emitted a hit");

            // Each hit carries ITS OWN sector value.
            var sectors = new System.Collections.Generic.HashSet<int>();
            foreach (var h in hits) sectors.Add(h.Sector);
            Assert.IsTrue(sectors.Contains(0), "front survivor → sector 0");
            Assert.IsTrue(sectors.Contains(2), "right survivor → sector 2");
            Assert.IsTrue(sectors.Contains(4), "back survivor → sector 4");
        }

        [Test]
        public void AllSurvivorsFanout_HardCapsAtMaxTargets()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.AllSurvivorsFanout,
                RouteTo = Target.Self,
                MaxTargets = 2,
                WriteHitBuffer = true
            });
            Manager.AddBuffer<TriggerQueryHit>(clip);

            for (var k = 0; k < 5; k++)
                AddEvent(clip, CreateCandidate(new float3(0, 0, 2 + k)));

            RunQuery();

            Assert.AreEqual(2, Manager.GetBuffer<TriggerQueryHit>(clip).Length, "survivors past the cap are dropped");
        }

        [Test]
        public void ClearOnLost_WritesNullAndStateTransitionsOnce()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = Target.Self,
                ClearOnLost = true
            });
            Manager.AddComponent<ClipActivePrevious>(clip);
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);

            var candidate = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, candidate);

            RunQuery();
            Assert.AreEqual(candidate, Manager.GetComponentData<Targets>(clip).Custom);
            Assert.AreEqual(candidate, Manager.GetComponentData<PhysicsTriggerQueryState>(clip).LastWinner);

            Manager.GetBuffer<StatefulTriggerEvent>(clip).Clear();

            RunQuery();
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<Targets>(clip).Custom);
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<PhysicsTriggerQueryState>(clip).LastWinner);
        }
    }
}