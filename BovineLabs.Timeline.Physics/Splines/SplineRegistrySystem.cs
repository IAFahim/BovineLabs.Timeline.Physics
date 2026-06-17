using BovineLabs.Core.Collections;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Splines
{
    /// <summary>
    ///     Collects every baked spline (<see cref="SplineKey" /> + <see cref="SplineBlob" />) into the
    ///     <see cref="SplineRegistry" /> singleton so any system can evaluate a path by key with no scene reference.
    ///     Rebuilt each update on the main thread before consumers run.
    /// </summary>
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
            if (_map.IsCreated)
            {
                _map.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // ponytail: O(splines) full rebuild each frame — splines are a handful of static scene objects.
            // If a project ever streams thousands, gate this on a SplineKey order-version change instead.
            _map.Clear();
            foreach (var (key, blob) in SystemAPI.Query<RefRO<SplineKey>, RefRO<SplineBlob>>())
            {
                if (blob.ValueRO.Value.IsCreated)
                {
                    _map[key.ValueRO.Value] = blob.ValueRO.Value;
                }
            }
        }
    }
}
