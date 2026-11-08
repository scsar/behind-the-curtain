using System;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    [Serializable]
    public sealed class PrototypePuppetEquipment
    {
        public string[] EquipmentIds;
        public PrototypePuppetEquipment(params string[] ids) { EquipmentIds = ids; }
    }

    [Serializable]
    public sealed class PrototypeLineup
    {
        public string Name;
        // Front left, front right, rear left, rear right.
        public PrototypePuppetEquipment[] Puppets;
        public PrototypeLineup(string name, params PrototypePuppetEquipment[] puppets)
        { Name = name; Puppets = puppets; }
    }

    [Serializable]
    public sealed class PuppetTuningEntry
    {
        public string Name;
        public double Value;
        public PuppetTuningEntry(string name, double value) { Name = name; Value = value; }
    }

    [Serializable]
    public sealed class PrototypeSettings
    {
        public System.Collections.Generic.List<PuppetTuningEntry> PuppetTuning = new System.Collections.Generic.List<PuppetTuningEntry>();
        // Arbitrarily chosen presentation values only; not combat geometry or timing.
        public Vector3 MarionetteTorsoSize = new Vector3(0.9f, 0.55f, 0.9f);
        public float MarionetteTorsoHeight = 0.65f;
        public float MarionetteHeadDiameter = 0.4f;
        public Vector3 MarionetteFaceSize = new Vector3(0.3f, 0.09f, 0.05f);
        public Color MarionetteDarkColor = new Color(0.12f, 0.1f, 0.14f);
        public float MarionetteArmLength = 0.55f;
        public float MarionetteLegLength = 0.38f;
        public float MarionetteLimbWidth = 0.12f;
        public float MarionetteShoulderX = 0.48f;
        public float MarionetteHipX = 0.22f;
        public float StringHeight = 1.5f;
        public float MarionetteStringWidth = 0.008f;
        public float PoseBlendSeconds = 0.12f;
        public float IdleBobHeight = 0.03f;
        public float IdleBobFrequency = 1.5f;
        public float MarionettePhaseOffset = 1.7f;
        public float WalkFrequency = 9f;
        public float WalkReferenceSpeed = 2f;
        public float WalkLegAngle = 28f;
        public float MoveLeanAngle = 15f;
        public float WaitingDrop = 0.08f;
        public float WaitingLegAngle = 25f;
        public float AimArmAngle = -90f;
        public float LungeDistance = 0.4f;
        public float RecoverLeanAngle = 25f;
        public float RecoverArmAngle = 15f;
        public float WithdrawLeanAngle = -18f;
        public float InterceptLegAngle = 18f;
        public float InterceptArmAngle = 70f;
        public float AttackRaisedAngle = -170f;
        public float AttackSwingAngle = -55f;
        public int SwingTicks = 6;
        public Vector3 WeaponMountEuler = new Vector3(180f, 0f, 0f);
        public Vector3 ArmorMountEuler = new Vector3(90f, 0f, 0f);
        public Vector3 WeaponHandOffset = Vector3.zero;
        public Vector3 ArmorArmOffset = new Vector3(-0.1f, -0.3f, 0.2f);
        public Vector3 ToolBackOffset = new Vector3(0f, 0f, -0.5f);
        public const int TicksPerSecond = 30;
        public bool UseDirectorCasting = true;
        // Provisional, arbitrarily chosen lineup and casting/range/presentation tuning.
        public int DirectorGuardCount = 2;
        public PrototypeLineup[] Lineups = {
            new PrototypeLineup("A", new PrototypePuppetEquipment("weapon.melee", "armor.shield"),
                new PrototypePuppetEquipment("weapon.melee", "armor.shield"),
                new PrototypePuppetEquipment("weapon.melee"), new PrototypePuppetEquipment("weapon.short", "tool.mender")),
            new PrototypeLineup("B", new PrototypePuppetEquipment("weapon.melee", "armor.heavyShield"),
                new PrototypePuppetEquipment("weapon.melee"),
                new PrototypePuppetEquipment("weapon.spear"), new PrototypePuppetEquipment("weapon.short", "tool.mender")),
            new PrototypeLineup("C", new PrototypePuppetEquipment("weapon.melee"),
                new PrototypePuppetEquipment("weapon.spear"),
                new PrototypePuppetEquipment("weapon.bow"), new PrototypePuppetEquipment("weapon.short", "tool.mender"))
        };
        public float CastingSummaryWidth = 780f;
        public float CastingSummaryHeight = 112f;
        // Requested expedition defaults; cone/formation/text tuning is provisional.
        public int EncounterCycles = 3;
        public int IntermissionTicks = 2 * TicksPerSecond;
        public float MeleeAttackRadius = 3f;
        public float RangedAttackConeDegrees = 20f;
        public const int FeedbackTicksPerSecond = 60; // Presentation only; rules remain 30Hz.
        public int PuppetHitFlashTicks = 9; // 0.15s at 60Hz.
        public int AttackLabelTicks = 36; // 0.6s at 60Hz.
        public float AttackLabelRise = 0.8f;
        public float AttackLabelHeight = 1f;
        public float SpreadSpacingMultiplier = 2f;
        public float ShieldAdvanceDistance = 2f;
        public float GuardHealerOffset = 1f;
        public Color HealerColor = Color.green;
        public double ExpeditionDecay = BehindTheCurtain.Core.ExpeditionSettings.DefaultDecay;
        public int MinimumAreaHitCount = BehindTheCurtain.Core.ObserverSettings.DefaultMinimumAreaHitCount;
        public int SustainedRangedStreak = BehindTheCurtain.Core.ObserverSettings.DefaultSustainedRangedStreak;
        // Requested defaults; provisional pressure tuning values.
        public int Seed = 1;
        public int AttackCooldownTicks = 90;
        // Provisional T15 tuning; all decisions and movement are executed by Core.
        public float BrainLabelMargin = 0.3f; // Provisional presentation margin in world units.
        public float BrainLabelWidth = 340f;
        public float BrainLabelPanelHeight = 160f;
        public Color BrainGoalColor = Color.cyan;
        public int SlamWarningTicks = 24;
        public float SlamRadius = 2f;
        // Provisional pressure presentation values.
        public int SlamFlashTicks = 6;
        public int HitFlashTicks = 12;
        public int HitBlinkTicks = 3;
        public int SlamCircleSegments = 64;
        public float SlamHeight = 0.04f;
        public Color SlamWarningColor = new Color(1f, 0f, 0f, 0.3f);
        public Color SlamFlashColor = new Color(1f, 0.7f, 0.7f, 0.8f);
        public Color HitColor = Color.red;
        public int AttackWaitTicks = 90;
        public int RangeMessageTicks = 30;
        public int CollapseTicks = 15;
        // Requested defaults; provisional tuning values for the prototype.
        public int ShootAimTicks = 18;
        public int ShootCooldownTicks = 60;
        public float ShootConeDegrees = 20f; // Maximum horizontal deviation from AimDirection.
        public int ShootMessageTicks = TicksPerSecond;
        public float ShootOriginHeight = 1.0f; // Metres above the player's feet.
        public float ShootOcclusionMargin = 0.1f; // Stop before the connection centre.
        // Provisional preview tuning values.
        public float ShootPreviewScale = 1.35f;
        public Color ShootPreviewColor = new Color(1f, 1f, 1f, 0.3f);
        public Color ShootOccludedPreviewColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        public int DisruptChannelTicks = 45;
        public int CancelMessageTicks = TicksPerSecond;
        // Provisional presentation values.
        public float CounterLineWidth = 0.025f;
        public int DisruptCircleSegments = 64;
        public float DisruptCircleHeight = 0.03f;
        public float ProgressHeight = 0.3f;
        public float ProgressWidth = 80f;
        public float ProgressBarHeight = 8f;
        public float CutRange = 2.5f;
        public float DisruptRange = 6f;
        public Color CutColor = Color.red;
        public Color ShootColor = Color.blue;
        public Color DisruptColor = Color.yellow;
        public Color ThreadStartColor = Color.white;
        public Color ThreadTautColor = new Color(1f, 0.5f, 0f);

        // Provisional cycle/layout and presentation values; tune after playtesting.
        public int TelegraphTicks = 60;
        public int CounterWindowTicks = 90;
        public int MaxRequiredPoints = 3;
        public int ResultTicks = 30;
        public int ShakeTicks = 12;
        public int FlashTicks = 12;
        public int ThreadBlinkTicks = 6;
        public float ConnectionSurfaceOffset = 0.05f; // Metres outside the body face.
        public float ConnectionDiameter = 0.22f;
        public float ThreadHeight = 4f;
        public float ThreadWidth = 0.035f;
        public float ThreadSag = 0.6f;
        public float CollapseAngle = 65f;
        public float CollapseDrop = 0.25f;
        public float ShakeAngle = 8f;
        public float ShakeCycles = 2f;
        public float ScreenEdgeFraction = 0.06f;
        public Color ThreadBlinkColor = new Color(3f, 3f, 3f);
        public Color WeakenedScreenColor = new Color(1f, 0f, 0f, 0.18f);
        public Color ExecutedScreenColor = new Color(1f, 0f, 0f, 0.65f);

        public float FloorSize = 20f;
        public float MoveSpeed = 5f;
        public float CameraPitch = 50f;
        public float CameraYaw = 45f;
        public float CameraDistance = 14f;
        public float TurnSpeed = 720f;
        public float RotateSensitivity = 0.2f;
        public float MinPitch = 25f;
        public float MaxPitch = 75f;
        public float MinDistance = 8f;
        public float MaxDistance = 24f;
        public float ZoomStep = 1.5f;
        public float CameraFieldOfView = 30f;
        public float PuppetPartyDistance = 8f;

        // Provisional presentation values; tune in Play mode.
        public float CameraSmoothTime = 0.15f;
        public float PuppetSpacing = 2f;
        public float PlayerRadius = 0.5f;
        public float PlayerHeight = 2f;
        public float PuppetSize = 1f;
        public Vector3 LightEulerAngles = new Vector3(50f, -30f, 0f);
        public Color PlayerColor = Color.blue;
        public Color PuppetColor = Color.gray;
        public Color FloorColor = new Color(0.65f, 0.65f, 0.65f);
    }
}
