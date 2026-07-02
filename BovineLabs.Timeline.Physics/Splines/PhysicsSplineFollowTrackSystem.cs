using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Splines
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    public partial struct PhysicsSplineFollowTrackSystem : ISystem
    {
        private TrackBlendStateDriver<PhysicsSplineFollowData, PhysicsSplineFollowAnimated, ActiveSplineFollow,
            PhysicsSplineFollowMixer, PhysicsSplineFollowState> _driver;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state, RearmPolicy.EveryActivation);
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
