namespace Vex.Knockback
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Authoring for <see cref="KnockbackReceiver"/>. Put this on the body that carries the eight trigger sphere
    /// zones (alongside <c>PhysicsBodyAuthoring</c> and a <c>StatefulTriggerEventAuthoring</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public class KnockbackReceiverAuthoring : MonoBehaviour
    {
        [Tooltip("Horizontal impulse (N·s) applied away from whatever touches a sphere zone. With mass 1 this is m/s.")]
        public float Strength = 5f;

        [Tooltip("Upward impulse (N·s) added on contact so the knockback reads as a backward leap. 0 = flat shove.")]
        public float Lift = 4f;

        private class KnockbackBaker : Baker<KnockbackReceiverAuthoring>
        {
            public override void Bake(KnockbackReceiverAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new KnockbackReceiver
                {
                    Strength = authoring.Strength,
                    Lift = authoring.Lift,
                });
            }
        }
    }
}
