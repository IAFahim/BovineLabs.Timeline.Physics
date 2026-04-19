// <copyright file="SmearVelocityBuilder.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using BovineLabs.Core.EntityCommands;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Smear
{
    public struct SmearVelocityBuilder
    {
        public float4 InitialValue;

        public SmearVelocityBuilder WithInitialValue(float4 value)
        {
            InitialValue = value;
            return this;
        }

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new SmearVelocity { Value = InitialValue });
        }
    }
}