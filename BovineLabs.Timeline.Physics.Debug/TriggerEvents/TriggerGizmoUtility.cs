using System.Runtime.CompilerServices;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Quill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    public static class TriggerGizmoUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawActuallyFired(
            Entity triggerEntity,
            StatefulEventState configEventState,
            float3 position,
            ref Drawer drawer,
            BufferLookup<StatefulTriggerEvent> triggerEventsLookup,
            BufferLookup<StatefulCollisionEvent> collisionEventsLookup,
            Color drawColor,
            string label)
        {
            if (triggerEventsLookup.TryGetBuffer(triggerEntity, out var triggers))
                foreach (var evt in triggers)
                    if (StatefulEventMatching.Matches(evt.State, configEventState, true, false))
                    {
                        drawer.Sphere(position, 0.75f, 16, drawColor, 0.8f);
                        drawer.Text32(position + new float3(0f, 0.8f, 0f), label, drawColor, 14f, 0.8f);
                    }

            if (collisionEventsLookup.TryGetBuffer(triggerEntity, out var collisions))
                foreach (var evt in collisions)
                    if (StatefulEventMatching.Matches(evt.State, configEventState, true, false))
                    {
                        drawer.Sphere(position, 0.75f, 16, drawColor, 0.8f);
                        drawer.Text32(position + new float3(0f, 0.8f, 0f), label, drawColor, 14f, 0.8f);
                    }
        }
    }
}