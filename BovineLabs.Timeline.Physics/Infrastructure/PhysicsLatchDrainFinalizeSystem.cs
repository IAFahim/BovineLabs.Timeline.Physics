using BovineLabs.Timeline.Physics.Data;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    /// <summary>
    /// Fixed-step tail of the drain gate for the fire-once / continuous-motion latch families (Force, Velocity, PID,
    /// Teleport). Runs last in the fixed step — after every producer and modifier apply has had its tick — and disables
    /// any latch that the render-side <see cref="DisableAbsentDrainableTrackJob{TData,TActive,TState}"/> lingered
    /// (<see cref="Data.Kernels.IOrphanedLatch.Orphaned"/>). Because this runs after the applies, every such latch has
    /// already been serviced this tick (impulse fired, continuous tail drained, or the missed control step applied), so
    /// the disable is safe — and because this is the ONLY thing that disables an orphaned latch, no
    /// <c>Active*</c>-driven fixed-step effect can be dropped across the render→fixed enable seam.
    /// <para>
    /// Placed in <see cref="PhysicsModifierGroup"/> / OrderLast so it trails the velocity-override Set path (modifier)
    /// and the teleport apply (modifier) as well as the producer-group force/velocity/PID applies.
    /// </para>
    /// </summary>
    [UpdateInGroup(typeof(PhysicsModifierGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsLatchDrainFinalizeSystem : ISystem
    {
        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;
        private EntityQuery _teleportQuery;
        private EntityQuery _linearPidQuery;
        private EntityQuery _angularPidQuery;

        private ComponentTypeHandle<ActiveForce> _forceActive;
        private ComponentTypeHandle<PhysicsForceState> _forceState;
        private ComponentTypeHandle<ActiveVelocity> _velocityActive;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityState;
        private ComponentTypeHandle<ActiveTeleport> _teleportActive;
        private ComponentTypeHandle<PhysicsTeleportState> _teleportState;
        private ComponentTypeHandle<ActiveLinearPid> _linearActive;
        private ComponentTypeHandle<PhysicsLinearPIDState> _linearState;
        private ComponentTypeHandle<ActiveAngularPid> _angularActive;
        private ComponentTypeHandle<PhysicsAngularPIDState> _angularState;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _forceQuery = SystemAPI.QueryBuilder().WithAllRW<ActiveForce, PhysicsForceState>().Build();
            _velocityQuery = SystemAPI.QueryBuilder().WithAllRW<ActiveVelocity, PhysicsVelocityState>().Build();
            _teleportQuery = SystemAPI.QueryBuilder().WithAllRW<ActiveTeleport, PhysicsTeleportState>().Build();
            _linearPidQuery = SystemAPI.QueryBuilder().WithAllRW<ActiveLinearPid, PhysicsLinearPIDState>().Build();
            _angularPidQuery = SystemAPI.QueryBuilder().WithAllRW<ActiveAngularPid, PhysicsAngularPIDState>().Build();

            _forceActive = state.GetComponentTypeHandle<ActiveForce>();
            _forceState = state.GetComponentTypeHandle<PhysicsForceState>();
            _velocityActive = state.GetComponentTypeHandle<ActiveVelocity>();
            _velocityState = state.GetComponentTypeHandle<PhysicsVelocityState>();
            _teleportActive = state.GetComponentTypeHandle<ActiveTeleport>();
            _teleportState = state.GetComponentTypeHandle<PhysicsTeleportState>();
            _linearActive = state.GetComponentTypeHandle<ActiveLinearPid>();
            _linearState = state.GetComponentTypeHandle<PhysicsLinearPIDState>();
            _angularActive = state.GetComponentTypeHandle<ActiveAngularPid>();
            _angularState = state.GetComponentTypeHandle<PhysicsAngularPIDState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _forceActive.Update(ref state);
            _forceState.Update(ref state);
            _velocityActive.Update(ref state);
            _velocityState.Update(ref state);
            _teleportActive.Update(ref state);
            _teleportState.Update(ref state);
            _linearActive.Update(ref state);
            _linearState.Update(ref state);
            _angularActive.Update(ref state);
            _angularState.Update(ref state);

            state.Dependency = new LatchDrainFinalizeJob<ActiveForce, PhysicsForceState>
            {
                ActiveHandle = _forceActive,
                StateHandle = _forceState,
            }.ScheduleParallel(_forceQuery, state.Dependency);

            state.Dependency = new LatchDrainFinalizeJob<ActiveVelocity, PhysicsVelocityState>
            {
                ActiveHandle = _velocityActive,
                StateHandle = _velocityState,
            }.ScheduleParallel(_velocityQuery, state.Dependency);

            state.Dependency = new LatchDrainFinalizeJob<ActiveTeleport, PhysicsTeleportState>
            {
                ActiveHandle = _teleportActive,
                StateHandle = _teleportState,
            }.ScheduleParallel(_teleportQuery, state.Dependency);

            state.Dependency = new LatchDrainFinalizeJob<ActiveLinearPid, PhysicsLinearPIDState>
            {
                ActiveHandle = _linearActive,
                StateHandle = _linearState,
            }.ScheduleParallel(_linearPidQuery, state.Dependency);

            state.Dependency = new LatchDrainFinalizeJob<ActiveAngularPid, PhysicsAngularPIDState>
            {
                ActiveHandle = _angularActive,
                StateHandle = _angularState,
            }.ScheduleParallel(_angularPidQuery, state.Dependency);
        }
    }
}
