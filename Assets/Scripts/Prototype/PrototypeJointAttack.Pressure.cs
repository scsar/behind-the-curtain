using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private sealed class Slam
        {
            public GameObject View;
            public Renderer Renderer;
            public Vector3 Center;
            public long ImpactTick;
            public bool Impacted;
        }

        private readonly List<Slam> slams = new List<Slam>();
        private System.Random pressureRandom;
        private Vector3 partyCenter;
        private int orbitDirection;
        private long nextSlamTick;
        private int cycleHitCount;
        private long hitFlashStart = -1;
        private Material playerMaterial;
        private Material slamWarningMaterial;
        private Material slamFlashMaterial;
        private Mesh slamMesh;

        private bool ValidPressureSettings()
            => settings.SlamIntervalTicks > 0 && settings.SlamWarningTicks > 0 &&
                settings.SlamFlashTicks > 0 && settings.HitFlashTicks > 0 && settings.HitBlinkTicks > 0 &&
                settings.SlamCircleSegments >= 3 && settings.SlamRadius > 0f &&
                !float.IsInfinity(settings.SlamRadius) && settings.PartyOrbitSpeed >= 0f &&
                !float.IsInfinity(settings.PartyOrbitSpeed);

        private void InitializePressure()
        {
            pressureRandom = new System.Random(settings.Seed);
            foreach (var position in restPositions) partyCenter += position;
            partyCenter /= restPositions.Length;
            playerMaterial = player.GetComponent<Renderer>().sharedMaterial;
            slamWarningMaterial = CreateUnlitMaterial(settings.SlamWarningColor);
            ConfigureTransparentPreview(slamWarningMaterial);
            SetMaterialColor(slamWarningMaterial, settings.SlamWarningColor);
            slamFlashMaterial = CreateUnlitMaterial(settings.SlamFlashColor);
            ConfigureTransparentPreview(slamFlashMaterial);
            SetMaterialColor(slamFlashMaterial, settings.SlamFlashColor);

            // A filled horizontal disc; no collider to interfere with Shoot raycasts.
            var segments = settings.SlamCircleSegments;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * settings.SlamRadius;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            slamMesh = new Mesh { name = "Slam Disc", vertices = vertices, triangles = triangles };
            slamMesh.RecalculateNormals();
            slamMesh.RecalculateBounds();
        }

        private void StartPressureCycle()
        {
            orbitDirection = pressureRandom.Next(2) == 0 ? -1 : 1;
            cycleHitCount = 0;
            nextSlamTick = attack.CounterWindow.Start;
        }

        private void AdvancePressure(long tick)
        {
            // Core owns attack phases and the inclusive counter window.
            if (attack != null && (attack.State == JointAttackState.Telegraphing || WindowOpen))
                RotateParty();
            if (WindowOpen && tick >= nextSlamTick)
            {
                CreateSlam(tick);
                nextSlamTick = tick + settings.SlamIntervalTicks;
            }
            for (var i = slams.Count - 1; i >= 0; i--)
            {
                var slam = slams[i];
                if (!slam.Impacted && tick >= slam.ImpactTick)
                {
                    slam.Impacted = true;
                    slam.Renderer.sharedMaterial = slamFlashMaterial;
                    // Spatial sampling is a prototype adapter, as with Cut/Disrupt distance checks.
                    var offset = Vector3.ProjectOnPlane(player.position - slam.Center, Vector3.up);
                    if (offset.sqrMagnitude <= settings.SlamRadius * settings.SlamRadius)
                    {
                        cycleHitCount++;
                        hitFlashStart = tick;
                        CancelAction("피격");
                    }
                }
                if (tick < slam.ImpactTick + settings.SlamFlashTicks) continue;
                Destroy(slam.View);
                slams.RemoveAt(i);
            }
        }

        private void RotateParty()
        {
            var rotation = Quaternion.Euler(0f,
                orbitDirection * settings.PartyOrbitSpeed / PrototypeSettings.TicksPerSecond, 0f);
            var limit = settings.FloorSize / 2f - settings.PuppetSize / 2f;
            // Reject the whole step at the edge, preserving the party's spacing and centre.
            foreach (var position in restPositions)
            {
                var rotated = partyCenter + rotation * (position - partyCenter);
                if (Mathf.Abs(rotated.x) > limit || Mathf.Abs(rotated.z) > limit) return;
            }
            for (var i = 0; i < puppets.Length; i++)
            {
                restPositions[i] = partyCenter + rotation * (restPositions[i] - partyCenter);
                puppets[i].position = restPositions[i];
                puppets[i].rotation = restRotations[i];
            }
            // Raycasts in this tick must see the moving bodies, including catch-up ticks.
            Physics.SyncTransforms();
        }

        private void CreateSlam(long tick)
        {
            var center = new Vector3(player.position.x, settings.SlamHeight, player.position.z);
            var view = new GameObject("Slam Warning");
            view.transform.SetParent(transform, false);
            view.transform.position = center;
            view.AddComponent<MeshFilter>().sharedMesh = slamMesh;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = slamWarningMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            slams.Add(new Slam { View = view, Renderer = renderer, Center = center,
                ImpactTick = tick + settings.SlamWarningTicks });
        }

        private void UpdateHitPresentation()
        {
            var elapsed = clock.Tick - hitFlashStart;
            var red = hitFlashStart >= 0 && elapsed < settings.HitFlashTicks &&
                elapsed / settings.HitBlinkTicks % 2 == 0;
            SetMaterialColor(playerMaterial, red ? settings.HitColor : settings.PlayerColor);
        }
    }
}
