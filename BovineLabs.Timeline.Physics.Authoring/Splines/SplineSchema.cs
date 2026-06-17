using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PropertyDrawers;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    /// <summary>
    ///     A stable, auto-keyed handle to a path. A <c>SplinePathAuthoring</c> GameObject bakes its geometry under
    ///     this schema's key; a spline-follow clip references the same schema asset and bakes the same key — so the
    ///     two are linked through an asset→asset reference (serializes safely) and resolved at runtime by key,
    ///     never by a fragile asset→scene reference. Same IUID + AutoRef pattern as EntityLinkSchema.
    /// </summary>
    [AutoRef(nameof(SplineSettings), "splines", nameof(SplineSchema), "Schemas/Splines/")]
    [CreateAssetMenu(menuName = "BovineLabs/Splines/Schema")]
    public sealed class SplineSchema : ScriptableObject, IUID
    {
        [SerializeField] [InspectorReadOnly] private ushort id;

        public ushort Id => id;

        int IUID.ID
        {
            get => id;
            set
            {
                if (value is < 0 or > ushort.MaxValue)
                {
                    Debug.LogError("Ran out of Spline schema keys.");
                    return;
                }

                id = (ushort)value;
            }
        }

        public static implicit operator ushort(SplineSchema schema)
        {
            return schema == null ? (ushort)0 : schema.id;
        }
    }
}
