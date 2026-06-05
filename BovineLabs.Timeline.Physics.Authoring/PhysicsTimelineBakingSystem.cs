namespace BovineLabs.Timeline.Physics.Authoring
{
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.Physics.Data;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;

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

                if (!em.HasComponent<ActiveLinearPid>(target))
                {
                    ecb.AddComponent<ActiveLinearPid>(target);
                    ecb.SetComponentEnabled<ActiveLinearPid>(target, false);
                    ecb.AddComponent<PhysicsLinearPIDState>(target);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsAngularPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveAngularPid>(target))
                {
                    ecb.AddComponent<ActiveAngularPid>(target);
                    ecb.SetComponentEnabled<ActiveAngularPid>(target, false);
                    ecb.AddComponent<PhysicsAngularPIDState>(target);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsForceAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveForce>(target))
                {
                    ecb.AddComponent<ActiveForce>(target);
                    ecb.SetComponentEnabled<ActiveForce>(target, false);
                    ecb.AddComponent<PhysicsForceState>(target);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsVelocityAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveVelocity>(target))
                {
                    ecb.AddComponent<ActiveVelocity>(target);
                    ecb.SetComponentEnabled<ActiveVelocity>(target, false);
                    ecb.AddComponent<PhysicsVelocityState>(target);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsDragAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveDrag>(target))
                {
                    ecb.AddComponent<ActiveDrag>(target);
                    ecb.SetComponentEnabled<ActiveDrag>(target, false);
                }

                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsRicochetAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveRicochet>(target))
                {
                    ecb.AddComponent<ActiveRicochet>(target);
                    ecb.SetComponentEnabled<ActiveRicochet>(target, false);
                    ecb.AddComponent<PhysicsRicochetState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsFilterOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveFilterOverride>(target))
                {
                    ecb.AddComponent<ActiveFilterOverride>(target);
                    ecb.SetComponentEnabled<ActiveFilterOverride>(target, false);
                    ecb.AddComponent<PhysicsFilterOverrideState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsGravityOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveGravityOverride>(target))
                {
                    ecb.AddComponent<ActiveGravityOverride>(target);
                    ecb.SetComponentEnabled<ActiveGravityOverride>(target, false);
                    ecb.AddComponent<PhysicsGravityOverrideState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsVelocityClampAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveVelocityClamp>(target))
                {
                    ecb.AddComponent<ActiveVelocityClamp>(target);
                    ecb.SetComponentEnabled<ActiveVelocityClamp>(target, false);
                    ecb.AddComponent<PhysicsVelocityClampState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsKinematicOverrideAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveKinematicOverride>(target))
                {
                    ecb.AddComponent<ActiveKinematicOverride>(target);
                    ecb.SetComponentEnabled<ActiveKinematicOverride>(target, false);
                    ecb.AddComponent<PhysicsKinematicOverrideState>(target);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();

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
