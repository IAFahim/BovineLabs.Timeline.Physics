using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    public partial struct PhysicsVelocityOverrideSystem : ISystem
    {
        private EntityQuery _query;
        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private ComponentTypeHandle<PhysicsVelocity> _physicsVelocityHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<OverrideJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, PhysicsVelocityState>()
                .WithAll<ActiveVelocity, LocalTransform>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);
            _velocityStateHandle = state.GetComponentTypeHandle<PhysicsVelocityState>();
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _physicsVelocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            _entityHandle.Update(ref state);
            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _physicsVelocityHandle.Update(ref state);
            _targetsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            state.Dependency = new OverrideJob
            {
                EntityHandle = _entityHandle,
                ActiveVelocityHandle = _activeVelocityHandle,
                VelocityStateHandle = _velocityStateHandle,
                TransformHandle = _transformHandle,
                PhysicsVelocityHandle = _physicsVelocityHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
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
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
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

                    var targets = TargetsLookup.HasComponent(entities[i]) ? TargetsLookup[entities[i]] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, entities[i], targets,
                        LinkSources, Links, StatLookup);

                    if (math.abs(multiplier) < 1e-5f) continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                        in TransformLookup, out var linVel);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                        in TransformLookup, out var angVel);

                    var pv = physicsVelocities[i];
                    pv.Linear = linVel * multiplier;
                    pv.Angular = angVel * multiplier;
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