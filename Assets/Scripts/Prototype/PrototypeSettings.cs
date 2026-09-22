using System;
using UnityEngine;

namespace BehindTheCurtain.Prototype
{
    [Serializable]
    public sealed class PrototypeSettings
    {
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
