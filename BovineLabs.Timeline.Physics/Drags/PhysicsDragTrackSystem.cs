namespace BovineLabs.Timeline.Physics.Drags
{
    using BovineLabs.Timeline.Physics.Data;
    using EntityLinks;
    using Kernels;
    using Unity.Burst;
    using Unity.Entities;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsDragTrackSystem : ISystem
    {
        private TrackBlendDriver<PhysicsDragData, PhysicsDragAnimated, ActiveDrag, PhysicsDragMixer> _driver;
        [BurstCompile] public void OnCreate(ref SystemState state) => _driver.OnCreate(ref state);
        [BurstCompile] public void OnDestroy(ref SystemState state) => _driver.OnDestroy(ref state);

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