using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Data.Mixers;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.VelocityOverrides
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsVelocityTrackSystem : ISystem
    {
        private TrackBlendStateDriver<PhysicsVelocityData, PhysicsVelocityAnimated, ActiveVelocity,
            PhysicsVelocityMixer, PhysicsVelocityState> _driver;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state, RearmPolicy.EveryActivation,
                new PhysicsVelocityState { Fired = false });
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

            state.Dependency = new AdvanceElapsedTimeJob<PhysicsVelocityData, PhysicsVelocityState>
            {
                BlendData = blendData,
                StateLookup = _driver.StateLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }
    }
}
