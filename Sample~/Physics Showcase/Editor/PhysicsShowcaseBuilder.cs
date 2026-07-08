using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Space = BovineLabs.Reaction.Data.Core.Target;
using TargetSlot = BovineLabs.Reaction.Data.Core.Target;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TriggerAuthoring = BovineLabs.Nerve.Authoring.PhysicsStates.StatefulTriggerEventAuthoring;
using ForceTrack = BovineLabs.Timeline.Physics.Authoring.PhysicsForceTrack;
using ForceClip = BovineLabs.Timeline.Physics.Authoring.PhysicsForceClip;
using ForceMode = BovineLabs.Timeline.Physics.PhysicsForceMode;
using ForceDir = BovineLabs.Timeline.Physics.PhysicsForceDirectionMode;
using VelocityTrack = BovineLabs.Timeline.Physics.Authoring.PhysicsVelocityTrack;
using VelocityClip = BovineLabs.Timeline.Physics.Authoring.PhysicsVelocityClip;
using VelocityMode = BovineLabs.Timeline.Physics.Data.PhysicsVelocityMode;
using DragTrack = BovineLabs.Timeline.Physics.Authoring.PhysicsDragTrack;
using DragClip = BovineLabs.Timeline.Physics.Authoring.PhysicsDragClip;
using GravityTrack = BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideTrack;
using GravityClip = BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideClip;
using ClampTrack = BovineLabs.Timeline.Physics.Authoring.VelocityClamps.PhysicsVelocityClampTrack;
using ClampClip = BovineLabs.Timeline.Physics.Authoring.VelocityClamps.PhysicsVelocityClampClip;
using TeleportTrack = BovineLabs.Timeline.Physics.Authoring.Teleports.PhysicsTeleportTrack;
using TeleportClip = BovineLabs.Timeline.Physics.Authoring.Teleports.PhysicsTeleportClip;
using RicochetTrack = BovineLabs.Timeline.Physics.Authoring.Ricochets.PhysicsRicochetTrack;
using RicochetClip = BovineLabs.Timeline.Physics.Authoring.Ricochets.PhysicsRicochetClip;
using LinearPIDTrack = BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsLinearPIDTrack;
using LinearPIDClip = BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsLinearPIDClip;
using LinearMode = BovineLabs.Timeline.Physics.PidLinearTargetMode;
using AngularPIDTrack = BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDTrack;
using AngularPIDClip = BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDClip;
using AngularMode = BovineLabs.Timeline.Physics.PidAngularTargetMode;
using KinematicTrack = BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideTrack;
using KinematicClip = BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideClip;
using FilterTrack = BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideTrack;
using FilterClip = BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideClip;
using TriggerTrack = BovineLabs.Timeline.Physics.Authoring.StatefulTriggerTrack;
using TriggerForceClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerForceClip;
using TriggerConditionClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerConditionClip;
using TriggerQueryClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerQueryClip;
using TriggerForceType = BovineLabs.Timeline.Physics.PhysicsTriggerForceType;
using TriggerSelection = BovineLabs.Timeline.Physics.PhysicsTriggerQuerySelection;
using EventState = BovineLabs.Nerve.PhysicsStates.StatefulEventState;

public static class PhysicsShowcaseBuilder
{
    private const string SampleFolder = "Assets/Samples/PhysicsShowcase";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/PhysicsShowcase.unity";
    private const string SubPath = SampleFolder + "/PhysicsShowcase_Sub.unity";

    private static readonly Color ForceColor = new Color(0.95f, 0.45f, 0.25f);
    private static readonly Color VelColor = new Color(0.25f, 0.80f, 0.90f);
    private static readonly Color DragColor = new Color(0.70f, 0.45f, 0.90f);
    private static readonly Color GravColor = new Color(0.45f, 0.85f, 0.45f);
    private static readonly Color BounceColor = new Color(0.95f, 0.85f, 0.25f);
    private static readonly Color ClampColor = new Color(0.40f, 0.55f, 0.95f);
    private static readonly Color TeleportColor = new Color(0.20f, 0.95f, 0.70f);
    private static readonly Color RicochetColor = new Color(1.00f, 0.40f, 0.10f);
    private static readonly Color LinPidColor = new Color(0.95f, 0.30f, 0.50f);
    private static readonly Color AngPidColor = new Color(0.95f, 0.55f, 0.55f);
    private static readonly Color KinColor = new Color(0.60f, 0.60f, 0.65f);
    private static readonly Color FilterColor = new Color(0.90f, 0.25f, 0.25f);
    private static readonly Color TriggerColor = new Color(0.90f, 0.85f, 0.15f);
    private static readonly Color TargetColor = new Color(0.95f, 0.20f, 0.20f);
    private static readonly Color ControlColor = new Color(0.55f, 0.57f, 0.62f);
    private static readonly Color BarrierColor = new Color(0.85f, 0.30f, 0.85f);
    private static readonly Color PadColor = new Color(0.22f, 0.24f, 0.29f);
    private static readonly Color BannerColor = new Color(0.06f, 0.08f, 0.12f);

    private const float ColStep = 9f;
    private const float RowStep = 5f;
    private const float PadY = 0.05f;
    private const float BallY = 0.75f;

    private const float ForceX = -54f;
    private const float VelX = -45f;
    private const float DragX = -36f;
    private const float GravX = -27f;
    private const float BounceX = -18f;
    private const float ClampX = -9f;
    private const float TeleportX = 0f;
    private const float RicochetX = 9f;
    private const float LinPidX = 18f;
    private const float AngPidX = 27f;
    private const float KinX = 36f;
    private const float FilterX = 45f;
    private const float TriggerX = 54f;

    private const uint CatBody = 1u << 1;
    private const uint CatBarrier = 1u << 2;
    private const uint CatTrigger = 1u << 3;
    private const uint CatGround = 1u << 0;
    private const uint CatAll = ~0u;

    private static readonly Vector3 CameraPos = new Vector3(0f, 22f, -50f);

    private static Scene activeSub;

    private enum BindKind { Body, Go, Targets, Trigger }

    private sealed class TrackBind
    {
        public string TrackName;
        public string BindName;
        public BindKind Kind;
    }

    private sealed class CellWire
    {
        public string DirectorName;
        public string TimelinePath;
        public string BindName;
        public BindKind DefaultKind = BindKind.Body;
        public List<TrackBind> Binds;
    }

    private static readonly List<CellWire> Wires = new List<CellWire>();

    private sealed class CaptionData
    {
        public string Title;
        public string Usage;
        public Vector3 CellPos;
        public Color Color;
    }

    private static readonly List<CaptionData> Captions = new List<CaptionData>();

    [MenuItem("Showcase/Build Physics")]
    public static void Build()
    {
        Wires.Clear();
        Captions.Clear();
        EnsureFolders();
        ResetAssets();

        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        BuildPads();
        BuildForceColumn();
        BuildVelocityColumn();
        BuildDragColumn();
        BuildGravityColumn();
        BuildBounceColumn();
        BuildClampColumn();
        BuildTeleportColumn();
        BuildRicochetColumn();
        BuildLinearPidColumn();
        BuildAngularPidColumn();
        BuildKinematicColumn();
        BuildFilterColumn();
        BuildTriggerColumn();

        EditorSceneManager.SaveScene(sub, SubPath);
        EditorSceneManager.SetActiveScene(parent);
        EditorSceneManager.CloseScene(sub, true);

        sub = EditorSceneManager.OpenScene(SubPath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);

        foreach (var w in Wires)
        {
            WireCell(w);
        }

        EditorSceneManager.MarkSceneDirty(sub);
        EditorSceneManager.SaveScene(sub);

        EditorSceneManager.SetActiveScene(parent);
        BuildParent();
        EditorSceneManager.SaveScene(parent);

        EditorSceneManager.CloseScene(sub, true);
        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("PhysicsShowcase: built exhaustive 13-column grid at " + ParentPath);
    }

