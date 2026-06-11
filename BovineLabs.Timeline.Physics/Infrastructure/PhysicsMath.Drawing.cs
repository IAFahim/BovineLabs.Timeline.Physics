#if (UNITY_EDITOR || BL_DEBUG) && BL_QUILL
using BovineLabs.Quill;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    public static partial class PhysicsMath
    {
        public static void DrawLinearPidPrediction(ref Drawer drawer, float3 startPos, float3 targetPos,
            PidTuning tuning, float time)
        {
            var pos = startPos;
            var vel = float3.zero;
            var integral = float3.zero;
            var prevError = targetPos - startPos;

            const float dt = 0.02f;
            var steps = (int)(time / dt);
            var lastPos = startPos;

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            for (var i = 0; i < steps; i++)
            {
                var error = targetPos - pos;
                integral += error * dt;
                integral = math.clamp(integral, -integralMax, integralMax);

                var derivative = (error - prevError) / dt;
                var rawForce = tuning.Proportional * error + tuning.Integral * integral +
                               tuning.Derivative * derivative;

                var forceMagSq = math.lengthsq(rawForce);
                var force = forceMagSq > maxSq ? math.normalize(rawForce) * tuning.MaxOutput : rawForce;

                vel += force * dt;
                pos += vel * dt;
                prevError = error;

                drawer.Line(lastPos, pos, new Color(0f, 1f, 0f, 0.3f));
                lastPos = pos;

                if (math.lengthsq(error) < 1e-4f && forceMagSq < 1e-4f && math.lengthsq(vel) < 1e-4f)
                {
                    pos = targetPos;
                    break;
                }
            }

            drawer.Cuboid(pos, quaternion.identity, new float3(0.5f), Color.green);
            drawer.Text32(pos + new float3(0, 0.5f, 0), "Predicted", Color.green, 10f);
        }

        public static void DrawAngularPidPrediction(ref Drawer drawer, float3 drawPos, quaternion startRot,
            quaternion targetRot, PidTuning tuning, float time)
        {
            var rot = startRot;
            var vel = float3.zero;
            var integral = float3.zero;

            ComputeAngularError(rot, targetRot, out var prevError);

            const float dt = 0.02f;
            var steps = (int)(time / dt);

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            for (var i = 0; i < steps; i++)
            {
                ComputeAngularError(rot, targetRot, out var error);

                integral += error * dt;
                integral = math.clamp(integral, -integralMax, integralMax);

                var derivative = (error - prevError) / dt;
                var rawTorque = tuning.Proportional * error + tuning.Integral * integral +
                                tuning.Derivative * derivative;

                var torqueMagSq = math.lengthsq(rawTorque);
                var torque = torqueMagSq > maxSq ? math.normalize(rawTorque) * tuning.MaxOutput : rawTorque;

                vel += torque * dt;

                var localDelta = vel * dt;
                var mag = math.length(localDelta);
                if (mag > 1e-6f)
                {
                    var dq = quaternion.AxisAngle(localDelta / mag, mag);
                    rot = math.normalize(math.mul(rot, dq));
                }

                prevError = error;

                if (i % 10 == 0)
                    drawer.Arrow(drawPos, math.mul(rot, math.forward()) * 0.5f, new Color(0f, 1f, 0f, 0.1f));

                if (math.lengthsq(error) < 1e-4f && torqueMagSq < 1e-4f && math.lengthsq(vel) < 1e-4f)
                {
                    rot = targetRot;
                    break;
                }
            }

            drawer.Arrow(drawPos, math.mul(rot, math.forward()), Color.green);
            drawer.Arrow(drawPos, math.mul(rot, math.up()), new Color(0f, 0.5f, 0f));
            drawer.Text32(drawPos + new float3(0, -0.5f, 0), "Predicted", Color.green, 10f);
        }
    }
}
#endif
