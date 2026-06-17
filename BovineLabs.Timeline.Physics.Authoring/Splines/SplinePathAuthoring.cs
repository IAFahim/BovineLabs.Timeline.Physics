using BovineLabs.Core.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    /// <summary>
    ///     Bakes a Unity <see cref="SplineContainer" /> on this GameObject into a world-space ECS spline blob, tagged
    ///     with a <see cref="SplineSchema" />'s stable key. Designers author the path with Unity's native spline tools
    ///     (handles, tangents, knots) in the Scene view; this turns it into data any system can evaluate by key.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [AddComponentMenu("BovineLabs/Splines/Spline Path")]
    public sealed class SplinePathAuthoring : MonoBehaviour
    {
        [Tooltip("The schema this path registers under. Spline-follow clips reference the same schema to find it.")]
        public SplineSchema schema;

        private sealed class Baker : Baker<SplinePathAuthoring>
        {
            public override void Bake(SplinePathAuthoring authoring)
            {
                var container = GetComponent<SplineContainer>();
                if (authoring.schema == null || container == null || container.Splines.Count == 0)
                {
                    return;
                }

                DependsOn(authoring.schema);

                // World-space bake: the body follows the path where it sits in the scene.
                var transform = (float4x4)authoring.transform.localToWorldMatrix;
                var blob = BlobSpline.Create(container.Splines[0], transform);
                AddBlobAsset(ref blob, out _);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SplineBlob { Value = blob });
                AddComponent(entity, new SplineKey { Value = authoring.schema.Id });
            }
        }
    }
}
