using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Ricochets
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsRicochetTrackSystem : ISystem
    {
        private TrackBlendStateDriver<PhysicsRicochetData, PhysicsRicochetAnimated, ActiveRicochet,
            DiscreteMixer<PhysicsRicochetData>, PhysicsRicochetState> _driver;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state, RearmPolicy.EveryActivation,
                new PhysicsRicochetState { Fired = false });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _driver.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();
            _driver.OnUpdate(ref state, ecb);
        }
    }
}
