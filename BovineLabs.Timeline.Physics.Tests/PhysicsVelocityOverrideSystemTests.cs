namespace BovineLabs.Timeline.Physics.Tests
{
    using Unity.Entities;
    using BovineLabs.Core.EntityCommands;
    using BovineLabs.Essence.Data;
    using BovineLabs.Essence.Data.Builders;
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Testing;
    using BovineLabs.Timeline.Physics.Data;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Core;
    using Unity.Mathematics;
    using Unity.Physics;
    using Unity.Transforms;

    public class PhysicsVelocityOverrideSystemTests : ECSTestsFixture
    {
        [Test]
        public void SetContinuous_OverridesVelocityEveryFrame()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            
            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(0, 5, 0),
                    Angular = new float3(0, 0, 1),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = WorldExtensions.GetOrCreateSystem<VelocityOverrides.PhysicsVelocityOverrideSystem>(World);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel.Linear.y, 0.001f);
            Assert.AreEqual(1f, vel.Angular.z, 0.001f);
            
            var state = Manager.GetComponentData<PhysicsVelocityState>(target);
            Assert.IsFalse(state.Fired, "SetContinuous should not set Fired to true");
        }

        [Test]
        public void SetInstant_OverridesVelocityOnce()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            
            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetInstant,
                    Linear = new float3(10, 0, 0),
                    Angular = new float3(0, 2, 0),
                    Space = Target.None,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = WorldExtensions.GetOrCreateSystem<VelocityOverrides.PhysicsVelocityOverrideSystem>(World);

            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(10f, vel.Linear.x, 0.001f);
            Assert.AreEqual(2f, vel.Angular.y, 0.001f);
            
            var state = Manager.GetComponentData<PhysicsVelocityState>(target);
            Assert.IsTrue(state.Fired, "SetInstant should set Fired to true");

            Manager.SetComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });

            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel2 = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(0f, vel2.Linear.x, 0.001f, "Velocity should remain 0 because it was already fired");
        }

        [Test]
        public void SetContinuous_SpaceSelf_RotatesVelocityToLocalSpace()
        {
            var target = Manager.CreateEntity();
            var rotation = quaternion.Euler(0, math.PI / 2f, 0);
            var ltw = new float4x4(rotation, float3.zero);
            
            Manager.AddComponentData(target, LocalTransform.FromRotation(rotation));
            Manager.AddComponentData(target, new LocalToWorld { Value = ltw });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            
            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(10, 0, 0),
                    Angular = float3.zero,
                    Space = Target.Self,
                    Strength = default
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = WorldExtensions.GetOrCreateSystem<VelocityOverrides.PhysicsVelocityOverrideSystem>(World);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(0f, vel.Linear.x, 0.001f);
            Assert.AreEqual(0f, vel.Linear.y, 0.001f);
            Assert.AreEqual(-10f, vel.Linear.z, 0.001f);
        }
        
        [Test]
        public void SetContinuous_WithStatStrength_ScalesVelocityByStatMultiplier()
        {
            var target = Manager.CreateEntity();
            var commands = new EntityManagerCommands(Manager, target, BlobAssetStore);

            var statModifier = new StatModifier
            {
                Type = 12345,
                ModifyType = StatModifyType.Added,
            };
            statModifier.Value = 250;

            using var statsBuilder = new StatsBuilder(Allocator.Temp);
            statsBuilder.WithDefault(statModifier);
            statsBuilder.WithCanBeModified(false);
            statsBuilder.WithWriteEvents(false);
            statsBuilder.ApplyTo(ref commands);

            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsVelocityState { Fired = false });
            Manager.AddComponentData(target, default(Targets));
            
            Manager.AddComponentData(target, new ActiveVelocity
            {
                Config = new PhysicsVelocityData
                {
                    Mode = PhysicsVelocityMode.SetContinuous,
                    Linear = new float3(10, 0, 0),
                    Angular = float3.zero,
                    Space = Target.None,
                    Strength = new StatStrengthConfig { Stat = 12345, ReadFrom = Target.Self }
                }
            });

            World.SetTime(new TimeData(0.1, 0.1f));
            var sys = WorldExtensions.GetOrCreateSystem<VelocityOverrides.PhysicsVelocityOverrideSystem>(World);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(25f, vel.Linear.x, 0.001f);
        }
    }
}
