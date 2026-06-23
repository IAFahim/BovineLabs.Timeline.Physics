using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Sockets
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct SocketReturnApplySystem : ISystem
    {
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveSocketReturn> _activeHandle;
        private ComponentTypeHandle<SocketReturnState> _stateHandle;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<WeaponSocket> _socketHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);

            _entityHandle = state.GetEntityTypeHandle();
            _activeHandle = state.GetComponentTypeHandle<ActiveSocketReturn>(true);
            _stateHandle = state.GetComponentTypeHandle<SocketReturnState>();
            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _socketHandle = state.GetComponentTypeHandle<WeaponSocket>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<SocketReturnState, PhysicsVelocity>()
                .WithAll<ActiveSocketReturn, LocalToWorld>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 1e-5f) return;

            _targetsLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _entityHandle.Update(ref state);
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _velocityHandle.Update(ref state);
            _socketHandle.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new DriveJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                VelocityHandle = _velocityHandle,
                SocketHandle = _socketHandle,
                TargetsLookup = _targetsLookup,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup,
                ECB = ecb
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct DriveJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveSocketReturn> ActiveHandle;
            public ComponentTypeHandle<SocketReturnState> StateHandle;
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            public ComponentTypeHandle<WeaponSocket> SocketHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var states = chunk.GetNativeArray(ref StateHandle);
                var velocities = chunk.GetNativeArray(ref VelocityHandle);

                var hasSocket = chunk.Has(ref SocketHandle);
                var sockets = hasSocket ? chunk.GetNativeArray(ref SocketHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var body = entities[i];
                    var config = actives[i].Config;
                    var spring = states[i];
                    var velocity = velocities[i];

                    PhysicsMath.ResolveTransform(body, in LocalTransformLookup, in LocalToWorldLookup,
                        in ParentLookup, out var currentPosition, out var currentRotation);

                    var socketEntity = body;
                    if (config.Socket != Target.Self && config.Socket != Target.None &&
                        TargetsLookup.TryGetComponent(body, out var targets))
                        socketEntity = targets.Get(config.Socket, body);

                    PhysicsMath.ResolveTransform(socketEntity, in LocalTransformLookup, in LocalToWorldLookup,
                        in ParentLookup, out var socketPosition, out var socketRotation);

                    var goalPosition = socketPosition + math.rotate(socketRotation, config.LocalPosition);
                    var goalRotation = math.mul(socketRotation, config.LocalRotation);

                    if (!spring.Initialized)
                    {
                        spring.LinearVelocity = velocity.Linear;
                        spring.AngularVelocity = velocity.Angular;
                        spring.Initialized = true;
                    }

                    SpringMath.CriticalSpring(currentPosition, spring.LinearVelocity, goalPosition, float3.zero,
                        config.PositionHalflife, DeltaTime, out var nextPosition, out var nextLinearVelocity);

                    SpringMath.CriticalSpringRotation(currentRotation, spring.AngularVelocity, goalRotation,
                        config.RotationHalflife, DeltaTime, out var nextRotation, out var nextAngularVelocity);

                    velocity.Linear = (nextPosition - currentPosition) / DeltaTime;
                    velocity.Angular = SpringMath.Log(math.mul(nextRotation, math.conjugate(currentRotation))) /
                                       DeltaTime;
                    velocities[i] = velocity;

                    spring.LinearVelocity = nextLinearVelocity;
                    spring.AngularVelocity = nextAngularVelocity;
                    states[i] = spring;

                    var arrived = math.distance(currentPosition, goalPosition) <= config.AttachDistance &&
                                  math.length(SpringMath.Log(math.mul(currentRotation,
                                      math.conjugate(goalRotation)))) <=
                                  config.AttachAngle;

                    if (arrived && hasSocket)
                    {
                        var socket = sockets[i];
                        socket.Bone = socketEntity;
                        socket.LocalPosition = config.LocalPosition;
                        socket.LocalRotation = config.LocalRotation;
                        sockets[i] = socket;
                        ECB.SetComponentEnabled<WeaponSocket>(unfilteredChunkIndex, body, true);
                        ECB.SetComponentEnabled<ActiveSocketReturn>(unfilteredChunkIndex, body, false);
                    }
                }
            }
        }
    }
}