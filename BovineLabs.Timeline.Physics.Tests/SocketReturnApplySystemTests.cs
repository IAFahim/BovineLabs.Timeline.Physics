using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Sockets;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class SocketReturnApplySystemTests : ECSTestsFixture
    {
        [Test]
        public void SocketReturn_EmptyTargetSlot_LeavesVelocityAndSpringUntouched()
        {
            World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var body = Manager.CreateEntity();
            var pose = LocalTransform.FromPosition(new float3(5f, 0f, 0f));
            Manager.AddComponentData(body, pose);
            Manager.AddComponentData(body, new LocalToWorld { Value = pose.ToMatrix() });
            var initial = new PhysicsVelocity
            {
                Linear = new float3(7f, 0f, 0f),
                Angular = new float3(0f, 3f, 0f)
            };
            Manager.AddComponentData(body, initial);
            Manager.AddComponentData(body, new SocketReturnState());
            Manager.AddComponentData(body, default(Targets));
            Manager.AddComponentData(body, new ActiveSocketReturn
            {
                Config = new SocketReturnData
                {
                    Socket = Target.Target,
                    LocalPosition = float3.zero,
                    LocalRotation = quaternion.identity,
                    PositionHalflife = 0.1f,
                    RotationHalflife = 0.1f,
                    AttachDistance = 0f,
                    AttachAngle = 0f
                }
            });

            World.SetTime(new TimeData(1.0, 1f / 60f));

            var sys = World.GetOrCreateSystem<SocketReturnApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            // The Target slot resolves to Entity.Null, so the recall spring must not fire the body toward
            // the world origin: velocity stays as-is and the spring state is not initialized this frame.
            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(initial.Linear, velocity.Linear);
            Assert.AreEqual(initial.Angular, velocity.Angular);
            Assert.IsFalse(Manager.GetComponentData<SocketReturnState>(body).Initialized);
        }
    }
}
