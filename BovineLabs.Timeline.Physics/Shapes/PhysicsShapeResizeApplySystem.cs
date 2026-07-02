using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using CapsuleCollider = Unity.Physics.CapsuleCollider;
using Collider = Unity.Physics.Collider;
using SphereCollider = Unity.Physics.SphereCollider;

namespace BovineLabs.Timeline.Physics.Shapes
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsShapeResizeApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveShapeResize> _activeHandle;
        private ComponentTypeHandle<PhysicsShapeResizeState> _stateHandle;
        private ComponentTypeHandle<PhysicsCollider> _colliderHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveShapeResize>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsShapeResizeState>();
            _colliderHandle = state.GetComponentTypeHandle<PhysicsCollider>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsShapeResizeState, PhysicsCollider>()
                .WithAll<ActiveShapeResize>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();

            state.RequireForUpdate<BLLogger>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _colliderHandle.Update(ref state);

            state.Dependency = new ApplyJob
            {
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                ColliderHandle = _colliderHandle,
                Logger = SystemAPI.GetSingleton<BLLogger>()
            }.ScheduleParallel(_query, state.Dependency);
        }

        private const byte Unsupported = 255;

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<ActiveShapeResize> ActiveHandle;
            public ComponentTypeHandle<PhysicsShapeResizeState> StateHandle;
            public ComponentTypeHandle<PhysicsCollider> ColliderHandle;
            public BLLogger Logger;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref StateHandle);
                var colliders = chunk.GetNativeArray(ref ColliderHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];
                    var collider = colliders[i];
                    if (!collider.IsValid) continue;

                    if (!collider.IsUnique)
                    {
                        if (isActive && !state.WarnedShared)
                        {
                            Logger.LogWarning512(
                                "PhysicsShapeResize targets a shared collider blob; the resize was skipped. Enable 'Force Unique' " +
                                "on the bound body's collider authoring so the geometry can be modified per instance.");
                            state.WarnedShared = true;
                            states[i] = state;
                        }

                        continue;
                    }

                    var ptr = (Collider*)collider.Value.GetUnsafePtr();

                    if (isActive && !state.Fired)
                    {
                        if (!Capture(ptr, ref state))
                        {
                            Logger.LogWarning512(
                                "PhysicsShapeResize only supports Sphere/Box/Capsule/Cylinder colliders; convex/mesh/compound were skipped.");
                            state.Type = Unsupported;
                        }

                        state.Fired = true;
                        states[i] = state;
                        if (state.Type != Unsupported) Apply(ptr, in state, actives[i].Config.Scale);
                    }
                    else if (isActive && state.Fired)
                    {
                        if (state.Type != Unsupported) Apply(ptr, in state, actives[i].Config.Scale);
                    }
                    else if (!isActive && state.Fired)
                    {
                        if (state.Type != Unsupported && actives[i].Config.RestoreOnExit)
                            Apply(ptr, in state, new float3(1f));

                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }

            private static unsafe bool Capture(Collider* ptr, ref PhysicsShapeResizeState s)
            {
                switch (ptr->Type)
                {
                    case ColliderType.Sphere:
                    {
                        var g = ((SphereCollider*)ptr)->Geometry;
                        s.OrigCenter = g.Center;
                        s.OrigRadius = g.Radius;
                        s.Type = (byte)ColliderType.Sphere;
                        return true;
                    }

                    case ColliderType.Box:
                    {
                        var g = ((BoxCollider*)ptr)->Geometry;
                        s.OrigCenter = g.Center;
                        s.OrigB = g.Size;
                        s.OrigRadius = g.BevelRadius;
                        s.OrigOrient = g.Orientation;
                        s.Type = (byte)ColliderType.Box;
                        return true;
                    }

                    case ColliderType.Capsule:
                    {
                        var g = ((CapsuleCollider*)ptr)->Geometry;
                        s.OrigCenter = g.Vertex0;
                        s.OrigB = g.Vertex1;
                        s.OrigRadius = g.Radius;
                        s.Type = (byte)ColliderType.Capsule;
                        return true;
                    }

                    case ColliderType.Cylinder:
                    {
                        var g = ((CylinderCollider*)ptr)->Geometry;
                        s.OrigCenter = g.Center;
                        s.OrigB = new float3(g.Height, g.BevelRadius, g.SideCount);
                        s.OrigRadius = g.Radius;
                        s.OrigOrient = g.Orientation;
                        s.Type = (byte)ColliderType.Cylinder;
                        return true;
                    }

                    default:
                        return false;
                }
            }

            private static unsafe void Apply(Collider* ptr, in PhysicsShapeResizeState s, float3 scale)
            {
                if (ptr->Type != (ColliderType)s.Type) return;

                switch ((ColliderType)s.Type)
                {
                    case ColliderType.Sphere:
                    {
                        ((SphereCollider*)ptr)->Geometry = new SphereGeometry
                        {
                            Center = s.OrigCenter * scale,
                            Radius = math.max(s.OrigRadius * scale.x, 1e-4f)
                        };
                        break;
                    }

                    case ColliderType.Box:
                    {
                        var size = math.max(s.OrigB * scale, 1e-4f);
                        ((BoxCollider*)ptr)->Geometry = new BoxGeometry
                        {
                            Center = s.OrigCenter * scale,
                            Size = size,
                            Orientation = s.OrigOrient,
                            BevelRadius = math.clamp(s.OrigRadius * scale.x, 0f, math.cmin(size) * 0.5f)
                        };
                        break;
                    }

                    case ColliderType.Capsule:
                    {
                        ((CapsuleCollider*)ptr)->Geometry = new CapsuleGeometry
                        {
                            Vertex0 = s.OrigCenter * scale,
                            Vertex1 = s.OrigB * scale,
                            Radius = math.max(s.OrigRadius * scale.x, 1e-4f)
                        };
                        break;
                    }

                    case ColliderType.Cylinder:
                    {
                        var radius = math.max(s.OrigRadius * scale.x, 1e-4f);
                        var height = math.max(s.OrigB.x * scale.y, 2e-4f);
                        ((CylinderCollider*)ptr)->Geometry = new CylinderGeometry
                        {
                            Center = s.OrigCenter * scale,
                            Orientation = s.OrigOrient,
                            Height = height,
                            Radius = radius,
                            BevelRadius = math.clamp(s.OrigB.y * scale.x, 0f, math.min(radius, height * 0.5f)),
                            SideCount = (int)s.OrigB.z
                        };
                        break;
                    }
                }
            }

        }
    }
}
