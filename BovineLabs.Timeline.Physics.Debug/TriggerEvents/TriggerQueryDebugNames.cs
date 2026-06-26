#if UNITY_EDITOR || BL_DEBUG
using Unity.Collections;

namespace BovineLabs.Timeline.Physics.Debug
{
    /// <summary>
    /// Burst-safe enum -> short FixedString names for the trigger-query config panel. enum.ToString() is managed
    /// and unusable in a Burst job, so every mode is spelled out here. Short labels keep the panel readable and
    /// well inside FixedString512. Mirrors <see cref="PhysicsTriggerQueryData"/>'s enums one-to-one.
    /// </summary>
    internal static class TriggerQueryDebugNames
    {
        public static FixedString32Bytes Selection(PhysicsTriggerQuerySelection s)
        {
            switch (s)
            {
                case PhysicsTriggerQuerySelection.Nearest: return "Nearest";
                case PhysicsTriggerQuerySelection.Farthest: return "Farthest";
                case PhysicsTriggerQuerySelection.MostAligned: return "MostAligned";
                case PhysicsTriggerQuerySelection.LeastAligned: return "LeastAligned";
                case PhysicsTriggerQuerySelection.HighestThreat: return "HighestThreat";
                case PhysicsTriggerQuerySelection.WeakestTarget: return "WeakestTarget";
                case PhysicsTriggerQuerySelection.CategoryPriority: return "CategoryPriority";
                case PhysicsTriggerQuerySelection.ClosingSpeedSelect: return "ClosingSpeed";
                case PhysicsTriggerQuerySelection.MostExposed: return "MostExposed";
                case PhysicsTriggerQuerySelection.DwellSelect: return "Dwell";
                case PhysicsTriggerQuerySelection.AllSurvivorsFanout: return "AllSurvivors";
                case PhysicsTriggerQuerySelection.TopK: return "TopK";
                case PhysicsTriggerQuerySelection.HeaviestMover: return "HeaviestMover";
                case PhysicsTriggerQuerySelection.MostBlocking: return "MostBlocking";
                case PhysicsTriggerQuerySelection.TauntOverride: return "Taunt";
                case PhysicsTriggerQuerySelection.TabCycle: return "TabCycle";
                default: return "?";
            }
        }

        public static FixedString32Bytes Value(PhysicsTriggerQueryValueMode v)
        {
            switch (v)
            {
                case PhysicsTriggerQueryValueMode.Constant: return "Constant";
                case PhysicsTriggerQueryValueMode.DirectionSector: return "DirSector";
                case PhysicsTriggerQueryValueMode.DistanceBand: return "DistBand";
                case PhysicsTriggerQueryValueMode.SectorBandPacked: return "SectorBand";
                case PhysicsTriggerQueryValueMode.OverlapCount: return "OverlapCount";
                case PhysicsTriggerQueryValueMode.FacingSide: return "FacingSide";
                case PhysicsTriggerQueryValueMode.CategoryOrdinal: return "CategoryOrd";
                case PhysicsTriggerQueryValueMode.ApproachVelocityBand: return "ApproachBand";
                case PhysicsTriggerQueryValueMode.ScaledMagnitude: return "ScaledMag";
                case PhysicsTriggerQueryValueMode.ContactNormalSector: return "ContactNormal";
                case PhysicsTriggerQueryValueMode.ImpactBand: return "ImpactBand";
                case PhysicsTriggerQueryValueMode.TimingWindowGrade: return "TimingGrade";
                case PhysicsTriggerQueryValueMode.OcclusionState: return "Occlusion";
                case PhysicsTriggerQueryValueMode.DeflectionBounce: return "Deflection";
                case PhysicsTriggerQueryValueMode.AggregateCentroid: return "Centroid";
                default: return "?";
            }
        }

        public static FixedString32Bytes RouteSlot(PhysicsTriggerRouteSlot s)
        {
            switch (s)
            {
                case PhysicsTriggerRouteSlot.Custom: return "Custom";
                case PhysicsTriggerRouteSlot.Owner: return "Owner";
                case PhysicsTriggerRouteSlot.Source: return "Source";
                case PhysicsTriggerRouteSlot.Target: return "Target";
                default: return "?";
            }
        }

        public static FixedString32Bytes RouteMode(PhysicsTriggerRouteMode m)
        {
            switch (m)
            {
                case PhysicsTriggerRouteMode.Fixed: return "Fixed";
                case PhysicsTriggerRouteMode.ByValue: return "ByValue";
                case PhysicsTriggerRouteMode.LinkedRole: return "LinkedRole";
                default: return "?";
            }
        }

        public static FixedString32Bytes WriteMode(PhysicsTriggerWriteMode m)
        {
            switch (m)
            {
                case PhysicsTriggerWriteMode.Set: return "Set";
                case PhysicsTriggerWriteMode.ClearOnly: return "ClearOnly";
                case PhysicsTriggerWriteMode.SetIfEmpty: return "SetIfEmpty";
                default: return "?";
            }
        }

        /// <summary> Append the short token of every enabled gate flag, space-separated. "none" if no gates set. </summary>
        public static void AppendGates(ref FixedString512Bytes sb, PhysicsTriggerGateFlags g)
        {
            if (g == PhysicsTriggerGateFlags.None)
            {
                sb.Append((FixedString32Bytes)"none");
                return;
            }

            var first = true;
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.ApproachGate, "Approach");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.FacingGate, "Facing");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.VerticalGate, "Vertical");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.FrameWindowGate, "FrameWin");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.MassBracket, "Mass");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.RequireOccluded, "Occluded");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.FactionGate, "Faction");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.DraftCorridorGate, "Draft");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.LedgeGate, "Ledge");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.PassLaneCone, "PassLane");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.ZoneStateGate, "Zone");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.SurfaceMaterialGate, "Surface");
            Tok(ref sb, ref first, g, PhysicsTriggerGateFlags.LightExposureGate, "Light");
        }

        private static void Tok(ref FixedString512Bytes sb, ref bool first, PhysicsTriggerGateFlags g,
            PhysicsTriggerGateFlags flag, in FixedString32Bytes name)
        {
            if ((g & flag) == 0)
            {
                return;
            }

            if (!first)
            {
                sb.Append(' ');
            }

            sb.Append(name);
            first = false;
        }
    }
}
#endif
