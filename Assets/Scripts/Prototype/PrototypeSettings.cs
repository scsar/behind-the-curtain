using System;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    [Serializable]
    public sealed class PrototypeSettings
    {
        public const int TicksPerSecond = 30;
        public int AttackWaitTicks = 90;
        public int RangeMessageTicks = 30;
        public int CollapseTicks = 15;
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
        public float ConnectionHeight = 1.2f;
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
