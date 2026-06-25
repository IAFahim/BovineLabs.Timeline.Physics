using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    /// <summary>
    /// Computes the crossing-aware, fixed-step <see cref="PhysicsClipGate"/> for every physics trigger clip.
    /// Runs first in <see cref="PhysicsProducerGroup"/> (fixed step), ahead of every trigger consumer, so they
    /// read a fresh activation state and first/last-frame edge derived from the clip timer interval instead of the
    /// stale, variable-rate core ClipActive/ClipActivePrevious. See <see cref="PhysicsClipGate"/> for the why.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsClipGateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new GateJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithPresent(typeof(PhysicsClipGate))]
        private partial struct GateJob : IJobEntity
        {
            private void Execute(
                ref PhysicsClipGate gate,
                EnabledRefRW<PhysicsClipGate> gateEnabled,
                EnabledRefRO<ClipActive> coreActive,
                EnabledRefRO<TimelineActive> timelineActive,
                in TimerData timer,
                in TimeTransform timeTransform)
            {
                // OR with core ClipActive so post/extrapolation (which core keeps active past End) is preserved;
                // the crossing test is what rescues the missed activations and short windows core point-samples away.
                // The TimelineActive gate keeps a clip parked inside its window from firing while the timeline is off.
                var active = timelineActive.ValueRO &&
                             (StatefulEventMatching.IsClipActiveCrossing(in timer, in timeTransform) ||
                              coreActive.ValueRO);

                // FirstFrame edge from our own fixed-step WasActive: true exactly once per activation, and stable
                // across the multiple fixed ticks that share one (frozen) timeline frame at low FPS.
                gate.FirstFrame = (byte)(active && gate.WasActive == 0 ? 1 : 0);
                gate.LastFrame = (byte)(active && StatefulEventMatching.IsClipLastFrame(in timer, in timeTransform) ? 1 : 0);
                gate.WasActive = (byte)(active ? 1 : 0);
                gateEnabled.ValueRW = active;
            }
        }
    }
}
