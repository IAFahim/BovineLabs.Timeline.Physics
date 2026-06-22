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
            string label,
            float radius = 0.75f,
            float textSize = 14f)
        {
            if (!HasMatchingEvent(triggerEntity, configEventState, triggerEventsLookup, collisionEventsLookup))
                return;

            drawer.Sphere(position, radius, 16, drawColor, 0.12f);
            drawer.Text32(position + new float3(0f, radius + 0.05f, 0f), label, drawColor, textSize, 0.12f);
        }

        private static bool HasMatchingEvent(
            Entity triggerEntity,
            StatefulEventState configEventState,
            BufferLookup<StatefulTriggerEvent> triggerEventsLookup,
            BufferLookup<StatefulCollisionEvent> collisionEventsLookup)
        {
            if (triggerEventsLookup.TryGetBuffer(triggerEntity, out var triggers))
                foreach (var evt in triggers)
                    if (StatefulEventMatching.Matches(evt.State, configEventState, false, false))
                        return true;

            if (collisionEventsLookup.TryGetBuffer(triggerEntity, out var collisions))
                foreach (var evt in collisions)
                    if (StatefulEventMatching.Matches(evt.State, configEventState, false, false))
                        return true;

            return false;
        }
    }
}