using BovineLabs.Testing;
using BovineLabs.Timeline.Physics;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Essence.Data;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsKinematicsApplySystemTests : ECSTestsFixture
    {
        [Test]
        public void AddContinuous_AppendsPendingVelocityEveryFrame()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddBuffer<PendingVelocity>(target);
            
            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.AddContinuous,
                    Linear = new float3(0, 5, 0),
                    Angular = new float3(0, 0, 1),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingVelocity>(target);
            Assert.AreEqual(1, pendings.Length, "Should have appended exactly one PendingVelocity");
            
            // Pending velocity is multiplied by DeltaTime in AddContinuous
            var dt = WorldUnmanaged.Time.DeltaTime;
            Assert.AreEqual(5f * dt, pendings[0].Linear.y, 0.001f);
            Assert.AreEqual(1f * dt, pendings[0].Angular.z, 0.001f);
            
            var state = Manager.GetComponentData<PhysicsVelocityState>(target);
            Assert.IsFalse(state.Fired, "AddContinuous should not set Fired to true");
        }
        
        [Test]
        public void AddInstant_AppendsPendingForceOnce()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsForceState { Fired = false });
            Manager.AddBuffer<PendingForce>(target);
            
            Manager.AddComponentData(target, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Impulse,
                    Linear = new float3(10, 0, 0),
                    Angular = new float3(0, 2, 0),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(1, pendings.Length, "Should have appended exactly one PendingForce on first frame");
            Assert.AreEqual(10f, pendings[0].Linear.x, 0.001f);
            Assert.AreEqual(2f, pendings[0].Angular.y, 0.001f);
            
            var state = Manager.GetComponentData<PhysicsForceState>(target);
            Assert.IsTrue(state.Fired, "Impulse should set Fired to true");

            // clear buffer
            pendings.Clear();

            // frame 2
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            
            pendings = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(0, pendings.Length, "Should not append again because it was fired");
        }

        [Test]
        public void AddContinuous_AwayFromTarget_AppendsForceInCorrectDirection()
        {
            var body = Manager.CreateEntity();
            var targetEntity = Manager.CreateEntity();
            
            // Body is at (0,0,0)
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsForceState { Fired = false });
            Manager.AddBuffer<PendingForce>(body);
            
            // Target is at (5, 0, 0)
            Manager.AddComponentData(targetEntity, LocalTransform.FromPosition(new float3(5, 0, 0)));
            Manager.AddComponentData(targetEntity, new LocalToWorld { Value = float4x4.Translate(new float3(5, 0, 0)) });
            
            // Targets link body -> targetEntity
            Manager.AddComponentData(body, new Targets { Target = targetEntity });
            
            Manager.AddComponentData(body, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Continuous,
                    DirectionMode = PhysicsForceDirectionMode.AwayFromTarget,
                    DirectionTarget = Target.Target,
                    Magnitude = 10f, // Push away with magnitude 10
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pendings = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, pendings.Length);
            
            // Target is at +5 X. Body is at 0 X.
            // Diff = Target - Body = (5,0,0)
            // Dir = (1,0,0)
            // Away = -Dir = (-1,0,0)
            // Applied force should be (-10, 0, 0) * dt (0.1) = (-1, 0, 0)
            Assert.AreEqual(-1f, pendings[0].Linear.x, 0.001f);
            Assert.AreEqual(0f, pendings[0].Linear.y, 0.001f);
            Assert.AreEqual(0f, pendings[0].Linear.z, 0.001f);
        }
    }
}