    // ---------------- FORCE ----------------

    private static void BuildForceColumn()
    {
        ForceCell(0, "Continuous sway", "Continuous force +X then -X (re-applied every frame) + drag -> bounded sway (loops)", t =>
        {
            var a = AddForce(t, 0.0, 2.0, "push +X 8N", ForceMode.Continuous, new Vector3(8f, 0f, 0f));
            var b = AddForce(t, 2.0, 2.0, "push -X 8N", ForceMode.Continuous, new Vector3(-8f, 0f, 0f));
            Blend(a, b);
        });

        ForceCell(1, "Z-axis push", "Continuous force +Z then -Z (FixedVector) + drag -> bounded forward/back glide (loops)", t =>
        {
            var a = AddForce(t, 0.0, 2.0, "+Z 8N", ForceMode.Continuous, new Vector3(0f, 0f, 8f));
            var b = AddForce(t, 2.0, 2.0, "-Z 8N", ForceMode.Continuous, new Vector3(0f, 0f, -8f));
            Blend(a, b);
        });

        ForceCell(2, "Lift vs gravity", "Continuous up force (>g) then down force -> rises against gravity, sinks back (loops)", t =>
        {
            var a = AddForce(t, 0.0, 1.5, "lift +Y 16N", ForceMode.Continuous, new Vector3(0f, 16f, 0f));
            var b = AddForce(t, 1.5, 1.5, "let fall 0N", ForceMode.Continuous, new Vector3(0f, 0f, 0f));
            Blend(a, b);
        });
    }

    private static TimelineClip AddForce(ForceTrack t, double start, double dur, string name, ForceMode mode, Vector3 force)
    {
        var c = AddClip<ForceClip>(t, start, dur, name);
        var a = (ForceClip)c.asset;
        a.mode = mode;
        a.directionMode = ForceDir.FixedVector;
        a.space = Space.None;
        a.linearForce = force;
        a.directionStrength = 1f;
        Dirty(c.asset);
        return c;
    }

    // ---------------- VELOCITY ----------------

    private static void BuildVelocityColumn()
    {
        VelocityCell(0, "SetContinuous sway", "Set linear velocity +X then -X each frame (loops)", t =>
        {
            var a = AddVelocity(t, 0.0, 1.6, "v=+X", VelocityMode.SetContinuous, new Vector3(3.5f, 0f, 0f));
            var b = AddVelocity(t, 1.6, 1.6, "v=-X", VelocityMode.SetContinuous, new Vector3(-3.5f, 0f, 0f));
            var c = AddVelocity(t, 3.2, 1.6, "v=-X", VelocityMode.SetContinuous, new Vector3(-3.5f, 0f, 0f));
            var d = AddVelocity(t, 4.8, 1.6, "v=+X", VelocityMode.SetContinuous, new Vector3(3.5f, 0f, 0f));
            Blend(a, b, c, d);
        });

        VelocityCell(1, "Travel + spin", "SetContinuous +Z/-Z travel + constant angular velocity -> glide & spin (loops)", t =>
        {
            var a = AddVelocitySpin(t, 0.0, 1.6, "fwd+spin", VelocityMode.SetContinuous, new Vector3(0f, 0f, 3.5f), new Vector3(0f, 6f, 0f));
            var b = AddVelocitySpin(t, 1.6, 1.6, "back+spin", VelocityMode.SetContinuous, new Vector3(0f, 0f, -3.5f), new Vector3(0f, 6f, 0f));
            var c = AddVelocitySpin(t, 3.2, 1.6, "fwd+spin", VelocityMode.SetContinuous, new Vector3(0f, 0f, 3.5f), new Vector3(0f, 6f, 0f));
            var d = AddVelocitySpin(t, 4.8, 1.6, "back+spin", VelocityMode.SetContinuous, new Vector3(0f, 0f, -3.5f), new Vector3(0f, 6f, 0f));
            Blend(a, b, c, d);
        });

        VelocityCell(2, "Vertical bob", "SetContinuous +Y then -Y (gravity off) -> weightless bob (loops)", t =>
        {
            var a = AddVelocity(t, 0.0, 1.2, "v=+Y", VelocityMode.SetContinuous, new Vector3(0f, 1.8f, 0f));
            var b = AddVelocity(t, 1.2, 1.2, "v=-Y", VelocityMode.SetContinuous, new Vector3(0f, -1.8f, 0f));
            var c = AddVelocity(t, 2.4, 1.2, "v=+Y", VelocityMode.SetContinuous, new Vector3(0f, 1.8f, 0f));
            var d = AddVelocity(t, 3.6, 1.2, "v=-Y", VelocityMode.SetContinuous, new Vector3(0f, -1.8f, 0f));
            Blend(a, b, c, d);
        });
    }

    private static TimelineClip AddVelocity(VelocityTrack t, double start, double dur, string name, VelocityMode mode, Vector3 vel)
    {
        var c = AddClip<VelocityClip>(t, start, dur, name);
        var a = (VelocityClip)c.asset;
        a.mode = mode;
        a.space = Space.None;
        a.linearVelocity = vel;
        Dirty(c.asset);
        return c;
    }

    private static TimelineClip AddVelocitySpin(VelocityTrack t, double start, double dur, string name, VelocityMode mode, Vector3 vel, Vector3 ang)
    {
        var c = AddVelocity(t, start, dur, name, mode, vel);
        ((VelocityClip)c.asset).angularVelocity = ang;
        Dirty(c.asset);
        return c;
    }

    // ---------------- DRAG ----------------

    private static void BuildDragColumn()
    {
        DragCell(0, "Heavy drag (4)", "Continuous force +Z/-Z + linearDrag 4 -> low terminal speed, tight sway (loops)", (ft, dt) =>
        {
            var a = AddForce(ft, 0.0, 1.2, "+Z 10N", ForceMode.Continuous, new Vector3(0f, 0f, 10f));
            var b = AddForce(ft, 1.2, 1.2, "-Z 10N", ForceMode.Continuous, new Vector3(0f, 0f, -10f));
            Blend(a, b);
            AddDrag(dt, 0.0, 2.4, "drag 4", 4f, 1f);
        });

        DragCell(1, "Light drag (1.2)", "Same force, linearDrag 1.2 -> higher terminal speed, wider glide (loops)", (ft, dt) =>
        {
            var a = AddForce(ft, 0.0, 1.2, "+Z 10N", ForceMode.Continuous, new Vector3(0f, 0f, 10f));
            var b = AddForce(ft, 1.2, 1.2, "-Z 10N", ForceMode.Continuous, new Vector3(0f, 0f, -10f));
            Blend(a, b);
            AddDrag(dt, 0.0, 2.4, "drag 1.2", 1.2f, 1f);
        });
    }

    private static TimelineClip AddDrag(DragTrack t, double start, double dur, string name, float lin, float ang)
    {
        var c = AddClip<DragClip>(t, start, dur, name);
        var a = (DragClip)c.asset;
        a.linearDrag = lin;
        a.angularDrag = ang;
        Dirty(c.asset);
        return c;
    }

