using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Rooms.UI
{
    /// <summary>
    /// A name and an HP number under every enemy on the battle stage.
    ///
    /// <para>Until 2026-09-28 an enemy had only its thin world-space bar (<see cref="UnitHealthBar"/>):
    /// no name, no number, and at 1 HP a sliver the eye could not find (playtest finding 9). The bar
    /// stays - it is the at-a-glance read - and this adds the two facts it cannot carry.</para>
    ///
    /// <para>UI Toolkit rather than world-space text because world text here is the legacy
    /// <c>TextMesh</c>, which blurs at small sizes; a UITK label is crisp, uses the theme font and
    /// scales with the panel. Each plate is placed every frame by projecting the enemy's feet into
    /// panel space, so it follows the lunge and the formation for free. Like the turn-order and party
    /// rows these are marks built from data; the host element is authored in the UXML.</para>
    ///
    /// <para>Pure view: it reads the live enemies off <see cref="CombatManager"/> and owns no state
    /// beyond the plates themselves.</para>
    /// </summary>
    public sealed class EnemyNameplates : VisualElement
    {
        /// <summary>Panel pixels between the bottom of the enemy's sprite and the top of its plate.</summary>
        private const float FeetGap = 2f;

        private sealed class Plate
        {
            public VisualElement Root;
            public Label Name;
            public Label Hp;
            public string LastHpText;
        }

        private readonly Dictionary<ICombatUnit, Plate> _plates = new Dictionary<ICombatUnit, Plate>();
        private readonly List<ICombatUnit> _stale = new List<ICombatUnit>();

        public EnemyNameplates()
        {
            AddToClassList("cd-nameplates");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
        }

        /// <summary>Call once a frame. Shows a plate per living enemy while a fight is on, none otherwise.</summary>
        public void Tick(Camera camera)
        {
            bool inCombat = CombatManager.HasInstance && CombatManager.Instance.InCombat;
            if (!inCombat || camera == null || panel == null)
            {
                ClearAll();
                return;
            }

            var alive = CombatManager.Instance.GetAliveEnemies();

            _stale.Clear();
            foreach (var unit in _plates.Keys)
            {
                if (!alive.Contains(unit))
                {
                    _stale.Add(unit);
                }
            }
            foreach (var unit in _stale)
            {
                Remove(unit);
            }

            foreach (var unit in alive)
            {
                if (unit == null || unit.Transform == null)
                {
                    continue;
                }

                if (!_plates.TryGetValue(unit, out var plate))
                {
                    plate = Build(unit);
                    _plates[unit] = plate;
                }

                UpdateHp(unit, plate);
                Place(unit, plate, camera);
            }
        }

        /// <summary>Removes every plate - combat over, or the layer is being torn down.</summary>
        public void ClearAll()
        {
            if (_plates.Count == 0)
            {
                return;
            }
            foreach (var plate in _plates.Values)
            {
                plate.Root.RemoveFromHierarchy();
            }
            _plates.Clear();
        }

        private Plate Build(ICombatUnit unit)
        {
            var root = new VisualElement();
            root.AddToClassList("cd-nameplate");
            root.pickingMode = PickingMode.Ignore;
            // Centred on its anchor point: the plate's own width is unknown until layout.
            root.style.position = Position.Absolute;
            root.style.translate = new Translate(Length.Percent(-50), 0);

            var enemy = unit as Enemy;
            if (enemy != null && enemy.IsBoss)
            {
                root.AddToClassList("cd-nameplate--boss");
            }

            var name = new Label(unit.DisplayName);
            name.AddToClassList("cd-nameplate__name");
            name.pickingMode = PickingMode.Ignore;

            var hp = new Label();
            hp.AddToClassList("cd-nameplate__hp");
            hp.pickingMode = PickingMode.Ignore;

            root.Add(name);
            root.Add(hp);
            Add(root);
            return new Plate { Root = root, Name = name, Hp = hp };
        }

        private static void UpdateHp(ICombatUnit unit, Plate plate)
        {
            int max = Mathf.Max(1, unit.GetEffectiveStat(StatType.MaxHealth));
            int current = Mathf.Max(0, unit.Stats.Health);
            string text = $"{current}/{max}";
            if (text == plate.LastHpText)
            {
                return;
            }
            plate.LastHpText = text;
            plate.Hp.text = text;
            // Low health gets its own colour, so the "one more hit" read does not depend on finding the bar.
            plate.Hp.EnableInClassList("cd-nameplate__hp--low", current * 4 <= max);
        }

        private void Place(ICombatUnit unit, Plate plate, Camera camera)
        {
            var sr = unit.Transform.GetComponent<SpriteRenderer>();
            Vector3 feet = sr != null
                ? new Vector3(sr.bounds.center.x, sr.bounds.min.y, unit.Transform.position.z)
                : unit.Transform.position;

            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, feet, camera);
            plate.Root.style.left = p.x;
            plate.Root.style.top = p.y + FeetGap;
        }

        private void Remove(ICombatUnit unit)
        {
            if (_plates.TryGetValue(unit, out var plate))
            {
                plate.Root.RemoveFromHierarchy();
                _plates.Remove(unit);
            }
        }
    }
}
