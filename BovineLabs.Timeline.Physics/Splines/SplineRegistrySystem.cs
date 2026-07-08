using BovineLabs.Core.Collections;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Splines
{
    [UpdateInGroup(typeof(PhysicsProducerGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct SplineRegistrySystem : ISystem
    {
        private NativeHashMap<ushort, BlobAssetReference<BlobSpline>> _map;
        private EntityQuery _query;
        private int _lastOrderVersion;
        private bool _built;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _map = new NativeHashMap<ushort, BlobAssetReference<BlobSpline>>(16, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new SplineRegistry { Map = _map });
            _query = SystemAPI.QueryBuilder().WithAll<SplineKey, SplineBlob>().Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_map.IsCreated) _map.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var version = _query.GetCombinedComponentOrderVersion(false);
            if (_built && version == _lastOrderVersion)
                return;

            _built = true;
            _lastOrderVersion = version;

            _map.Clear();
            foreach (var (key, blob) in SystemAPI.Query<RefRO<SplineKey>, RefRO<SplineBlob>>())
                if (blob.ValueRO.Value.IsCreated)
                    _map[key.ValueRO.Value] = blob.ValueRO.Value;
        }
    }
}
