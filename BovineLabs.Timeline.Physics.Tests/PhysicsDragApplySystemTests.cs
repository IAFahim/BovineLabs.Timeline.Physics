using BovineLabs.Testing;
using BovineLabs.Essence.Data;
using NUnit.Framework;
using BovineLabs.Reaction.Data.Core;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsDragApplySystemTests : ECSTestsFixture
    {
        [Test]
        public void ApplyDrag_ReducesVelocityEveryFrame()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = new float3(10, 0, 0), Angular = new float3(0, 5, 0) });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1,1,1) });
            
            Manager.AddComponentData(target, new ActiveDrag
            {
                Config = new PhysicsDragData
                {
                    Linear = 1f,
                    Angular = 1f,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsDragApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.Less(vel.Linear.x, 10f, "Linear velocity should be reduced by drag");
            Assert.Less(vel.Angular.y, 5f, "Angular velocity should be reduced by drag");
            Assert.Greater(vel.Linear.x, 0f, "Velocity should not flip direction");
            Assert.Greater(vel.Angular.y, 0f, "Velocity should not flip direction");
        }

        [Test]
        public void ApplyDrag_ZeroDeltaTime_DoesNotReduceVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = new float3(10, 0, 0) });
            Manager.AddComponentData(target, new ActiveDrag
            {
                Config = new PhysicsDragData { Linear = 1f, Angular = 1f, Strength = default }
            });

            World.SetTime(new TimeData(0.1, 0.0f));
            var sys = World.GetOrCreateSystem<PhysicsDragApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pv = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10.0f, pv.Linear.x, 0.001f, "Velocity should remain exactly 10 when DeltaTime is 0");
        }

        [Test]
        public void ApplyDrag_DisabledStat_DoesNotReduceVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = new float3(10, 0, 0) });
            
            var stats = Manager.AddBuffer<Stat>(target);
            StatKey statKey = 12345;
            stats.Initialize();
            stats.AsMap().Add(statKey, new StatValue { Added = 0, Multi = 0f });

            Manager.AddComponentData(target, new ActiveDrag
            {
                Config = new PhysicsDragData { 
                    Linear = 1f, 
                    Angular = 1f, 
                    Strength = new StatStrengthConfig { Stat = statKey, ReadFrom = Target.Self } 
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsDragApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pv = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10.0f, pv.Linear.x, 0.001f, "Velocity should remain 10 when stat multiplier is 0");
        }
    }
}