    // ---------------- GRAVITY ----------------

    private static void BuildGravityColumn()
    {
        GravityCell(0, "Zero-G float", "GravityOverride scale=0 -> weightless; gentle SetContinuous bob, no sag (loops)", 0f, GravColor);
        GravityCell(1, "Low-G (0.3)", "GravityOverride scale=0.3 -> bob droops a little under weak gravity (loops)", 0.3f, GravColor);
        GravityCell(2, "Full-G control (1)", "GravityOverride scale=1 -> bob fights full gravity, sags hardest (loops)", 1f, ControlColor);
    }

    private static void GravityCell(int row, string label, string usage, float scale, Color color)
    {
        var z = row * RowStep;
        var name = "Grav_" + row;
        MakeBall(name + "_Actor", new Vector3(GravX, BallY + 0.3f, z), 0.5f, color, 1f, true);
        var timeline = NewTimeline(TimelineFolder + "/Gravity_" + row + ".playable");
        var gt = timeline.CreateTrack<GravityTrack>(null, "Gravity");
        AddGravity(gt, 0.0, 4.0, "scale=" + scale, scale);
        var ft = timeline.CreateTrack<ForceTrack>(null, "Lift");
        var a = AddForce(ft, 0.0, 1.0, "up 20N", ForceMode.Continuous, new Vector3(0f, 20f, 0f));
        var b = AddForce(ft, 1.0, 1.0, "down 20N", ForceMode.Continuous, new Vector3(0f, -20f, 0f));
        var c = AddForce(ft, 2.0, 1.0, "up 20N", ForceMode.Continuous, new Vector3(0f, 20f, 0f));
        var d = AddForce(ft, 3.0, 1.0, "down 20N", ForceMode.Continuous, new Vector3(0f, -20f, 0f));
        Blend(a, b, c, d);
        var dt = timeline.CreateTrack<DragTrack>(null, "Drag");
        AddDrag(dt, 0.0, 4.0, "drag 2", 2f, 1f);
        FinishCellMulti(timeline, name, GravX, z, label, usage, color);
    }

    // ---------------- BOUNCE / RESTITUTION ----------------

    private static void BuildBounceColumn()
    {
        BounceCell(0, 0.92f, "High restitution", "Continuous slam-down + restitution 0.92 -> tall lively bounces (loops)", 26f);
        BounceCell(1, 0.55f, "Mid restitution", "Same slam, restitution 0.55 -> low damped bounces (loops)", 26f);
        BounceCell(2, 0.95f, "Max restitution", "Same slam, restitution 0.95 -> highest most persistent bounces (loops)", 26f);
    }

    // ---------------- CLAMP ----------------

    private static void BuildClampColumn()
    {
        ClampPairCell(0, "Speed clamp", "Continuous force + VelocityClamp maxLinearSpeed=3 (loops)", 3f);
        ClampPairCell(1, "Tight clamp", "Same force, maxLinearSpeed=1.5 -> slower terminal (loops)", 1.5f);
    }

    // ---------------- TELEPORT ----------------

    private static void BuildTeleportColumn()
    {
        TeleportCell(0, "Teleport to anchor", "PhysicsTeleportClip snaps body onto a landing patch around the red anchor at activation; continuous +X velocity then drifts it away (teleport is one-shot per activation, see CONFUSIONS)");
        TeleportCell(1, "Snap + radius 4", "Same teleport, radius 4 lands farther out; +Z drift companion keeps the cell alive after the snap");
    }

    private static void TeleportCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "Teleport_" + row;
        var anchorName = name + "_Anchor";
        var actorName = name + "_Actor";

        MakeBall(actorName, new Vector3(TeleportX + 2.5f, BallY, z), 0.5f, TeleportColor, 0f, true);
        var anchor = MakeBall(anchorName, new Vector3(TeleportX - 1.2f, BallY, z), 0.45f, TargetColor, 0f, false);
        AddTargets(actorName, anchorName);

        var radius = row == 0 ? 1.6f : 4f;
        var timeline = NewTimeline(TimelineFolder + "/Teleport_" + row + ".playable");
        var tt = timeline.CreateTrack<TeleportTrack>(null, "Teleport");
        var clip = AddClip<TeleportClip>(tt, 0.0, 0.5, "teleport->anchor");
        var ca = (TeleportClip)clip.asset;
        ca.entityToTeleport = TargetSlot.Owner;
        ca.teleportRelativeTo = TargetSlot.Target;
        ca.azimuthTarget = TargetSlot.Target;
        ca.radius = radius;
        ca.elevationCenter = 0f;
        ca.elevationHalfRange = 0f;
        ca.resetVelocity = true;
        Dirty(clip.asset);

        var vt = timeline.CreateTrack<VelocityTrack>(null, "Drift");
        var v = AddVelocity(vt, 0.5, 3.5, "drift +X", VelocityMode.SetContinuous, new Vector3(1.6f, 0f, 0f));
        v.blendInDuration = 0.3;

