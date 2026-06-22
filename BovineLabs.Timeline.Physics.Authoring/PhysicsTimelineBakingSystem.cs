using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsTimelineBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var queuedBuffers = new NativeHashSet<Entity>(64, Allocator.Temp);

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsLinearPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveLinearPid, PhysicsLinearPIDState>(ref ecb, target, em);
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsAngularPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveAngularPid, PhysicsAngularPIDState>(ref ecb, target, em);
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsForceAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveForce, PhysicsForceState>(ref ecb, target, em);
                if (!em.HasComponent<PhysicsForceRandom>(target)) ecb.AddComponent<PhysicsForceRandom>(target);
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsVelocityAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveVelocity, PhysicsVelocityState>(ref ecb, target, em);
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsDragAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActive<ActiveDrag>(ref ecb, target, em);
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsRicochetAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveRicochet, PhysicsRicochetState>(ref ecb, target, em);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsFilterOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveFilterOverride, PhysicsFilterOverrideState>(ref ecb, target, em);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsGravityOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveGravityOverride, PhysicsGravityOverrideState>(ref ecb, target, em);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsVelocityClampAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveVelocityClamp, PhysicsVelocityClampState>(ref ecb, target, em);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsKinematicOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;
                EnsureActiveState<ActiveKinematicOverride, PhysicsKinematicOverrideState>(ref ecb, target, em);
            }

            ecb.Playback(em);
            ecb.Dispose();

            queuedBuffers.Dispose();
        }

        private static void EnsureActiveState<TActive, TState>(ref EntityCommandBuffer ecb, Entity target,
            EntityManager em)
            where TActive : unmanaged, IComponentData, IEnableableComponent
            where TState : unmanaged, IComponentData
        {
            if (em.HasComponent<TActive>(target)) return;
            ecb.AddComponent<TActive>(target);
            ecb.SetComponentEnabled<TActive>(target, false);
            ecb.AddComponent<TState>(target);
        }

        private static void EnsureActive<TActive>(ref EntityCommandBuffer ecb, Entity target, EntityManager em)
            where TActive : unmanaged, IComponentData, IEnableableComponent
        {
            if (em.HasComponent<TActive>(target)) return;
            ecb.AddComponent<TActive>(target);
            ecb.SetComponentEnabled<TActive>(target, false);
        }

        private static void EnsureAccumulationBuffers(ref EntityCommandBuffer ecb, Entity target, EntityManager em,
            NativeHashSet<Entity> queued)
        {
            if (!queued.Add(target)) return;
            if (!em.HasBuffer<PendingForce>(target)) ecb.AddBuffer<PendingForce>(target);
            if (!em.HasBuffer<PendingVelocity>(target)) ecb.AddBuffer<PendingVelocity>(target);

            if (!em.HasComponent<PendingVelocityReset>(target))
            {
                ecb.AddComponent<PendingVelocityReset>(target);
                ecb.SetComponentEnabled<PendingVelocityReset>(target, false);
            }
        }
    }
}