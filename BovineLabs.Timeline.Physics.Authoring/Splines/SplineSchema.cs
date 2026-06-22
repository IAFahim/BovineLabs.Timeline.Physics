using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PropertyDrawers;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
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