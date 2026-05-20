using BovineLabs.Timeline.Data;
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

            var queuedLinearPid = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedAngularPid = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedForce = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedVelocity = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedDrag = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedBuffers = new NativeHashSet<Entity>(64, Allocator.Temp);

            // 1. Linear PID
            foreach (var (binding, clipEntity) in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsLinearPIDAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .WithEntityAccess())
            {
                if (!em.HasComponent<PhysicsLinearPIDState>(clipEntity))
                    ecb.AddComponent<PhysicsLinearPIDState>(clipEntity);

                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveLinearPid>(target) && queuedLinearPid.Add(target))
                {
                    ecb.AddComponent<ActiveLinearPid>(target);
                    ecb.SetComponentEnabled<ActiveLinearPid>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            // 2. Angular PID
            foreach (var (binding, clipEntity) in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsAngularPIDAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .WithEntityAccess())
            {
                if (!em.HasComponent<PhysicsAngularPIDState>(clipEntity))
                    ecb.AddComponent<PhysicsAngularPIDState>(clipEntity);

                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveAngularPid>(target) && queuedAngularPid.Add(target))
                {
                    ecb.AddComponent<ActiveAngularPid>(target);
                    ecb.SetComponentEnabled<ActiveAngularPid>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            // 3. Force
            foreach (var (binding, clipEntity) in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsForceAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .WithEntityAccess())
            {
                if (!em.HasComponent<PhysicsForceState>(clipEntity))
                    ecb.AddComponent<PhysicsForceState>(clipEntity);

                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveForce>(target) && queuedForce.Add(target))
                {
                    ecb.AddComponent<ActiveForce>(target);
                    ecb.SetComponentEnabled<ActiveForce>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            // 4. Velocity
            foreach (var (binding, clipEntity) in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsVelocityAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .WithEntityAccess())
            {
                if (!em.HasComponent<PhysicsVelocityState>(clipEntity))
                    ecb.AddComponent<PhysicsVelocityState>(clipEntity);

                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveVelocity>(target) && queuedVelocity.Add(target))
                {
                    ecb.AddComponent<ActiveVelocity>(target);
                    ecb.SetComponentEnabled<ActiveVelocity>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            // 5. Drag
            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsDragAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveDrag>(target) && queuedDrag.Add(target))
                {
                    ecb.AddComponent<ActiveDrag>(target);
                    ecb.SetComponentEnabled<ActiveDrag>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            ecb.Playback(em);
            ecb.Dispose();

            queuedLinearPid.Dispose();
            queuedAngularPid.Dispose();
            queuedForce.Dispose();
            queuedVelocity.Dispose();
            queuedDrag.Dispose();
            queuedBuffers.Dispose();
        }

        private static void EnsureAccumulationBuffers(ref EntityCommandBuffer ecb, Entity target, EntityManager em,
            NativeHashSet<Entity> queued)
        {
            if (!queued.Add(target)) return;
            if (!em.HasBuffer<PendingForce>(target)) ecb.AddBuffer<PendingForce>(target);
            if (!em.HasBuffer<PendingVelocity>(target)) ecb.AddBuffer<PendingVelocity>(target);
        }
    }
}