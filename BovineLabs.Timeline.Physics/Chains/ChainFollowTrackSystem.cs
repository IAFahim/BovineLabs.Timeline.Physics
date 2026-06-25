using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Chains
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainFollowTrackSystem : ISystem
    {
        private TrackBlendDriver<ChainFollowData, ChainFollowAnimated, ActiveChainFollow, ChainFollowMixer> _driver;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state);

            // ponytail: skip when idle — see PhysicsVelocityTrackSystem; the generic
            // PrepareAnimatedJob schedule SIGSEGVs in an IL2CPP player.
            state.RequireForUpdate<ChainFollowAnimated>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _driver.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            _driver.OnUpdate(ref state, ecb);
        }
    }
}