using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public enum EquipmentSlot { Weapon, Armor, StageTool }
    public enum RangeBand { Melee, Mid, Ranged }
    public enum CapabilityTag { Block, Protect, Heal, Disrupt }

    public sealed class EquipmentCost
    {
        public EquipmentCost(double moveSpeedScale = 1, bool immobileWhileUsing = false)
        { MoveSpeedScale = moveSpeedScale; ImmobileWhileUsing = immobileWhileUsing; }
        public double MoveSpeedScale { get; }
        // Applied by ability execution in T16b-3, not by movement yet.
        public bool ImmobileWhileUsing { get; }
        public bool IsValid => AiNumber.Finite(MoveSpeedScale) && MoveSpeedScale > 0 && MoveSpeedScale <= 1;
    }

    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(string id, EquipmentSlot slot, RangeBand? rangeBand = null,
            IEnumerable<CapabilityTag>? capabilities = null, EquipmentCost? cost = null)
        {
            Id = id; Slot = slot; RangeBand = rangeBand; Cost = cost ?? new EquipmentCost();
            Capabilities = new List<CapabilityTag>(capabilities ?? Array.Empty<CapabilityTag>()).AsReadOnly();
        }
        public string Id { get; }
        public EquipmentSlot Slot { get; }
        public RangeBand? RangeBand { get; }
        public IReadOnlyList<CapabilityTag> Capabilities { get; }
        public EquipmentCost Cost { get; }
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(typeof(EquipmentSlot), Slot) || !Cost.IsValid) return false;
                if (Slot == EquipmentSlot.Weapon ? !RangeBand.HasValue || !Enum.IsDefined(typeof(RangeBand), RangeBand.Value) : RangeBand.HasValue) return false;
                foreach (var tag in Capabilities) if (!Enum.IsDefined(typeof(CapabilityTag), tag)) return false;
                return true;
            }
        }
    }

    public sealed class EquipmentCatalog
    {
        private readonly Dictionary<string, EquipmentDefinition> definitions;
        private EquipmentCatalog(Dictionary<string, EquipmentDefinition> definitions) { this.definitions = definitions; }
        public static AiResult<EquipmentCatalog> Create(IEnumerable<EquipmentDefinition>? definitions)
        {
            if (definitions == null) return AiResult<EquipmentCatalog>.Fail(AiFailure.InvalidValue);
            var copy = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
            foreach (var item in definitions)
            {
                if (item == null || !item.IsValid || copy.ContainsKey(item.Id)) return AiResult<EquipmentCatalog>.Fail(AiFailure.InvalidValue);
                copy.Add(item.Id, item);
            }
            return AiResult<EquipmentCatalog>.Ok(new EquipmentCatalog(copy));
        }
        public AiResult<EquipmentDefinition> Get(string id) => id != null && definitions.TryGetValue(id, out var item)
            ? AiResult<EquipmentDefinition>.Ok(item) : AiResult<EquipmentDefinition>.Fail(AiFailure.InvalidId);
        // Provisional, arbitrarily chosen prototype equipment costs.
        public const double HeavyShieldMoveSpeedScale = 0.8;
        public static EquipmentCatalog Default { get; } = Create(new[] {
            new EquipmentDefinition("weapon.melee", EquipmentSlot.Weapon, global::BehindTheCurtain.Core.RangeBand.Melee),
            new EquipmentDefinition("weapon.short", EquipmentSlot.Weapon, global::BehindTheCurtain.Core.RangeBand.Melee),
            new EquipmentDefinition("weapon.spear", EquipmentSlot.Weapon, global::BehindTheCurtain.Core.RangeBand.Mid),
            new EquipmentDefinition("weapon.bow", EquipmentSlot.Weapon, global::BehindTheCurtain.Core.RangeBand.Ranged),
            new EquipmentDefinition("armor.shield", EquipmentSlot.Armor, capabilities: new[] { CapabilityTag.Block, CapabilityTag.Protect }),
            new EquipmentDefinition("armor.heavyShield", EquipmentSlot.Armor, capabilities: new[] { CapabilityTag.Block, CapabilityTag.Protect }, cost: new EquipmentCost(HeavyShieldMoveSpeedScale)),
            new EquipmentDefinition("tool.mender", EquipmentSlot.StageTool, capabilities: new[] { CapabilityTag.Heal }, cost: new EquipmentCost(immobileWhileUsing: true)),
            new EquipmentDefinition("tool.jammer", EquipmentSlot.StageTool, capabilities: new[] { CapabilityTag.Disrupt }, cost: new EquipmentCost(immobileWhileUsing: true))
        }).Value;
    }

    public sealed class Loadout
    {
        public const int MaxCapabilities = 3;
        private Loadout(List<EquipmentDefinition> equipment, List<CapabilityTag> tags, RangeBand band, double speed)
        {
            Equipment = equipment.AsReadOnly(); Capabilities = tags.AsReadOnly(); RangeBand = band; MoveSpeedScale = speed;
            var ids = new List<string>(); foreach (var item in equipment) ids.Add(item.Id); EquipmentIds = ids.AsReadOnly();
        }
        public IReadOnlyList<EquipmentDefinition> Equipment { get; }
        public IReadOnlyList<string> EquipmentIds { get; }
        public IReadOnlyList<CapabilityTag> Capabilities { get; }
        public RangeBand RangeBand { get; }
        public double MoveSpeedScale { get; }
        public bool HasCapability(CapabilityTag tag) { foreach (var value in Capabilities) if (value == tag) return true; return false; }
        public double GetAttackRange(PuppetSettings settings) => RangeBand == global::BehindTheCurtain.Core.RangeBand.Mid ? settings.MidAttackRange
            : RangeBand == global::BehindTheCurtain.Core.RangeBand.Ranged ? settings.RangedAttackRange : settings.AttackRange;
        public static AiResult<Loadout> Create(IEnumerable<string>? ids, EquipmentCatalog? catalog = null)
        {
            if (ids == null) return AiResult<Loadout>.Fail(AiFailure.InvalidValue);
            catalog = catalog ?? EquipmentCatalog.Default;
            var equipment = new List<EquipmentDefinition>(); var tags = new HashSet<CapabilityTag>(); var slots = new HashSet<EquipmentSlot>();
            RangeBand band = global::BehindTheCurtain.Core.RangeBand.Melee; double speed = 1;
            foreach (var id in ids)
            {
                var found = catalog.Get(id);
                if (!found.Success) return AiResult<Loadout>.Fail(found.Failure);
                var item = found.Value;
                if (!item.IsValid || !slots.Add(item.Slot)) return AiResult<Loadout>.Fail(AiFailure.InvalidValue);
                equipment.Add(item); foreach (var tag in item.Capabilities) tags.Add(tag);
                if (tags.Count > MaxCapabilities) return AiResult<Loadout>.Fail(AiFailure.InvalidValue);
                if (item.Slot == EquipmentSlot.Weapon) band = item.RangeBand!.Value;
                speed *= item.Cost.MoveSpeedScale;
            }
            if (!slots.Contains(EquipmentSlot.Weapon) || speed <= 0) return AiResult<Loadout>.Fail(AiFailure.InvalidValue);
            equipment.Sort((a, b) => a.Slot.CompareTo(b.Slot)); var orderedTags = new List<CapabilityTag>(tags); orderedTags.Sort();
            return AiResult<Loadout>.Ok(new Loadout(equipment, orderedTags, band, speed));
        }
    }

    public static class LoadoutPresets
    {
        public static Loadout Shield { get; } = Loadout.Create(new[] { "weapon.melee", "armor.shield" }).Value;
        public static Loadout Melee { get; } = Loadout.Create(new[] { "weapon.melee" }).Value;
        public static Loadout Mender { get; } = Loadout.Create(new[] { "weapon.short", "tool.mender" }).Value;
        public static Loadout ForRole(TargetRole role) => role == TargetRole.Guard ? Shield : role == TargetRole.Healer ? Mender : Melee;
    }
}