        var wire = MultiWire(timeline, name, actorName);
        wire.Binds.Add(new TrackBind { TrackName = "Teleport", BindName = actorName, Kind = BindKind.Targets });
        wire.Binds.Add(new TrackBind { TrackName = "Drift", BindName = actorName, Kind = BindKind.Body });
        FinishWire(timeline, wire, TeleportX, z, label, usage, TeleportColor);
    }

    // ---------------- RICOCHET ----------------

    private static void BuildRicochetColumn()
    {
        RicochetCell(0, "Ricochet ray + wall bounce", "PhysicsRicochetClip casts a ricochet ray (maxBounces 3) off the magenta wall each loop; a continuous +X velocity also drives the body into a real restitution bounce off that wall (loops)");
    }

    private static void RicochetCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "Ricochet_" + row;
        var actorName = name + "_Actor";

        MakeWall(name + "_Wall", new Vector3(RicochetX + 3.0f, 1.5f, z), new Vector3(0.4f, 3f, 4f), BarrierColor, true, CatAll, CatAll);
        MakeBall(actorName, new Vector3(RicochetX - 2.5f, BallY, z), 0.5f, RicochetColor, 0f, true, 0.95f);

        var timeline = NewTimeline(TimelineFolder + "/Ricochet_" + row + ".playable");
        var rt = timeline.CreateTrack<RicochetTrack>(null, "Ricochet");
        var clip = AddClip<RicochetClip>(rt, 0.0, 0.5, "cast ricochet");
        var ca = (RicochetClip)clip.asset;
        ca.maxBounces = 3;
        ca.maxDistance = 50f;
        ca.minGrazingAngle = 15f;
        ca.rayOrigin = TargetSlot.Self;
        ca.rayDirection = TargetSlot.Self;
        Dirty(clip.asset);

        var vt = timeline.CreateTrack<VelocityTrack>(null, "Drive");
        var a = AddVelocity(vt, 0.0, 1.5, "drive +X", VelocityMode.SetContinuous, new Vector3(4f, 0f, 0f));
        var b = AddVelocity(vt, 1.5, 1.5, "drive -X", VelocityMode.SetContinuous, new Vector3(-4f, 0f, 0f));
        Blend(a, b);

        var wire = MultiWire(timeline, name, actorName);
        wire.Binds.Add(new TrackBind { TrackName = "Ricochet", BindName = actorName, Kind = BindKind.Go });
        wire.Binds.Add(new TrackBind { TrackName = "Drive", BindName = actorName, Kind = BindKind.Body });
        FinishWire(timeline, wire, RicochetX, z, label, usage, RicochetColor);
    }

    // ---------------- LINEAR PID ----------------

    private static void BuildLinearPidColumn()
    {
        LinearPidSeekCell(0, "Seek orbiting target", "PhysicsLinearPIDClip (TargetLocal) drives a force toward an orbiting red target -> body chases it forever (loops)");
        LinearPidWorldCell(1, "World hover A<->B", "PhysicsLinearPIDClip (World) makes targetOffset an ABSOLUTE world goal; two clips alternate two points so the body flies A->B->A each loop (loops)");
    }

    private static void LinearPidSeekCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "LinPid_" + row;
        var actorName = name + "_Actor";
        var targetName = name + "_Target";

        MakeBall(actorName, new Vector3(LinPidX - 1.5f, BallY + 1.0f, z), 0.5f, LinPidColor, 0f, true);
        var lpt = MakeBall(targetName, new Vector3(LinPidX, BallY + 1.0f, z), 0.4f, TargetColor, 0f, true);
        SetBodyFilter(lpt, CatTrigger, 0u);
        AddTargets(actorName, targetName);
        BuildOrbitTarget(name, targetName, new Vector3(LinPidX, BallY + 1.0f, z));

        var timeline = NewTimeline(TimelineFolder + "/LinPid_" + row + ".playable");
        var pt = timeline.CreateTrack<LinearPIDTrack>(null, "Linear PID");
        var clip = AddClip<LinearPIDClip>(pt, 0.0, 6.0, "seek");
        var ca = (LinearPIDClip)clip.asset;
        ca.trackingTarget = TargetSlot.Target;
        ca.targetMode = LinearMode.TargetLocal;
        ca.targetOffset = Vector3.zero;
        ca.strength = 1f;
        Dirty(clip.asset);

        var wire = MultiWire(timeline, name, actorName);
        wire.DefaultKind = BindKind.Body;
        FinishWire(timeline, wire, LinPidX, z, label, usage, LinPidColor);
    }

    private static void LinearPidWorldCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "LinPid_" + row;
        var actorName = name + "_Actor";

        MakeBall(actorName, new Vector3(LinPidX, BallY + 1.0f, z), 0.5f, LinPidColor, 0f, true);

        var timeline = NewTimeline(TimelineFolder + "/LinPid_" + row + ".playable");
        var pt = timeline.CreateTrack<LinearPIDTrack>(null, "Linear PID");
        var ct = timeline.CreateTrack<ClampTrack>(null, "Clamp");
        AddClamp(ct, 0.0, 6.0, "clamp", 6f, 5f);

        var a = AddClip<LinearPIDClip>(pt, 0.0, 3.0, "to A");
        var aa = (LinearPIDClip)a.asset;
        aa.trackingTarget = TargetSlot.None;
        aa.targetMode = LinearMode.World;
        aa.targetOffset = new Vector3(LinPidX - 2.0f, BallY + 2.4f, z);
        aa.strength = 1f;
        Dirty(a.asset);

        var b = AddClip<LinearPIDClip>(pt, 3.0, 3.0, "to B");
        var ba = (LinearPIDClip)b.asset;
        ba.trackingTarget = TargetSlot.None;
        ba.targetMode = LinearMode.World;
        ba.targetOffset = new Vector3(LinPidX + 2.0f, BallY + 0.8f, z);
        ba.strength = 1f;
        Dirty(b.asset);
        a.blendInDuration = 0.4;
        b.blendInDuration = 0.4;

        var wire = MultiWire(timeline, name, actorName);
        wire.DefaultKind = BindKind.Body;
        FinishWire(timeline, wire, LinPidX, z, label, usage, LinPidColor);
    }

    // ---------------- ANGULAR PID ----------------

    private static void BuildAngularPidColumn()
    {
        AngularPidCell(0, "Look at orbiting target", "PhysicsAngularPIDClip (LookAtTarget) applies torque to face an orbiting target -> body keeps turning to track it (loops)", AngularMode.LookAtTarget);
    }

    private static void AngularPidCell(int row, string label, string usage, AngularMode mode)
    {
        var z = row * RowStep;
        var name = "AngPid_" + row;
        var actorName = name + "_Actor";
        var targetName = name + "_Target";

        var actor = MakePrimitive(PrimitiveType.Cube, actorName, new Vector3(AngPidX, BallY + 0.5f, z), new Vector3(0.45f, 0.45f, 1.6f), AngPidColor);
        ConfigureBody(actor, 0f, true, 0.2f, BoxColliderSize(actor));
        var nose = MakePrimitive(PrimitiveType.Sphere, actorName + "_Nose", new Vector3(AngPidX, BallY + 0.5f, z + 0.95f), new Vector3(0.55f, 0.55f, 0.45f), Color.white);
        nose.transform.SetParent(actor.transform, true);

        var apt = MakeBall(targetName, new Vector3(AngPidX, BallY + 0.5f, z + 2.0f), 0.4f, TargetColor, 0f, true);
        SetBodyFilter(apt, CatTrigger, 0u);
        AddTargets(actorName, targetName);
        BuildOrbitTarget(name, targetName, new Vector3(AngPidX, BallY + 0.5f, z + 1.6f));

        var timeline = NewTimeline(TimelineFolder + "/AngPid_" + row + ".playable");
        var pt = timeline.CreateTrack<AngularPIDTrack>(null, "Angular PID");
        var clip = AddClip<AngularPIDClip>(pt, 0.0, 6.0, "look");
        var ca = (AngularPIDClip)clip.asset;
        ca.trackingTarget = TargetSlot.Target;
        ca.targetMode = mode;
        ca.strength = 1f;
        Dirty(clip.asset);

        var wire = MultiWire(timeline, name, actorName);
        wire.DefaultKind = BindKind.Body;
        FinishWire(timeline, wire, AngPidX, z, label, usage, AngPidColor);
    }

    // ---------------- KINEMATIC OVERRIDE ----------------

    private static void BuildKinematicColumn()
    {
        KinematicCell(0, true, "Kinematic carry", "PhysicsKinematicOverrideClip makes the body kinematic (infinite-mass, force/gravity-immune); a Velocity track bobs it on rails the whole loop (loops)");
        KinematicCell(1, false, "Dynamic control", "Same velocity bob but NO kinematic override -> gravity + collisions perturb the dynamic body (control)");
    }

    private static void KinematicCell(int row, bool kinematic, string label, string usage)
    {
        var z = row * RowStep;
        var name = "Kin_" + row;
        var actorName = name + "_Actor";
        var color = kinematic ? KinColor : ControlColor;
        MakeBall(actorName, new Vector3(KinX, BallY + 1.6f, z), 0.5f, color, kinematic ? 0f : 1f, true);

        var timeline = NewTimeline(TimelineFolder + "/Kin_" + row + ".playable");
        if (kinematic)
        {
            var kt = timeline.CreateTrack<KinematicTrack>(null, "Kinematic");
            var kc = AddClip<KinematicClip>(kt, 0.0, 4.8, "kinematic");
            var ka = (KinematicClip)kc.asset;
            ka.isKinematic = true;
            ka.zeroVelocityOnEnter = true;
            ka.zeroGravity = true;
            Dirty(kc.asset);
        }

        var vt = timeline.CreateTrack<VelocityTrack>(null, "Bob");
        var a = AddVelocity(vt, 0.0, 1.2, "up", VelocityMode.SetContinuous, new Vector3(0f, 1.6f, 0f));
        var b = AddVelocity(vt, 1.2, 1.2, "down", VelocityMode.SetContinuous, new Vector3(0f, -1.6f, 0f));
        var c = AddVelocity(vt, 2.4, 1.2, "up", VelocityMode.SetContinuous, new Vector3(0f, 1.6f, 0f));
        var d = AddVelocity(vt, 3.6, 1.2, "down", VelocityMode.SetContinuous, new Vector3(0f, -1.6f, 0f));
        Blend(a, b, c, d);

        var wire = MultiWire(timeline, name, actorName);
        wire.Binds.Add(new TrackBind { TrackName = "Kinematic", BindName = actorName, Kind = BindKind.Go });
        wire.Binds.Add(new TrackBind { TrackName = "Bob", BindName = actorName, Kind = BindKind.Body });
        FinishWire(timeline, wire, KinX, z, label, usage, color);
    }

    // ---------------- FILTER OVERRIDE ----------------

    private static void BuildFilterColumn()
    {
        FilterCell(0, "Pass through barrier", "PhysicsFilterOverrideClip clears CollidesWith while active -> the ForceUnique body passes THROUGH the magenta barrier; outside the clip it collides. Continuous +X/-X velocity carries it back and forth (loops)");
    }

    private static void FilterCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "Filter_" + row;
        var actorName = name + "_Actor";

        MakeWall(name + "_Barrier", new Vector3(FilterX, 1.0f, z), new Vector3(0.4f, 2f, 4f), BarrierColor, true, CatBarrier, CatBody);

        var actor = MakeBall(actorName, new Vector3(FilterX - 3.0f, BallY, z), 0.5f, FilterColor, 0f, true);
        SetBodyFilter(actor, CatBody, CatBarrier);
        SetForceUnique(actor, true);

        var timeline = NewTimeline(TimelineFolder + "/Filter_" + row + ".playable");
        var ftk = timeline.CreateTrack<FilterTrack>(null, "Filter");
        var fc = AddClip<FilterClip>(ftk, 1.0, 2.0, "no collide");
        var fa = (FilterClip)fc.asset;
        fa.belongsToOverride = MakeTags(CatBody);
        fa.collidesWithOverride = MakeTags(0u);
        fa.restoreOnExit = true;
        Dirty(fc.asset);

        var vt = timeline.CreateTrack<VelocityTrack>(null, "Drive");
        var a = AddVelocity(vt, 0.0, 2.0, "drive +X", VelocityMode.SetContinuous, new Vector3(3.2f, 0f, 0f));
        var b = AddVelocity(vt, 2.0, 2.0, "drive -X", VelocityMode.SetContinuous, new Vector3(-3.2f, 0f, 0f));
        Blend(a, b);

        var wire = MultiWire(timeline, name, actorName);
        wire.Binds.Add(new TrackBind { TrackName = "Filter", BindName = actorName, Kind = BindKind.Go });
        wire.Binds.Add(new TrackBind { TrackName = "Drive", BindName = actorName, Kind = BindKind.Body });
        FinishWire(timeline, wire, FilterX, z, label, usage, FilterColor);
    }

    // ---------------- STATEFUL TRIGGER ----------------

    private static void BuildTriggerColumn()
    {
        TriggerForceCell(0, "Trigger force (radial)", "StatefulTriggerTrack on a trigger volume; PhysicsTriggerForceClip (radial, continuous) shoves any body inside the yellow zone. A passenger driven by velocity passes through each loop -> gets kicked (loops)");
        TriggerConditionCell(1, "Trigger condition + query", "Same trigger volume; PhysicsTriggerConditionClip routes a condition on Enter and PhysicsTriggerQueryClip selects the nearest contacting body into a Custom slot (no motion of its own - sensor coverage)");
    }

    private static GameObject MakeTriggerZone(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(3.2f, 2.4f, 3.2f);
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        var mat = MakeMaterial(name, new Color(TriggerColor.r, TriggerColor.g, TriggerColor.b, 0.25f));
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideCollisionResponse = true;
        shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(CatTrigger);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(CatBody);

        var body = go.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Static;

        go.AddComponent<TriggerAuthoring>();
        var targets = go.AddComponent<TargetsAuthoring>();
        targets.Owner = go;

        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static GameObject MakeTriggerPassenger(string name, Vector3 start, float z, out TimelineAsset passengerTimeline, out string passengerDir)
    {
        var go = MakeBall(name, start, 0.5f, new Color(0.95f, 0.7f, 0.2f), 0f, true);
        SetBodyFilter(go, CatBody, CatTrigger | CatBarrier);

        var timeline = NewTimeline(TimelineFolder + "/" + name + "_Drive.playable");
        var vt = timeline.CreateTrack<VelocityTrack>(null, "Velocity");
        var a = AddVelocity(vt, 0.0, 2.2, "in +X", VelocityMode.SetContinuous, new Vector3(2.6f, 0f, 0f));
        var b = AddVelocity(vt, 2.2, 2.2, "out -X", VelocityMode.SetContinuous, new Vector3(-2.6f, 0f, 0f));
        Blend(a, b);
        FixDuration(timeline);
        Dirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) Dirty(tr);
        AssetDatabase.SaveAssets();

        passengerDir = name + "_Director";
        MakeDirector(passengerDir);
        passengerTimeline = timeline;
        var wire = new CellWire { DirectorName = passengerDir, TimelinePath = AssetDatabase.GetAssetPath(timeline), BindName = name, DefaultKind = BindKind.Body, Binds = new List<TrackBind>() };
        Wires.Add(wire);
        return go;
    }

    private static void TriggerForceCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "Trigger_" + row;
        var zoneName = name + "_Zone";
        var passengerName = name + "_Passenger";

        MakeTriggerZone(zoneName, new Vector3(TriggerX, 1.2f, z));
        MakeTriggerPassenger(passengerName, new Vector3(TriggerX - 3.0f, BallY, z), z, out _, out _);

        var timeline = NewTimeline(TimelineFolder + "/Trigger_" + row + ".playable");
        var tt = timeline.CreateTrack<TriggerTrack>(null, "Trigger");
        var fc = AddClip<TriggerForceClip>(tt, 0.0, 6.0, "radial shove");
        var fa = (TriggerForceClip)fc.asset;
        fa.triggerState = EventState.Stay;
        fa.forceType = TriggerForceType.Radial;
        fa.mode = ForceMode.Continuous;
        fa.magnitude = -14f;
        fa.applyTo = TargetSlot.Target;
        fa.ignoreTarget = TargetSlot.Owner;
        Dirty(fc.asset);

        var wire = new CellWire
        {
            DirectorName = name + "_Director",
            TimelinePath = AssetDatabase.GetAssetPath(timeline),
            BindName = zoneName,
            DefaultKind = BindKind.Trigger,
            Binds = new List<TrackBind> { new TrackBind { TrackName = "Trigger", BindName = zoneName, Kind = BindKind.Trigger } },
        };
        FinishWire(timeline, wire, TriggerX, z, label, usage, TriggerColor);
    }

    private static void TriggerConditionCell(int row, string label, string usage)
    {
        var z = row * RowStep;
        var name = "TrigCQ_" + row;
        var zoneName = name + "_Zone";
        var passengerName = name + "_Passenger";

        MakeTriggerZone(zoneName, new Vector3(TriggerX, 1.2f, z));
        MakeTriggerPassenger(passengerName, new Vector3(TriggerX - 3.0f, BallY, z), z, out _, out _);

        var timeline = NewTimeline(TimelineFolder + "/TrigCQ_" + row + ".playable");
        var tt = timeline.CreateTrack<TriggerTrack>(null, "Trigger");

        var cc = AddClip<TriggerConditionClip>(tt, 0.0, 6.0, "on enter");
        var ccA = (TriggerConditionClip)cc.asset;
        ccA.triggerState = EventState.Enter;
        ccA.collidesWith = MakeTags(CatBody);
        ccA.routeTo = TargetSlot.Target;
        ccA.ignoreTarget = TargetSlot.Owner;
        Dirty(cc.asset);

        var qt = timeline.CreateTrack<TriggerTrack>(null, "Query");
        var qc = AddClip<TriggerQueryClip>(qt, 0.0, 6.0, "nearest");
        var qcA = (TriggerQueryClip)qc.asset;
        qcA.triggerState = EventState.Stay;
        qcA.collidesWith = MakeTags(CatBody);
        qcA.selection = TriggerSelection.Nearest;
        qcA.routeTo = TargetSlot.Self;
        qcA.ignoreTarget = TargetSlot.Owner;
        Dirty(qc.asset);

        var wire = new CellWire
        {
            DirectorName = name + "_Director",
            TimelinePath = AssetDatabase.GetAssetPath(timeline),
            BindName = zoneName,
            DefaultKind = BindKind.Trigger,
            Binds = new List<TrackBind>
            {
                new TrackBind { TrackName = "Trigger", BindName = zoneName, Kind = BindKind.Trigger },
                new TrackBind { TrackName = "Query", BindName = zoneName, Kind = BindKind.Trigger },
            },
        };
        FinishWire(timeline, wire, TriggerX, z, label, usage, TriggerColor);
    }

    // ---------------- orbit target companion ----------------

    private static void BuildOrbitTarget(string baseName, string targetName, Vector3 home)
    {
        var dirName = baseName + "_TgtDirector";
        var path = TimelineFolder + "/" + baseName + "_Target.playable";
        MakeDirector(dirName);
        var timeline = NewTimeline(path);
        var track = timeline.CreateTrack<VelocityTrack>(null, "Velocity");
        var a = AddVelocity(track, 0.0, 1.4, "right", VelocityMode.SetContinuous, new Vector3(2.0f, 0f, 0f));
        var b = AddVelocity(track, 1.4, 1.4, "left", VelocityMode.SetContinuous, new Vector3(-2.0f, 0f, 0f));
        var c = AddVelocity(track, 2.8, 1.4, "fwd", VelocityMode.SetContinuous, new Vector3(0f, 0f, 2.0f));
        var d = AddVelocity(track, 4.2, 1.4, "back", VelocityMode.SetContinuous, new Vector3(0f, 0f, -2.0f));
        Blend(a, b, c, d);
        FixDuration(timeline);
        Dirty(timeline, track);
        AssetDatabase.SaveAssets();
        Wires.Add(new CellWire { DirectorName = dirName, TimelinePath = path, BindName = targetName, DefaultKind = BindKind.Body, Binds = new List<TrackBind>() });
    }

    // ---------------- cell scaffolding ----------------

    private static void ForceCell(int row, string label, string usage, System.Action<ForceTrack> fill)
    {
        var z = row * RowStep;
        var name = "Force_" + row;
        var gravFactor = row == 2 ? 1f : 0f;
        var startY = row == 2 ? BallY + 1.5f : BallY;
        MakeBall(name + "_Actor", new Vector3(ForceX, startY, z), 0.5f, ForceColor, gravFactor, true);
        var timeline = NewTimeline(TimelineFolder + "/Force_" + row + ".playable");
        var track = timeline.CreateTrack<ForceTrack>(null, "Force");
        fill(track);
        var dt = timeline.CreateTrack<DragTrack>(null, "Drag");
        AddDrag(dt, 0.0, 4.0, "drag 2", 2f, 1f);
        FinishCellMulti(timeline, name, ForceX, z, label, usage, ForceColor);
    }

    private static void VelocityCell(int row, string label, string usage, System.Action<VelocityTrack> fill)
    {
        var z = row * RowStep;
        var name = "Vel_" + row;
        MakeBall(name + "_Actor", new Vector3(VelX, BallY, z), 0.5f, VelColor, 0f, true);
        var timeline = NewTimeline(TimelineFolder + "/Velocity_" + row + ".playable");
        var track = timeline.CreateTrack<VelocityTrack>(null, "Velocity");
        fill(track);
        FinishCellMulti(timeline, name, VelX, z, label, usage, VelColor);
    }

    private static void DragCell(int row, string label, string usage, System.Action<ForceTrack, DragTrack> fill)
    {
        var z = row * RowStep;
        var name = "Drag_" + row;
        MakeBall(name + "_Actor", new Vector3(DragX, BallY, z), 0.5f, DragColor, 0f, true);
        var timeline = NewTimeline(TimelineFolder + "/Drag_" + row + ".playable");
        var dt = timeline.CreateTrack<DragTrack>(null, "Drag");
        var ft = timeline.CreateTrack<ForceTrack>(null, "Force");
        fill(ft, dt);
        FinishCellMulti(timeline, name, DragX, z, label, usage, DragColor);
    }

    private static void BounceCell(int row, float restitution, string label, string usage, float downForce)
    {
        var z = row * RowStep;
        var name = "Bounce_" + row;
        MakeBall(name + "_Actor", new Vector3(BounceX, BallY + 2.5f, z), 0.5f, BounceColor, 1f, true, restitution);
        var timeline = NewTimeline(TimelineFolder + "/Bounce_" + row + ".playable");
        var ft = timeline.CreateTrack<ForceTrack>(null, "Slam");
        var a = AddForce(ft, 0.0, 0.5, "slam down", ForceMode.Continuous, new Vector3(0f, -downForce, 0f));
        var b = AddForce(ft, 0.5, 1.7, "free bounce", ForceMode.Continuous, new Vector3(0f, 0f, 0f));
        var c = AddForce(ft, 2.2, 0.5, "slam down", ForceMode.Continuous, new Vector3(0f, -downForce, 0f));
        var d = AddForce(ft, 2.7, 1.7, "free bounce", ForceMode.Continuous, new Vector3(0f, 0f, 0f));
        Blend(a, b, c, d);
        FinishCellMulti(timeline, name, BounceX, z, label, usage, BounceColor);
    }

    private static void ClampPairCell(int row, string label, string usage, float maxSpeed)
    {
        var z = row * RowStep;
        var name = "Clamp_" + row;
        MakeBall(name + "_Actor", new Vector3(ClampX, BallY, z), 0.5f, ClampColor, 0f, true);
        var timeline = NewTimeline(TimelineFolder + "/Clamp_" + row + ".playable");
        var ft = timeline.CreateTrack<ForceTrack>(null, "Force");
        var a = AddForce(ft, 0.0, 1.3, "force +X 40N", ForceMode.Continuous, new Vector3(40f, 0f, 0f));
        var b = AddForce(ft, 1.3, 1.3, "force -X 40N", ForceMode.Continuous, new Vector3(-40f, 0f, 0f));
        Blend(a, b);
        var ct = timeline.CreateTrack<ClampTrack>(null, "Clamp");
        AddClamp(ct, 0.0, 2.6, "clamp", maxSpeed, 5f);
        FinishCellMulti(timeline, name, ClampX, z, label, usage, ClampColor);
    }

    private static TimelineClip AddGravity(GravityTrack t, double start, double dur, string name, float scale)
    {
        var c = AddClip<GravityClip>(t, start, dur, name);
        var a = (GravityClip)c.asset;
        a.gravityScale = scale;
        a.restoreOnExit = true;
        Dirty(c.asset);
        return c;
    }

    private static TimelineClip AddClamp(ClampTrack t, double start, double dur, string name, float maxLin, float maxAng)
    {
        var c = AddClip<ClampClip>(t, start, dur, name);
        var a = (ClampClip)c.asset;
        a.maxLinearSpeed = maxLin;
        a.maxAngularSpeed = maxAng;
        Dirty(c.asset);
        return c;
    }

    // ---------------- wire/caption plumbing ----------------

    private static CellWire MultiWire(TimelineAsset timeline, string name, string bindName)
    {
        return new CellWire
        {
            DirectorName = name + "_Director",
            TimelinePath = AssetDatabase.GetAssetPath(timeline),
            BindName = bindName,
            DefaultKind = BindKind.Body,
            Binds = new List<TrackBind>(),
        };
    }

    private static void FinishWire(TimelineAsset timeline, CellWire wire, float x, float z, string label, string usage, Color color)
    {
        FixDuration(timeline);
        Dirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) Dirty(tr);
        AssetDatabase.SaveAssets();
        MakeDirector(wire.DirectorName);
        Wires.Add(wire);
        Captions.Add(new CaptionData { Title = label, Usage = usage, CellPos = new Vector3(x, 3.4f, z), Color = color });
    }

    private static void FinishCellMulti(TimelineAsset timeline, string name, float x, float z, string label, string usage, Color color)
    {
        FixDuration(timeline);
        Dirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) Dirty(tr);
        AssetDatabase.SaveAssets();
        MakeDirector(name + "_Director");
        Wires.Add(new CellWire { DirectorName = name + "_Director", TimelinePath = AssetDatabase.GetAssetPath(timeline), BindName = name + "_Actor", DefaultKind = BindKind.Body, Binds = new List<TrackBind>() });
        Captions.Add(new CaptionData { Title = label, Usage = usage, CellPos = new Vector3(x, 3.4f, z), Color = color });
    }

    private static TimelineAsset NewTimeline(string path)
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, path);
        return timeline;
    }

    private static void Blend(params TimelineClip[] clips)
    {
        foreach (var c in clips)
        {
            c.blendInDuration = 0.4;
        }
    }

    private static void FixDuration(TimelineAsset timeline)
    {
        var end = 0.0;
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
            {
                var clipEnd = clip.start + clip.duration;
                if (clipEnd > end)
                {
                    end = clipEnd;
                }
            }
        }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
    }

    private static TimelineClip AddClip<T>(TrackAsset track, double start, double duration, string name) where T : PlayableAsset
    {
        var clip = track.CreateClip<T>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = name;
        return clip;
    }

    private static void WireCell(CellWire w)
    {
        var director = GameObject.Find(w.DirectorName).GetComponent<PlayableDirector>();
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(w.TimelinePath);
        director.playableAsset = timeline;

        foreach (var track in timeline.GetOutputTracks())
        {
            var bind = FindBind(w, track.name);
            var go = GameObject.Find(bind.BindName);
            Object value = BindValue(go, bind.Kind);
            director.SetGenericBinding(track, value);
        }

        EditorUtility.SetDirty(director);
    }

    private static TrackBind FindBind(CellWire w, string trackName)
    {
        if (w.Binds != null)
        {
            foreach (var b in w.Binds)
            {
                if (b.TrackName == trackName)
                {
                    return b;
                }
            }
        }

        return new TrackBind { TrackName = trackName, BindName = w.BindName, Kind = w.DefaultKind };
    }

    private static Object BindValue(GameObject go, BindKind kind)
    {
        switch (kind)
        {
            case BindKind.Go: return go;
            case BindKind.Targets: return go.GetComponent<TargetsAuthoring>();
            case BindKind.Trigger: return go.GetComponent<TriggerAuthoring>();
            default: return go.GetComponent<PhysicsBodyAuthoring>();
        }
    }

    private static PlayableDirector MakeDirector(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        var director = go.AddComponent<PlayableDirector>();
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        return director;
    }

    private static void AddTargets(string actorName, string targetName)
    {
        var actor = GameObject.Find(actorName);
        var target = GameObject.Find(targetName);
        var targets = actor.GetComponent<TargetsAuthoring>();
        if (targets == null)
        {
            targets = actor.AddComponent<TargetsAuthoring>();
        }

        targets.Target = target;
        targets.Owner = actor;
        EditorUtility.SetDirty(targets);
    }

    private static PhysicsCategoryTags MakeTags(uint value)
    {
        return new PhysicsCategoryTags { Value = value };
    }

    private static Vector3 BoxColliderSize(GameObject go)
    {
        var s = go.transform.localScale;
        return new Vector3(s.x, s.y, s.z);
    }

    private static void ConfigureBody(GameObject go, float gravityFactor, bool dynamic, float restitution, Vector3 boxSize)
    {
        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideRestitution = true;
        shape.Restitution = new PhysicsMaterialCoefficient { Value = restitution, CombineMode = Unity.Physics.Material.CombinePolicy.Maximum };
        shape.OverrideFriction = true;
        shape.Friction = new PhysicsMaterialCoefficient { Value = 0.4f, CombineMode = Unity.Physics.Material.CombinePolicy.GeometricMean };
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(CatBody);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(CatGround | CatBody | CatBarrier | CatTrigger);

        var body = go.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = dynamic ? BodyMotionType.Dynamic : BodyMotionType.Static;
        body.Mass = 1f;
        body.GravityFactor = gravityFactor;
        body.LinearDamping = 0.05f;
        body.AngularDamping = 0.05f;
    }

    private static GameObject MakeBall(string name, Vector3 pos, float radius, Color color, float gravityFactor, bool dynamic, float restitution = 0.2f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, radius * 2f);
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);

        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity);
        shape.OverrideRestitution = true;
        shape.Restitution = new PhysicsMaterialCoefficient { Value = restitution, CombineMode = Unity.Physics.Material.CombinePolicy.Maximum };
        shape.OverrideFriction = true;
        shape.Friction = new PhysicsMaterialCoefficient { Value = 0.4f, CombineMode = Unity.Physics.Material.CombinePolicy.GeometricMean };
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(CatBody);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(CatGround | CatBody | CatBarrier | CatTrigger);

        var body = go.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = dynamic ? BodyMotionType.Dynamic : BodyMotionType.Static;
        body.Mass = 1f;
        body.GravityFactor = gravityFactor;
        body.LinearDamping = 0.05f;
        body.AngularDamping = 0.05f;

        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static void SetBodyFilter(GameObject go, uint belongsTo, uint collidesWith)
    {
        var shape = go.GetComponent<PhysicsShapeAuthoring>();
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(belongsTo);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(collidesWith);
        EditorUtility.SetDirty(shape);
    }

    private static void SetForceUnique(GameObject go, bool unique)
    {
        var shape = go.GetComponent<PhysicsShapeAuthoring>();
        shape.ForceUnique = unique;
        EditorUtility.SetDirty(shape);
    }

    private static GameObject MakePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static GameObject MakePad(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, PadColor);

        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideRestitution = true;
        shape.Restitution = new PhysicsMaterialCoefficient { Value = 0.0f, CombineMode = Unity.Physics.Material.CombinePolicy.Maximum };
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(CatGround);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(CatBody);

        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static void BuildPads()
    {
        MakePad("Ground_Floor", new Vector3(0f, -0.4f, 5f), new Vector3(130f, 0.8f, 30f));

        float[] xs = { ForceX, VelX, DragX, GravX, BounceX, ClampX, TeleportX, RicochetX, LinPidX, AngPidX, KinX, FilterX, TriggerX };
        string[] names = { "Force", "Vel", "Drag", "Grav", "Bounce", "Clamp", "Teleport", "Ricochet", "LinPid", "AngPid", "Kin", "Filter", "Trigger" };
        for (var i = 0; i < xs.Length; i++)
        {
            MakePad(names[i] + "_Pad", new Vector3(xs[i], PadY, 5f), new Vector3(6.4f, 0.12f, 24f));
        }
    }

    private static void MakeWall(string name, Vector3 pos, Vector3 size, Color color, bool visible, uint belongsTo, uint collidesWith)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        if (visible)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);
        }
        else
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        }

        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideRestitution = true;
        shape.Restitution = new PhysicsMaterialCoefficient { Value = 0.6f, CombineMode = Unity.Physics.Material.CombinePolicy.Maximum };
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = MakeTags(belongsTo);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = MakeTags(collidesWith);

        var body = go.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Static;

        SceneManager.MoveGameObjectToScene(go, activeSub);
    }

    private static UnityEngine.Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var mat = new UnityEngine.Material(shader) { name = name + "_Mat" };
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        return mat;
    }

    private static void BuildParent()
    {
        FrameCamera();
        RenderSettings.fog = false;

        MakeBanner("Title_Banner", new Vector3(0f, 22.0f, 0f), new Vector3(70f, 4.2f, 0.1f));
        MakeWorldLabel("Title", "PHYSICS TIMELINE GRID — FULL TRACK & CLIP COVERAGE", new Vector3(0f, 22.6f, -0.4f), 70f, Color.white, 6.5f, TextAlignmentOptions.Center);
        MakeWorldLabel("Subtitle", "12 tracks · ~17 clips on simulated PhysicsBodies   ·   com.bovinelabs.timeline.physics", new Vector3(0f, 21.0f, -0.4f), 70f, new Color(0.85f, 0.9f, 1f), 2.4f, TextAlignmentOptions.Center);

        MakeColumnHeader("Force_Header", "FORCE", ForceX, ForceColor);
        MakeColumnHeader("Vel_Header", "VELOCITY", VelX, VelColor);
        MakeColumnHeader("Drag_Header", "DRAG", DragX, DragColor);
        MakeColumnHeader("Grav_Header", "GRAVITY", GravX, GravColor);
        MakeColumnHeader("Bounce_Header", "RESTITUTION", BounceX, BounceColor);
        MakeColumnHeader("Clamp_Header", "VEL CLAMP", ClampX, ClampColor);
        MakeColumnHeader("Teleport_Header", "TELEPORT", TeleportX, TeleportColor);
        MakeColumnHeader("Ricochet_Header", "RICOCHET", RicochetX, RicochetColor);
        MakeColumnHeader("LinPid_Header", "LINEAR PID", LinPidX, LinPidColor);
        MakeColumnHeader("AngPid_Header", "ANGULAR PID", AngPidX, AngPidColor);
        MakeColumnHeader("Kin_Header", "KINEMATIC", KinX, KinColor);
        MakeColumnHeader("Filter_Header", "FILTER", FilterX, FilterColor);
        MakeColumnHeader("Trigger_Header", "STATEFUL TRIGGER", TriggerX, TriggerColor);

        foreach (var cap in Captions)
        {
            MakeCaption(cap.Title, cap.Usage, cap.CellPos, cap.Color);
        }

        MakeBanner("Usage_Banner", new Vector3(0f, 0.7f, -9.0f), new Vector3(80f, 2.2f, 0.1f));
        MakeWorldLabel("Usage", "Dynamic PhysicsBody actors on static pads   ·   each director loops (FixedLength + Loop)   ·   Continuous forces/velocity & PID keep moving; teleport/ricochet/trigger-spawn clips fire once per activation (see captions)", new Vector3(0f, 0.7f, -9.3f), 78f, new Color(0.96f, 0.97f, 1f), 2.0f, TextAlignmentOptions.Center);

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        if (sceneAsset == null)
        {
            Debug.LogError("PhysicsShowcase: sub-scene asset missing at " + SubPath);
            return;
        }

        var subSceneGo = new GameObject("Showcase SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);
    }

    private static void MakeColumnHeader(string name, string text, float x, Color color)
    {
        var pos = new Vector3(x, 4.6f, -5.5f);
        MakeBanner(name + "_Banner", pos + new Vector3(0f, 0f, 0.08f), new Vector3(6.2f, 1.5f, 0.1f));
        MakeWorldLabel(name, "<b>" + text + "</b>", pos, 6.0f, color, 3.0f, TextAlignmentOptions.Center);
    }

    private static float CaptionY(float z)
    {
        return 4.0f + z * 0.2f;
    }

    private static void MakeCaption(string title, string usage, Vector3 cellPos, Color color)
    {
        var z = cellPos.z;
        var y = CaptionY(z);
        MakeBanner("CapBanner_" + title + "_" + z, new Vector3(cellPos.x, y, z + 0.06f), new Vector3(6.2f, 2.1f, 0.05f));
        MakeWorldLabel("Cap_" + title + "_" + z, "<b>" + title + "</b>", new Vector3(cellPos.x, y + 0.5f, z), 6.0f, color, 2.7f, TextAlignmentOptions.Center);
        MakeWorldLabel("Use_" + title + "_" + z, usage, new Vector3(cellPos.x, y - 0.42f, z), 6.0f, new Color(0.95f, 0.96f, 1f), 1.4f, TextAlignmentOptions.Center);
    }

    private static void FrameCamera()
    {
        var required = GameObject.Find("Required In Scene");
        if (required == null)
        {
            return;
        }

        var camTransform = required.transform.Find("Main Camera");
        if (camTransform == null)
        {
            return;
        }

        camTransform.position = CameraPos;
        camTransform.rotation = Quaternion.Euler(22f, 0f, 0f);
        var cam = camTransform.GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = 60f;
            cam.farClipPlane = 400f;
            EditorUtility.SetDirty(cam);
        }

        EditorUtility.SetDirty(camTransform);
    }

    private static void MakeBanner(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, BannerColor);
    }

    private static void MakeWorldLabel(string name, string text, Vector3 pos, float width, Color color, float fontSize, TextAlignmentOptions alignment)
    {
        var holder = new GameObject(name);
        holder.transform.position = pos;
        holder.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);

        var go = new GameObject("Text");
        go.transform.SetParent(holder.transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.rectTransform.sizeDelta = new Vector2(width, 4f);
        tmp.rectTransform.localPosition = Vector3.zero;
        tmp.fontStyle = FontStyles.Bold;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Samples"))
        {
            AssetDatabase.CreateFolder("Assets", "Samples");
        }

        if (!AssetDatabase.IsValidFolder(SampleFolder))
        {
            AssetDatabase.CreateFolder("Assets/Samples", "PhysicsShowcase");
        }

        if (!AssetDatabase.IsValidFolder(TimelineFolder))
        {
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");
        }
    }

    private static void ResetAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(TimelineFolder) != null)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { TimelineFolder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        foreach (var p in new[] { ParentPath, SubPath })
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(p) != null)
            {
                AssetDatabase.DeleteAsset(p);
            }
        }
    }

    private static void Dirty(params Object[] objects)
    {
        foreach (var o in objects)
        {
            EditorUtility.SetDirty(o);
        }
    }
}
