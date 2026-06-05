namespace BovineLabs.Timeline.Physics.VelocityOverrides
{

    using BovineLabs.Timeline.Data;
    using EntityLinks;
    using Data;
    using Data.Mixers;
    using Infrastructure;
    using Kernels;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsVelocityTrackSystem : ISystem
    {
        private TrackBlendDriver<PhysicsVelocityData, PhysicsVelocityAnimated, ActiveVelocity, PhysicsVelocityMixer> _driver;
        private ComponentLookup<PhysicsVelocityState> _stateLookup;
        private EntityQuery _resetQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state);
            _stateLookup = state.GetComponentLookup<PhysicsVelocityState>(false);

            using var reset = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, PhysicsVelocityAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>();
            _resetQuery = state.GetEntityQuery(reset);
        }

        [BurstCompile] public void OnDestroy(ref SystemState state) => _driver.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            _driver.UpdateLookups(ref state);
            _stateLookup.Update(ref state);

            state.Dependency = new ResetStateTrackJob<PhysicsVelocityState, ActiveVelocity>
            {
                TrackBindingTypeHandle = _driver.BindingHandle,
                StateLookup = _stateLookup,
                ActiveLookup = _driver.ActiveLookup,
                ResetValue = new PhysicsVelocityState { Fired = false }
            }.ScheduleParallel(_resetQuery, state.Dependency);

            _driver.OnUpdate(ref state, ecb);
        }
    }
}