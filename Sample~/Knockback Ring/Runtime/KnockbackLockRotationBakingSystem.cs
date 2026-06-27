namespace Vex.Knockback
{
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;

    /// <summary>
    /// Locks rotation on every <see cref="KnockbackReceiver"/> body by zeroing its inverse inertia at bake time, so a
    /// knockback (a pure linear impulse) slides the body cleanly without tumbling it over. Linear response is
    /// untouched — only angular response is removed. Runs after all bakers, so the physics baker's <c>PhysicsMass</c>
    /// is already present.
    /// </summary>
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct KnockbackLockRotationBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var mass in SystemAPI.Query<RefRW<PhysicsMass>>().WithAll<KnockbackReceiver>())
            {
                mass.ValueRW.InverseInertia = float3.zero;
            }
        }
    }
}
