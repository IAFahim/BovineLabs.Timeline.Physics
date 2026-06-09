using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Sockets
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponSocketAttachSystem : ISystem
    {
        private ComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Parent> _parentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            _parentLookup.Update(ref state);

            new AttachJob
            {
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct AttachJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;

            public void Execute(Entity entity, in WeaponSocket socket, ref LocalTransform transform,
                EnabledRefRO<WeaponSocket> enabled)
            {
                if (!enabled.ValueRO) return;
                if (!LocalToWorldLookup.TryGetComponent(socket.Bone, out var boneL2W)) return;

                var boneRotation = new quaternion(math.orthonormalize(new float3x3(boneL2W.Value)));
                var worldPosition = math.transform(boneL2W.Value, socket.LocalPosition);
                var worldRotation = math.mul(boneRotation, socket.LocalRotation);

                if (ParentLookup.TryGetComponent(entity, out var parent) &&
                    LocalToWorldLookup.TryGetComponent(parent.Value, out var parentL2W))
                {
                    var inverseParent = math.inverse(parentL2W.Value);
                    var parentRotation = new quaternion(math.orthonormalize(new float3x3(parentL2W.Value)));
                    transform.Position = math.transform(inverseParent, worldPosition);
                    transform.Rotation = math.mul(math.inverse(parentRotation), worldRotation);
                }
                else
                {
                    transform.Position = worldPosition;
                    transform.Rotation = worldRotation;
                }
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponSocketFreezeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FreezeJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(WeaponSocket))]
        private partial struct FreezeJob : IJobEntity
        {
            public void Execute(ref PhysicsVelocity velocity, EnabledRefRO<WeaponSocket> enabled)
            {
                if (!enabled.ValueRO) return;
                velocity.Linear = float3.zero;
                velocity.Angular = float3.zero;
            }
        }
    }
}