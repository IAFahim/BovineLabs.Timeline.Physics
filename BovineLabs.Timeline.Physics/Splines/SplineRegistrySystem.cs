using BovineLabs.Core.Collections;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Splines
{
    [UpdateInGroup(typeof(PhysicsProducerGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.Editor)]
    public partial struct SplineRegistrySystem : ISystem
    {
        private NativeHashMap<ushort, BlobAssetReference<BlobSpline>> _map;

        public void OnCreate(ref SystemState state)
        {
            _map = new NativeHashMap<ushort, BlobAssetReference<BlobSpline>>(16, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new SplineRegistry { Map = _map });
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_map.IsCreated) _map.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _map.Clear();
            foreach (var (key, blob) in SystemAPI.Query<RefRO<SplineKey>, RefRO<SplineBlob>>())
                if (blob.ValueRO.Value.IsCreated)
                    _map[key.ValueRO.Value] = blob.ValueRO.Value;
        }
    }
}