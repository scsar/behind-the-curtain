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
        private Vector3 partyCenter;
        private int cycleHitCount;
        private long hitFlashStart = -1;
        private Material playerMaterial;
        private Material slamWarningMaterial;
        private Material slamFlashMaterial;
        private Mesh slamMesh;

        private bool ValidPressureSettings()
            => settings.AttackCooldownTicks > 0 && settings.SlamWarningTicks > 0 &&
                settings.SlamFlashTicks > 0 && settings.HitFlashTicks > 0 && settings.HitBlinkTicks > 0 &&
                settings.SlamCircleSegments >= 3 && settings.SlamRadius > 0f &&
                !float.IsInfinity(settings.SlamRadius);

        private void InitializePressure()
        {
            foreach (var position in restPositions) partyCenter += position;
            partyCenter /= restPositions.Length;
            var forward = Vector3.ProjectOnPlane(player.position - partyCenter, Vector3.up);
            var facing = forward.sqrMagnitude > 0f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            for (var i = 0; i < puppets.Length; i++)
            {
                restRotations[i] = facing;
                puppets[i].rotation = facing;
            }
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
            cycleHitCount = 0;
        }

        private void AdvancePressure(long tick)
        {
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

        private void CreateSlam(long tick, Vec2 target)
        {
            var center = new Vector3((float)target.X, settings.SlamHeight, (float)target.Z);
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
