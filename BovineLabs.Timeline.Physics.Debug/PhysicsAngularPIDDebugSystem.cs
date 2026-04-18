#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsAngularPIDDebugSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this._localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            this._velocityLookup = state.GetComponentLookup<PhysicsVelocity>(true);
            this._targetsLookup = state.GetComponentLookup<Targets>(true);
            this._targetsCustomsLookup = state.GetComponentLookup<TargetsCustom>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DrawSystem.Singleton>(out var drawSystem))
            {
                return;
            }

            var drawer = drawSystem.CreateDrawer();

            this._localTransformLookup.Update(ref state);
            this._velocityLookup.Update(ref state);
            this._targetsLookup.Update(ref state);
            this._targetsCustomsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                TransformLookup = this._localTransformLookup,
                VelocityLookup = this._velocityLookup,
                TargetsLookup = this._targetsLookup,
                TargetsCustomLookup = this._targetsCustomsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;

            private void Execute(in TrackBinding binding, in PhysicsAngularPIDAnimated animated, in LocalTime localTime)
            {
                var entity = binding.Value;
                if (!this.TransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!PhysicsMath.TryResolveAngularPidTarget(transform, animated.AuthoredData, entity, in this.TargetsLookup, in this.TargetsCustomLookup, in this.TransformLookup, out var finalRot)) return;
                
                var forward = math.mul(finalRot, math.forward());
                var up = math.mul(finalRot, math.up());
                this.Drawer.Arrow(transform.Position, forward, Color.blue);
                this.Drawer.Arrow(transform.Position, up, Color.green);
                
                PhysicsMath.TryDrawAngularPidPrediction(ref this.Drawer, transform.Position, transform.Rotation, finalRot, animated.AuthoredData.Tuning, (float)localTime.Value);

                if (this.VelocityLookup.TryGetComponent(entity, out var velocity))
                    this.Drawer.Arrow(transform.Position, velocity.Angular, Color.magenta);
            }
        }
    }
}
#endif