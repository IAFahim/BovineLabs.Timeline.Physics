using System;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Single source of truth for the runtime components a physics-track TARGET needs: per track type, the
    /// Active/State pair (Active added disabled), plus the shared accumulation buffers where the track produces
    /// motion. Add one <see cref="Ensure{TAnimated,TActive,TState}"/> line per new track.
    /// Deliberate exceptions: ChainFollow and SocketReturn are NOT ensured here — their Active/State only make
    /// sense on a rig provisioned by <c>ChainWeaponAuthoring</c> / <c>WeaponRecallAuthoring</c>; a clip bound to
    /// an entity without that rig is intentionally a no-op.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsTimelineBakingSystem : ISystem
    {
        [Flags]
        private enum EnsureFlags : byte
        {
            None = 0,

            /// <summary>The track writes motion into the shared PendingForce/PendingVelocity/reset seam.</summary>
            AccumulationBuffers = 1 << 0,

            /// <summary>The track rolls per-entity deterministic randomness (force scatter).</summary>
            ForceRandom = 1 << 1,
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var queuedBuffers = new NativeHashSet<Entity>(64, Allocator.Temp);

            Ensure<PhysicsLinearPIDAnimated, ActiveLinearPid, PhysicsLinearPIDState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers);
            Ensure<PhysicsAngularPIDAnimated, ActiveAngularPid, PhysicsAngularPIDState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers);
            Ensure<PhysicsForceAnimated, ActiveForce, PhysicsForceState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers | EnsureFlags.ForceRandom);
            Ensure<PhysicsVelocityAnimated, ActiveVelocity, PhysicsVelocityState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers);
            Ensure<PhysicsSplineFollowAnimated, ActiveSplineFollow, PhysicsSplineFollowState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers);
            EnsureStateless<PhysicsDragAnimated, ActiveDrag>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.AccumulationBuffers);

            Ensure<PhysicsRicochetAnimated, ActiveRicochet, PhysicsRicochetState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsFilterOverrideAnimated, ActiveFilterOverride, PhysicsFilterOverrideState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsShapeResizeAnimated, ActiveShapeResize, PhysicsShapeResizeState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsShapeSwapAnimated, ActiveShapeSwap, PhysicsShapeSwapState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsGravityOverrideAnimated, ActiveGravityOverride, PhysicsGravityOverrideState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsVelocityClampAnimated, ActiveVelocityClamp, PhysicsVelocityClampState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsKinematicOverrideAnimated, ActiveKinematicOverride, PhysicsKinematicOverrideState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);
            Ensure<PhysicsTeleportAnimated, ActiveTeleport, PhysicsTeleportState>(
                ref state, em, ref ecb, queuedBuffers, EnsureFlags.None);

            ecb.Playback(em);
            ecb.Dispose();

            queuedBuffers.Dispose();
        }

        private static void Ensure<TAnimated, TActive, TState>(ref SystemState state, EntityManager em,
            ref EntityCommandBuffer ecb, NativeHashSet<Entity> queuedBuffers, EnsureFlags flags)
            where TAnimated : unmanaged, IComponentData
            where TActive : unmanaged, IComponentData, IEnableableComponent
            where TState : unmanaged, IComponentData
        {
            foreach (var target in Targets<TAnimated>(ref state))
            {
                EnsureActiveState<TActive, TState>(ref ecb, target, em);
                ApplyFlags(ref ecb, target, em, queuedBuffers, flags);
            }
        }

        private static void EnsureStateless<TAnimated, TActive>(ref SystemState state, EntityManager em,
            ref EntityCommandBuffer ecb, NativeHashSet<Entity> queuedBuffers, EnsureFlags flags)
            where TAnimated : unmanaged, IComponentData
            where TActive : unmanaged, IComponentData, IEnableableComponent
        {
            foreach (var target in Targets<TAnimated>(ref state))
            {
                EnsureActive<TActive>(ref ecb, target, em);
                ApplyFlags(ref ecb, target, em, queuedBuffers, flags);
            }
        }

        private static NativeArray<Entity> Targets<TAnimated>(ref SystemState state)
            where TAnimated : unmanaged, IComponentData
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, TAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab);
            var query = state.GetEntityQuery(builder);

            var bindings = query.ToComponentDataArray<TrackBinding>(Allocator.Temp);
            var targets = new NativeArray<Entity>(bindings.Length, Allocator.Temp);
            var count = 0;
            foreach (var binding in bindings)
            {
                if (binding.Value != Entity.Null)
                    targets[count++] = binding.Value;
            }

            bindings.Dispose();
            return targets.GetSubArray(0, count);
        }

        private static void ApplyFlags(ref EntityCommandBuffer ecb, Entity target, EntityManager em,
            NativeHashSet<Entity> queuedBuffers, EnsureFlags flags)
        {
            if ((flags & EnsureFlags.ForceRandom) != 0 && !em.HasComponent<PhysicsForceRandom>(target))
                ecb.AddComponent<PhysicsForceRandom>(target);

            if ((flags & EnsureFlags.AccumulationBuffers) != 0)
                EnsureAccumulationBuffers(ref ecb, target, em, queuedBuffers);
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
