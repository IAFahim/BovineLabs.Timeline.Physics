using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.VelocityOverrides
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsVelocityOverrideSystem : ISystem
    {
        private EntityQuery _query;
        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;
        private ComponentTypeHandle<PhysicsVelocity> _physicsVelocityHandle;

        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<OverrideJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, PhysicsVelocityState>()
                .WithAll<ActiveVelocity, LocalToWorld>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);
            _velocityStateHandle = state.GetComponentTypeHandle<PhysicsVelocityState>();
            _physicsVelocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();

            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            _entityHandle.Update(ref state);
            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);
            _physicsVelocityHandle.Update(ref state);
            _targetsLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            state.Dependency = new OverrideJob
            {
                EntityHandle = _entityHandle,
                ActiveVelocityHandle = _activeVelocityHandle,
                VelocityStateHandle = _velocityStateHandle,
                PhysicsVelocityHandle = _physicsVelocityHandle,
                TargetsLookup = _targetsLookup,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct OverrideJob : IJobChunkWorkerBeginEnd
        {
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveVelocity> ActiveVelocityHandle;
            public ComponentTypeHandle<PhysicsVelocityState> VelocityStateHandle;
            public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var velocities = chunk.GetNativeArray(ref ActiveVelocityHandle);
                var states = chunk.GetNativeArray(ref VelocityStateHandle);
                var physicsVelocities = chunk.GetNativeArray(ref PhysicsVelocityHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var config = velocities[i].Config;
                    var s = states[i];

                    var isSet = config.Mode == PhysicsVelocityMode.SetContinuous ||
                                config.Mode == PhysicsVelocityMode.SetInstant;
                    if (!isSet) continue;

                    var isInstant = config.Mode == PhysicsVelocityMode.SetInstant;
                    if (isInstant && s.Fired) continue;

                    var targets = TargetsLookup.TryGetComponent(entities[i], out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, entities[i], targets,
                        LinkSources, Links, StatLookup);

                    if (!math.isfinite(multiplier) || math.abs(multiplier) < 1e-5f) continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                        in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var linVel);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                        in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var angVel);

                    var pv = physicsVelocities[i];
                    pv.Linear = linVel * multiplier;
                    pv.Angular = angVel * multiplier;
                    if (!math.all(math.isfinite(pv.Linear)) || !math.all(math.isfinite(pv.Angular))) continue;
                    physicsVelocities[i] = pv;

                    if (isInstant)
                    {
                        s.Fired = true;
                        states[i] = s;
                    }
                }
            }
        }
    }
}