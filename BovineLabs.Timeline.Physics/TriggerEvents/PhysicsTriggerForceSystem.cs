using BovineLabs.Core.PhysicsStates;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PhysicsTriggerForceSystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private BufferLookup<Stat> _statLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>();
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _statLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);

            var pendingForces = new NativeQueue<PendingForce>(state.WorldUpdateAllocator);

            state.Dependency = new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                PendingForces = pendingForces.AsParallelWriter(),
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                LtwLookup = _ltwLookup,
                StatLookup = _statLookup,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                PendingForces = pendingForces,
                DeltaTime = SystemAPI.Time.DeltaTime,
                VelocityLookup = _velocityLookup,
                MassLookup = _massLookup
            }.Schedule(state.Dependency);
        }

        private struct PendingForce
        {
            public Entity Target;
            public float3 Force;
            public PhysicsForceMode Mode;
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct GatherJob : IJobEntity
        {
            public float DeltaTime;
            public NativeQueue<PendingForce>.ParallelWriter PendingForces;
            
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute(in TrackBinding binding, in PhysicsTriggerForceData cfg)
            {
                var self = binding.Value;
                if (self == Entity.Null) return;

                var targets = TargetsLookup.HasComponent(self) ? TargetsLookup[self] : default;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    foreach (var evt in triggers)
                    {
                        if (evt.State != cfg.EventState || !LtwLookup.HasComponent(evt.EntityB)) continue;
                        var selfPos = LtwLookup[self].Position;
                        var otherPos = LtwLookup[evt.EntityB].Position;
                        var midpoint = (selfPos + otherPos) * 0.5f;
                        ProcessEvent(self, evt.EntityB, in cfg, midpoint, in targets);
                    }
                }

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    foreach (var evt in collisions)
                    {
                        if (evt.State != cfg.EventState || !LtwLookup.HasComponent(evt.EntityB)) continue;
                        var selfPos = LtwLookup[self].Position;
                        var otherPos = LtwLookup[evt.EntityB].Position;
                        var hasContact = evt.TryGetDetails(out var details);
                        var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                        ProcessEvent(self, evt.EntityB, in cfg, pt, in targets);
                    }
                }
            }

            private void ProcessEvent(Entity self, Entity other, in PhysicsTriggerForceData cfg, float3 contactPoint, in Targets targets)
            {
                if (!PhysicsTriggerResolution.TryResolveLinkedTarget(
                        cfg.ApplyTo, cfg.ApplyToLinkKey, self, other, targets, LinkSources,
                        Links, out var targetToApply)) return;

                float multiplier = 1f;
                if (cfg.StrengthStat.Value != 0)
                {
                    if (PhysicsTriggerResolution.TryResolveLinkedTarget(
                            cfg.ReadStatFrom, cfg.ReadStatLinkKey, self, other, targets, LinkSources,
                            Links, out var statEntity))
                    {
                        if (StatLookup.TryGetBuffer(statEntity, out var statsBuffer))
                        {
                            multiplier = statsBuffer.AsMap().GetValueFloat(cfg.StrengthStat);
                        }
                    }
                }

                if (math.abs(multiplier) < 1e-5f || math.abs(cfg.Magnitude) < 1e-5f) return;

                var selfLtw = LtwLookup[self];
                
                // If target doesn't have LocalToWorld, default to collision other for position resolving
                var targetLtw = LtwLookup.HasComponent(targetToApply) ? LtwLookup[targetToApply] : LtwLookup[other];
                var magnitude = cfg.Magnitude * multiplier;

                float3 force = float3.zero;

                switch (cfg.ForceType)
                {
                    case PhysicsTriggerForceType.Directional:
                        force = math.rotate(selfLtw.Rotation, cfg.Direction) * magnitude;
                        break;
                    case PhysicsTriggerForceType.Radial:
                    {
                        PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfLtw, targetLtw, contactPoint, out var origin);
                        var dir = targetLtw.Position - origin;
                        var lenSq = math.lengthsq(dir);
                        if (lenSq > 1e-5f)
                        {
                            force = (dir / math.sqrt(lenSq)) * magnitude;
                        }
                        break;
                    }
                    case PhysicsTriggerForceType.Vortex:
                    {
                        PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfLtw, targetLtw, contactPoint, out var origin);
                        var offset = targetLtw.Position - origin;
                        var up = math.rotate(selfLtw.Rotation, math.up());
                        var projOffset = offset - math.dot(offset, up) * up;
                        var lenSq = math.lengthsq(projOffset);
                        if (lenSq > 1e-5f)
                        {
                            force = math.normalize(math.cross(up, projOffset)) * magnitude;
                        }
                        break;
                    }
                }

                if (math.lengthsq(force) > 1e-5f)
                {
                    PendingForces.Enqueue(new PendingForce
                    {
                        Target = targetToApply,
                        Force = force,
                        Mode = cfg.Mode
                    });
                }
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<PendingForce> PendingForces;
            public float DeltaTime;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;

            public void Execute()
            {
                while (PendingForces.TryDequeue(out var pending))
                {
                    if (!VelocityLookup.TryGetComponent(pending.Target, out var velocity)) continue;

                    var mass = MassLookup.TryGetComponent(pending.Target, out var m)
                        ? m
                        : Unity.Physics.PhysicsMass.CreateKinematic(Unity.Physics.MassProperties.UnitSphere);

                    var t = pending.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;

                    PhysicsMath.ApplyLinearForce(velocity, mass, pending.Force, t, out var newVelocity);
                    VelocityLookup[pending.Target] = newVelocity;
                }
            }
        }
    }
}