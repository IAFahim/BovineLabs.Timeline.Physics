using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Sockets
{
    public class WeaponRecallAuthoring : MonoBehaviour
    {
        public Transform restBone;
        public Vector3 restLocalPosition = Vector3.zero;
        public Vector3 restLocalRotationEuler = Vector3.zero;

        private class WeaponRecallBaker : Baker<WeaponRecallAuthoring>
        {
            public override void Bake(WeaponRecallAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Physics.Sockets.WeaponSocket
                {
                    Bone = authoring.restBone != null
                        ? GetEntity(authoring.restBone, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    LocalPosition = authoring.restLocalPosition,
                    LocalRotation = quaternion.Euler(math.radians(authoring.restLocalRotationEuler))
                });
                SetComponentEnabled<Physics.Sockets.WeaponSocket>(entity, false);

                AddComponent<Physics.Sockets.ActiveSocketReturn>(entity);
                SetComponentEnabled<Physics.Sockets.ActiveSocketReturn>(entity, false);
                AddComponent<Physics.Sockets.SocketReturnState>(entity);
            }
        }
    }
}
