using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BehindTheCurtain.Prototype
{
    /// <summary>Primitive presentation only; equipment capabilities and costs belong to Core.</summary>
    public sealed class PrototypeEquipmentView : MonoBehaviour
    {
        // Provisional proportions and colors for silhouette checks, not gameplay ranges.
        private static readonly Color Metal = new Color(0.65f, 0.72f, 0.8f);
        private static readonly Color Wood = new Color(0.32f, 0.18f, 0.09f);
        private static readonly Color Brass = new Color(0.85f, 0.58f, 0.18f);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<GameObject> models = new List<GameObject>();
        private string equipmentKey;
        private string previewLabel;

        public void SetEquipment(IEnumerable<string> ids, bool preview = false)
        {
            var items = new List<string>(ids);
            var key = string.Join("|", items) + (preview ? "|preview" : "|mounted");
            if (equipmentKey == key) return;
            equipmentKey = key;
            foreach (var model in models)
            {
                model.SetActive(false);
                Destroy(model);
            }
            models.Clear();
            ReleaseMaterials();
            previewLabel = preview ? string.Join(" / ", items) : null;
            foreach (var id in items)
            {
                var root = new GameObject(id);
                root.transform.SetParent(transform, false);
                models.Add(root);
                Build(root.transform, id);
                if (preview) continue;
                // Attachments follow the existing cube puppet's forward (+Z).
                root.transform.localPosition = id.StartsWith("weapon.") ? new Vector3(0.65f, -0.15f, 0.3f)
                    : id.StartsWith("armor.") ? new Vector3(-0.6f, 0f, 0.4f)
                    : new Vector3(0.5f, 0.1f, -0.6f);
                root.transform.localScale = Vector3.one * 0.65f;
            }
        }

        public static void CreateGallery(Transform parent)
        {
            var gallery = new GameObject("Equipment Gallery (visual samples, not equipped)");
            gallery.transform.SetParent(parent, false);
            var ids = new[] { "weapon.melee", "weapon.short", "weapon.spear", "weapon.bow",
                "armor.shield", "armor.heavyShield", "tool.mender", "tool.jammer" };
            for (var i = 0; i < ids.Length; i++)
            {
                var stand = new GameObject("Preview " + ids[i]);
                stand.transform.SetParent(gallery.transform, false);
                stand.transform.localPosition = new Vector3(-6f + (i % 4) * 4f, 1f, -7f + (i / 4) * 3f);
                stand.AddComponent<PrototypeEquipmentView>().SetEquipment(new[] { ids[i] }, true);
            }
        }

        private void Build(Transform root, string id)
        {
            switch (id)
            {
                case "weapon.melee":
                case "weapon.short":
                    var length = id == "weapon.short" ? 0.6f : 1.2f;
                    Part(root, "Grip", PrimitiveType.Cylinder, new Vector3(0, -0.2f, 0), new Vector3(0.09f, 0.2f, 0.09f), Wood);
                    Part(root, "Crossguard", PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.09f, 0.15f), Brass);
                    Part(root, "Blade", PrimitiveType.Cube, new Vector3(0, length / 2f, 0), new Vector3(0.16f, length, 0.06f), Metal);
                    break;
                case "weapon.spear":
                    Part(root, "Shaft", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.8f, 0.07f), Wood);
                    Part(root, "Spearhead", PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(0.18f, 0.4f, 0.06f), Metal, new Vector3(0, 0, 45));
                    Part(root, "Collar", PrimitiveType.Cylinder, new Vector3(0, 0.75f, 0), new Vector3(0.13f, 0.1f, 0.13f), Brass);
                    break;
                case "weapon.bow":
                    // Segmented limbs and a thin primitive string; no custom meshes required.
                    Segment(root, "Upper limb", new Vector3(0.3f, 0.8f, 0), new Vector3(-0.15f, 0.3f, 0), 0.09f, Wood);
                    Segment(root, "Grip", new Vector3(-0.15f, 0.3f, 0), new Vector3(-0.15f, -0.3f, 0), 0.12f, Wood);
                    Segment(root, "Lower limb", new Vector3(-0.15f, -0.3f, 0), new Vector3(0.3f, -0.8f, 0), 0.09f, Wood);
                    Segment(root, "Bowstring", new Vector3(0.3f, -0.8f, 0), new Vector3(0.3f, 0.8f, 0), 0.025f, Metal);
                    Segment(root, "Arrow", new Vector3(-0.5f, 0, 0.08f), new Vector3(0.6f, 0, 0.08f), 0.035f, Brass);
                    break;
                case "armor.shield":
                    Part(root, "Round shield", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.9f, 0.09f, 0.9f), Metal, new Vector3(90, 0, 0));
                    Part(root, "Boss", PrimitiveType.Sphere, new Vector3(0, 0, 0.13f), Vector3.one * 0.25f, Brass);
                    break;
                case "armor.heavyShield":
                    Part(root, "Tower shield", PrimitiveType.Cube, Vector3.zero, new Vector3(0.85f, 1.5f, 0.22f), Metal);
                    Part(root, "Vertical brace", PrimitiveType.Cube, new Vector3(0, 0, 0.14f), new Vector3(0.12f, 1.55f, 0.08f), Brass);
                    Part(root, "Cross brace", PrimitiveType.Cube, new Vector3(0, 0, 0.14f), new Vector3(0.9f, 0.12f, 0.08f), Brass);
                    break;
                case "tool.mender":
                    Part(root, "Repair case", PrimitiveType.Cube, Vector3.zero, new Vector3(0.65f, 0.55f, 0.3f), new Color(0.18f, 0.55f, 0.35f));
                    Part(root, "Spool", PrimitiveType.Cylinder, new Vector3(0, 0.38f, 0), new Vector3(0.3f, 0.15f, 0.3f), Brass, new Vector3(90, 0, 0));
                    Part(root, "Cross vertical", PrimitiveType.Cube, new Vector3(0, 0, 0.18f), new Vector3(0.08f, 0.3f, 0.04f), Color.white);
                    Part(root, "Cross horizontal", PrimitiveType.Cube, new Vector3(0, 0, 0.18f), new Vector3(0.3f, 0.08f, 0.04f), Color.white);
                    break;
                case "tool.jammer":
                    Part(root, "Signal case", PrimitiveType.Cube, Vector3.zero, new Vector3(0.45f, 0.55f, 0.3f), new Color(0.4f, 0.2f, 0.65f));
                    Part(root, "Emitter", PrimitiveType.Sphere, new Vector3(0, 0.35f, 0), Vector3.one * 0.3f, Brass);
                    Part(root, "Left prong", PrimitiveType.Cube, new Vector3(-0.18f, 0.6f, 0), new Vector3(0.06f, 0.5f, 0.06f), Metal);
                    Part(root, "Right prong", PrimitiveType.Cube, new Vector3(0.18f, 0.6f, 0), new Vector3(0.06f, 0.5f, 0.06f), Metal);
                    break;
                default:
                    Debug.LogWarning("No primitive equipment model for " + id, this);
                    break;
            }
        }

        private void Segment(Transform root, string name, Vector3 from, Vector3 to, float width, Color color)
        {
            var part = Part(root, name, PrimitiveType.Cylinder, (from + to) / 2f,
                new Vector3(width, Vector3.Distance(from, to) / 2f, width), color);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }

        private Transform Part(Transform root, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Color color, Vector3 euler = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(euler);
            // Decorative geometry must not affect Shoot occlusion or combat raycasts.
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = part.GetComponent<Renderer>();
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var material = new Material(pipeline != null ? pipeline.defaultMaterial : renderer.sharedMaterial);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
            materials.Add(material);
            return part.transform;
        }

        private void OnGUI()
        {
            if (previewLabel == null || Camera.main == null) return;
            var screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screen.z > 0)
                GUI.Box(new Rect(screen.x - 70f, Screen.height - screen.y, 140f, 24f), previewLabel);
        }

        private void ReleaseMaterials()
        {
            foreach (var material in materials) Destroy(material);
            materials.Clear();
        }

        private void OnDestroy() => ReleaseMaterials();
    }
}
