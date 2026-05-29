#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented", Justification = "Using see cref")]
    public static class RicochetDebugSystem
    {
        [ConfigVar("ricochetgizmo.draw-enabled", true, "Enable the ricochet gizmo drawer.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("ricochetgizmo.ray-color-1", 0.0f, 0.8f, 1.0f, 0.7f, "Color for alternating ray segments (Cyan)")]
        public static readonly SharedStatic<Color> RayColor1 = SharedStatic<Color>.GetOrCreate<Tags.RayColor1>();

        [ConfigVar("ricochetgizmo.ray-color-2", 0.6f, 0.2f, 1.0f, 0.7f, "Color for alternating ray segments (Purple)")]
        public static readonly SharedStatic<Color> RayColor2 = SharedStatic<Color>.GetOrCreate<Tags.RayColor2>();

        [ConfigVar("ricochetgizmo.terminal-color", 1.0f, 0.4f, 0.2f, 0.9f, "Color for terminal hit (Coral)")]
        public static readonly SharedStatic<Color> TerminalColor = SharedStatic<Color>.GetOrCreate<Tags.TerminalColor>();

        [ConfigVar("ricochetgizmo.origin-color", 1.0f, 1.0f, 1.0f, 0.8f, "Color for ray origin (White)")]
        public static readonly SharedStatic<Color> OriginColor = SharedStatic<Color>.GetOrCreate<Tags.OriginColor>();

        [ConfigVar("ricochetgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        [ConfigVar("ricochetgizmo.segments", 12, "Sphere arc segment count.")]
        public static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<Tags.Segments>();

        private struct Tags
        {
            public struct Enabled { }
            public struct RayColor1 { }
            public struct RayColor2 { }
            public struct TerminalColor { }
            public struct OriginColor { }
            public struct TextColor { }
            public struct Segments { }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [BurstCompile]
    public partial struct PhysicsRicochetGizmoSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _colliderLookup.Update(ref state);

            if (!TimelineDebugUtility.TryGetDrawer<PhysicsRicochetGizmoSystem>(
                    ref state, RicochetDebugSystem.Enabled.Data, out var drawer))
                return;

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            state.Dependency = new DrawRicochetJob
            {
                Drawer         = drawer,
                Segments       = math.clamp(RicochetDebugSystem.Segments.Data, 4, 32),
                RayColor1      = RicochetDebugSystem.RayColor1.Data,
                RayColor2      = RicochetDebugSystem.RayColor2.Data,
                TerminalColor  = RicochetDebugSystem.TerminalColor.Data,
                OriginColor    = RicochetDebugSystem.OriginColor.Data,
                TextColor      = RicochetDebugSystem.TextColor.Data,
                TransformLookup = _localToWorldLookup,
                TargetsLookup  = _targetsLookup,
                ColliderLookup = _colliderLookup,
                CollisionWorld = collisionWorld
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawRicochetJob : IJobEntity
        {
            public Drawer Drawer;
            public int Segments;
            public Color RayColor1;
            public Color RayColor2;
            public Color TerminalColor;
            public Color OriginColor;
            public Color TextColor;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            
            [ReadOnly] public CollisionWorld CollisionWorld;

            public unsafe void Execute(Entity entity, in TrackBinding binding, in PhysicsRicochetAnimated animated)
            {
                var d = animated.AuthoredData;
                var targets = TargetsLookup.TryGetComponent(entity, out var t) ? t : default;
                
                float3 origin = float3.zero;
                float3 direction = math.forward();
                
                var originEntity = ResolveTarget(entity, d.RayOrigin, targets);
                if (originEntity != Entity.Null && TransformLookup.HasComponent(originEntity))
                {
                    origin = TransformLookup[originEntity].Position;
                }
                
                var dirEntity = ResolveTarget(entity, d.RayDirection, targets);
                if (dirEntity != Entity.Null && TransformLookup.HasComponent(dirEntity))
                {
                    direction = math.rotate(TransformLookup[dirEntity].Rotation, math.forward());
                }

                var remainingDistance = d.MaxDistance;
                var bounceCount = 0;
                
                var currentPos = origin;
                var currentDir = math.normalize(direction);

                // Draw Origin
                Drawer.Sphere(origin, 0.15f, Segments, OriginColor);
                Drawer.Text32(origin + new float3(0f, 0.3f, 0f), "Origin", TextColor, 10f);
                
                while (bounceCount <= d.MaxBounces && remainingDistance > 0)
                {
                    var rayColor = (bounceCount % 2 == 0) ? RayColor1 : RayColor2;

                    var rayInput = new RaycastInput
                    {
                        Start = currentPos,
                        End = currentPos + currentDir * remainingDistance,
                        Filter = new CollisionFilter
                        {
                            BelongsTo = ~0u,
                            CollidesWith = d.RicochetMask | d.TerminalHitMask,
                            GroupIndex = 0
                        }
                    };

                    if (!CollisionWorld.CastRay(rayInput, out var hit))
                    {
                        // No hit, draw line to end of remaining distance
                        var endPos = currentPos + currentDir * remainingDistance;
                        Drawer.Line(currentPos, endPos, rayColor);
                        Drawer.Text32(endPos + new float3(0f, 0.2f, 0f), "Max Range", new Color(0.6f, 0.6f, 0.6f, 0.8f), 8f);
                        break;
                    }
                    
                    var dist = math.distance(currentPos, hit.Position);
                    remainingDistance -= dist;
                    var hitEntity = hit.Entity;
                    
                    var surfaceBelongsTo = 0u;
                    if (ColliderLookup.HasComponent(hitEntity))
                    {
                        var col = ColliderLookup[hitEntity];
                        if (col.IsValid)
                        {
                            surfaceBelongsTo = col.Value.Value.GetCollisionFilter().BelongsTo;
                        }
                    }

                    var grazingAngle = math.acos(math.abs(math.dot(currentDir, hit.SurfaceNormal)));

                    if (grazingAngle >= d.MinGrazingAngle || (surfaceBelongsTo & d.TerminalHitMask) != 0)
                    {
                        // Terminal hit
                        Drawer.Line(currentPos, hit.Position, rayColor);
                        Drawer.Sphere(hit.Position, 0.2f, Segments, TerminalColor);
                        
                        // Draw hit normal
                        Drawer.Arrow(hit.Position, hit.SurfaceNormal * 0.5f, new Color(TerminalColor.r, TerminalColor.g, TerminalColor.b, 0.5f));
                        
                        Drawer.Text32(hit.Position + new float3(0f, 0.4f, 0f), "Terminal Hit", TerminalColor, 12f);
                        
                        // Optional route visualization
                        if (d.HitConditionKey != 0)
                        {
                            var routeTarget = ResolveTarget(entity, d.HitRouteTo, targets);
                            if (routeTarget != Entity.Null && TransformLookup.TryGetComponent(routeTarget, out var routeLtw))
                            {
                                Drawer.Line(hit.Position, routeLtw.Position, new Color(TerminalColor.r, TerminalColor.g, TerminalColor.b, 0.3f));
                                Drawer.Text32(routeLtw.Position + new float3(0f, 0.5f, 0f), "Route Target", TextColor, 8f);
                            }
                        }
                        
                        break;
                    }
                    
                    if ((surfaceBelongsTo & d.RicochetMask) != 0)
                    {
                        // Ricochet hit
                        Drawer.Line(currentPos, hit.Position, rayColor);
                        Drawer.Sphere(hit.Position, 0.1f, Segments, rayColor);
                        
                        // Draw bounce index
                        Drawer.Text32(hit.Position + new float3(0f, 0.2f, 0f), $"Bounce {bounceCount+1}", rayColor, 10f);

                        // Draw reflection normal faint
                        Drawer.Arrow(hit.Position, hit.SurfaceNormal * 0.4f, new Color(0.8f, 0.8f, 0.8f, 0.3f));

                        currentDir = currentDir - 2f * math.dot(currentDir, hit.SurfaceNormal) * hit.SurfaceNormal;
                        currentPos = hit.Position + currentDir * 0.01f;
                        bounceCount++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            
            private Entity ResolveTarget(Entity self, Target target, in Targets targets)
            {
                if (target == Target.None) return self;
                var baseTarget = targets.Get(target, self);
                return baseTarget != Entity.Null ? baseTarget : self;
            }
        }
    }
}
#endif
