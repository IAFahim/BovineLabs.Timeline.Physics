using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Forces
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    public partial struct PhysicsForceTrackSystem : ISystem
    {
        private TrackBlendStateDriver<PhysicsForceData, PhysicsForceAnimated, ActiveForce,
            PhysicsForceMixer, PhysicsForceState> _driver;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state, RearmPolicy.EveryActivation,
                new PhysicsForceState { Fired = false });
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
            var blendData = _driver.OnUpdate(ref state, ecb);

            state.Dependency = new AdvanceElapsedTimeJob<PhysicsForceData, PhysicsForceState>
            {
                BlendData = blendData,
                StateLookup = _driver.StateLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }
    }
}
