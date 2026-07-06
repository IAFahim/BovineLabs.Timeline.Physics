using BovineLabs.Core.Collections;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Essence.Debug;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
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
        private DoubleRewindableAllocators payloadAllocators;

        public override void Setup()
        {
            base.Setup();
            CreateDebugSingletons();
            CreatePhysicsWorldSingleton();

            // ConditionEventWriter.Lookup resolves this allocator singleton, normally owned by
            // ConditionEventWriteSystem, which this fixture does not create.
            payloadAllocators = new DoubleRewindableAllocators(Allocator.Persistent, 16 * 1024);
            Manager.AddComponentData(Manager.CreateEntity(),
                new ConditionEventPayloadAllocator { Handle = payloadAllocators.Allocator.Handle });

            // ConditionEventWriter also resolves the ConditionConfig blob (payload type per event key),
            // normally baked from project settings. Provide a flat Int32-payload config so writes resolve.
            CreateConditionConfigSingleton();

            // The query system resolves the hit buffer's home entity at runtime and adds it via this ECB; creating the
            // system here provides its Singleton (SystemAPI.GetSingleton in OnUpdate) and lets tests play it back.
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        public override void TearDown()
        {
            if (physicsWorldCreated)
            {
                physicsWorld.Dispose();
                physicsWorldCreated = false;
            }

            payloadAllocators.Dispose();

            base.TearDown();
        }

        private void CreatePhysicsWorldSingleton()
        {
            physicsWorld = new PhysicsWorld(0, 0, 0);
            physicsWorldCreated = true;
            var entity = Manager.CreateSingleton<PhysicsWorldSingleton>();
            Manager.SetComponentData(entity, new PhysicsWorldSingleton { PhysicsWorld = physicsWorld });
        }

        private void CreateConditionConfigSingleton()
        {
            const int maxEventKey = 600;

            // Event condition type key (ConditionTypes.EventType == byte 0 in SetReset); hardcoded so the
            // fixture does not depend on the KSettings asset being loaded into the test world.
            const byte eventConditionType = 0;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ConditionConfig.Data>();
            var payloadTypeBuilder = builder.AllocateHashMap(ref root.PayloadTypes, maxEventKey + 1, 2);
            for (var key = 0; key <= maxEventKey; key++)
            {
                payloadTypeBuilder.Add(new EventSubscriberKey(key, eventConditionType), ConditionPayloadType.Int32);
            }

            var blob = builder.CreateBlobAssetReference<ConditionConfig.Data>(Allocator.Persistent);
            builder.Dispose();

            Manager.AddComponentData(Manager.CreateEntity(), new ConditionConfig { Value = blob });
            BlobAssetStore.TryAdd(ref blob);
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

        // Play back the structural changes the query queued (e.g. adding a TriggerQueryHit buffer to a routed entity).
        private void PlaybackStructuralChanges()
        {
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>().Update();
            Manager.CompleteAllTrackedJobs();
        }

        private void SetFirstFrame(Entity clip, bool value)
        {
            var gate = Manager.GetComponentData<PhysicsClipGate>(clip);
            gate.FirstFrame = (byte)(value ? 1 : 0);
            Manager.SetComponentData(clip, gate);
        }

        [Test]
        public void Nearest_PicksClosestCandidate_AndRoutesIntoCustomTarget()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self }
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self }
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self }
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                MaxTargets = 8,
                WriteHitBuffer = true,
                ValueMode = PhysicsTriggerQueryValueMode.DirectionSector,
                SectorCount = 8,
                SectorReference = PhysicsTriggerSectorReference.SelfForward,
                SectorPlane = PhysicsTriggerSectorPlane.XZ,
                SectorCustomUp = new float3(0, 1, 0)
            });

            var front = CreateCandidate(new float3(0, 0, 3)); // sector 0
            var right = CreateCandidate(new float3(3, 0, 0)); // sector 2
            var back = CreateCandidate(new float3(0, 0, -3)); // sector 4
            AddEvent(clip, front);
            AddEvent(clip, right);
            AddEvent(clip, back);

            // No hand-added buffer: the system must add TriggerQueryHit to the routed (Self) entity on demand. It
            // appears next tick, so the hits land on the SECOND query.
            RunQuery();
            PlaybackStructuralChanges();
            Assert.IsTrue(Manager.HasComponent<TriggerQueryHit>(clip),
                "the query added the hit buffer to the routed entity");

            RunQuery();
            PlaybackStructuralChanges();

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
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                MaxTargets = 2,
                WriteHitBuffer = true
            });

            for (var k = 0; k < 5; k++)
                AddEvent(clip, CreateCandidate(new float3(0, 0, 2 + k)));

            // First query adds the hit buffer (via ECB); the hits land on the second.
            RunQuery();
            PlaybackStructuralChanges();
            RunQuery();
            PlaybackStructuralChanges();

            Assert.AreEqual(2, Manager.GetBuffer<TriggerQueryHit>(clip).Length, "survivors past the cap are dropped");
        }

        [Test]
        public void ClearOnLost_WritesNullAndStateTransitionsOnce()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
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

        [Test]
        public void AllSurvivorsFanout_EightCandidatesAtCapSeven_DoesNotOverflow()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.AllSurvivorsFanout,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                MaxTargets = 7,
                WriteHitBuffer = true,
            });

            // Add farthest-first so each subsequent (nearer) candidate scores ABOVE the current worst and inserts
            // while the winners list is full — the exact path that grew FixedList64Bytes<Entity> past its capacity
            // of 7 before the fix. The 8th (best) survivor must evict the worst without overflowing.
            for (var k = 0; k < 8; k++)
                AddEvent(clip, CreateCandidate(new float3(0, 0, 9 - k))); // z = 9,8,...,2

            RunQuery();
            PlaybackStructuralChanges();
            RunQuery();
            PlaybackStructuralChanges();

            Assert.AreEqual(7, Manager.GetBuffer<TriggerQueryHit>(clip).Length,
                "cap 7 with 8 survivors: the worst is dropped, no overflow");
        }

        [Test]
        public void MultiWinner_ClearOnLost_ClearsRoutedSlotOnLoss()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.AllSurvivorsFanout,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                RouteSlot = PhysicsTriggerRouteSlot.Custom,
                MaxTargets = 4,
                ClearOnLost = true,
            });

            var candidate = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, candidate);

            RunQuery();
            Assert.AreEqual(candidate, Manager.GetComponentData<Targets>(clip).Custom, "winner routed into Custom");

            Manager.GetBuffer<StatefulTriggerEvent>(clip).Clear();

            RunQuery();
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<Targets>(clip).Custom,
                "multi-winner ClearOnLost must clear the routed slot on loss");
        }

        [Test]
        public void GraceFrames_HoldsWinnerForExactlyGraceFrames()
        {
            const int grace = 2;
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                ClearOnLost = true,
                GraceFrames = grace,
            });

            var candidate = CreateCandidate(new float3(0, 0, 2));
            AddEvent(clip, candidate);

            RunQuery();
            Assert.AreEqual(candidate, Manager.GetComponentData<Targets>(clip).Custom);

            Manager.GetBuffer<StatefulTriggerEvent>(clip).Clear();

            // The hold must last exactly GraceFrames query frames before the lost clear fires.
            for (var f = 0; f < grace; f++)
            {
                RunQuery();
                Assert.AreEqual(candidate, Manager.GetComponentData<Targets>(clip).Custom,
                    $"winner held during grace frame {f + 1}");
                Assert.AreEqual(candidate, Manager.GetComponentData<PhysicsTriggerQueryState>(clip).LastWinner);
            }

            RunQuery();
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<Targets>(clip).Custom,
                "after GraceFrames holds, the lost clear fires");
            Assert.AreEqual(Entity.Null, Manager.GetComponentData<PhysicsTriggerQueryState>(clip).LastWinner);
        }

        [Test]
        public void TabCycle_HoldsWinnerAcrossStayFrames_AdvancesOnlyOnRefireEdge()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.TabCycle,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                SectorReference = PhysicsTriggerSectorReference.SelfForward,
                SectorPlane = PhysicsTriggerSectorPlane.XZ,
                SectorCustomUp = new float3(0, 1, 0),
                SectorCount = 8,
            });

            var a = CreateCandidate(new float3(0, 0, 3)); // front (bearing 0)
            var b = CreateCandidate(new float3(3, 0, 0)); // right (bearing ~pi/2)
            AddEvent(clip, a);
            AddEvent(clip, b);

            RunQuery();
            var first = Manager.GetComponentData<Targets>(clip).Custom;
            Assert.AreNotEqual(Entity.Null, first);

            // Continuously active (Stay, no re-fire edge): the winner must NOT round-robin frame to frame.
            RunQuery();
            Assert.AreEqual(first, Manager.GetComponentData<Targets>(clip).Custom, "held winner across a Stay frame");
            RunQuery();
            Assert.AreEqual(first, Manager.GetComponentData<Targets>(clip).Custom, "still held after another Stay frame");

            // A re-fire edge (clip-activation FirstFrame) advances to the other survivor.
            SetFirstFrame(clip, true);
            RunQuery();
            SetFirstFrame(clip, false);
            Assert.AreNotEqual(first, Manager.GetComponentData<Targets>(clip).Custom,
                "the re-fire edge advances TabCycle to the next survivor");
        }

        [Test]
        public void FactionGate_ExcludesCandidatesWithoutFactionMember()
        {
            var clip = CreateQueryClip(new PhysicsTriggerQueryData
            {
                EventState = StatefulEventState.Stay,
                Selection = PhysicsTriggerQuerySelection.Nearest,
                RouteTo = new EntityLinkRef { ReadRootFrom = Target.Self },
                Gates = PhysicsTriggerGateFlags.FactionGate,
                FactionAllowMask = (1u << 0) | (1u << 1),
            });

            var untagged = CreateCandidate(new float3(0, 0, 2)); // nearest, but no FactionMember → excluded
            var tagged = CreateCandidate(new float3(0, 0, 4));
            Manager.AddComponentData(tagged, new FactionMember { Faction = 1 });
            AddEvent(clip, untagged);
            AddEvent(clip, tagged);

            RunQuery();

            Assert.AreEqual(tagged, Manager.GetComponentData<Targets>(clip).Custom,
                "untagged candidate must be excluded even though the faction-0 bit is allowed");
        }
    }
}