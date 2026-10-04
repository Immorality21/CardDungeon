using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Cards;
using Assets.Scripts.UnitStats;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Heroes.Editor
{
    /// <summary>
    /// One place to define what each hero <i>is</i>: the player-facing text (<see cref="HeroSO.Label"/>,
    /// <see cref="HeroSO.Blurb"/>) and the developer-facing
    /// <see cref="HeroVision"/>, edited on the hero asset itself so the intent lives beside the stats
    /// and the grid it is meant to explain.
    ///
    /// <para>Below the editable fields, every branch of the hero's sphere grid is shown twice: what the
    /// vision says it should be, and what the grid actually grants today (abilities with their scaling
    /// stat and the XP it takes to reach them, summons, stat and resistance totals). That side-by-side
    /// is the point — a grid that has drifted from its hero, or a hero with no vision at all, is
    /// visible at a glance.</para>
    ///
    /// <para>Branches are recovered from the node-key convention (<c>hero-a-3</c> is branch a); a node
    /// whose key has no branch letter (a summon such as <c>warrior-boar</c>) belongs to the branch of
    /// the nearest lettered node on its shortest path from the start. Anything before the fork is the
    /// trunk.</para>
    /// </summary>
    public class HeroVisionWindow : EditorWindow
    {
        private const string RosterPath = "Assets/ScriptableObjects/PartyRoster.asset";
        private const string Trunk = "trunk";

        private readonly List<HeroSO> _heroes = new List<HeroSO>();
        private HeroSO _hero;
        private SerializedObject _serializedHero;

        private ListView _list;
        private ScrollView _detail;
        private VisualElement _gridToday;

        private Dictionary<string, MagicSO> _magicByKey;
        private Dictionary<string, SummonSO> _summonByKey;

        [MenuItem("Tools/Heroes/Hero Vision")]
        public static void Open()
        {
            var window = GetWindow<HeroVisionWindow>("Hero Vision");
            window.minSize = new Vector2(820f, 480f);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(Reload) { text = "Reload" });
            toolbar.Add(new ToolbarButton(AssetDatabase.SaveAssets) { text = "Save Assets" });
            var hint = new Label("Edits are stored on each HeroSO. The Vision fields are never shown to the player.");
            hint.style.unityTextAlign = TextAnchor.MiddleLeft;
            hint.style.marginLeft = 8f;
            hint.style.opacity = 0.7f;
            toolbar.Add(hint);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            root.Add(split);

            _list = new ListView
            {
                itemsSource = _heroes,
                fixedItemHeight = 40f,
                selectionType = SelectionType.Single,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
            _list.selectionChanged += selection => Select(selection.FirstOrDefault() as HeroSO);
            split.Add(_list);

            _detail = new ScrollView(ScrollViewMode.Vertical);
            _detail.style.paddingLeft = 10f;
            _detail.style.paddingRight = 10f;
            _detail.style.paddingTop = 6f;
            split.Add(_detail);

            Reload();
        }

        private void Reload()
        {
            _magicByKey = LoadByKey<MagicSO>(m => m.Key);
            _summonByKey = LoadByKey<SummonSO>(s => s.Key);

            _heroes.Clear();
            var roster = AssetDatabase.LoadAssetAtPath<PartyRosterSO>(RosterPath);
            if (roster != null)
            {
                _heroes.AddRange(roster.Heroes.Where(h => h != null));
            }
            // Heroes that exist but are not on the roster still deserve a vision; list them after it.
            foreach (var hero in LoadAll<HeroSO>())
            {
                if (!_heroes.Contains(hero))
                {
                    _heroes.Add(hero);
                }
            }

            _list.Rebuild();
            int index = _hero != null ? _heroes.IndexOf(_hero) : 0;
            if (_heroes.Count > 0)
            {
                _list.SetSelection(Mathf.Max(0, index));
            }
            else
            {
                Select(null);
            }
        }

        // --- the hero list ---------------------------------------------------------------------------

        private static VisualElement MakeListItem()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 32f;
            icon.style.height = 32f;
            icon.style.marginLeft = 4f;
            icon.style.marginRight = 6f;
            row.Add(icon);
            var text = new VisualElement();
            text.Add(new Label { name = "name", style = { unityFontStyleAndWeight = FontStyle.Bold } });
            text.Add(new Label { name = "progress", style = { opacity = 0.6f, fontSize = 10f } });
            row.Add(text);
            return row;
        }

        private void BindListItem(VisualElement row, int index)
        {
            var hero = _heroes[index];
            row.Q<Image>("icon").sprite = hero.Sprite;
            row.Q<Label>("name").text = hero.DisplayName;
            RefreshProgress(row, hero);
        }

        private static void RefreshProgress(VisualElement row, HeroSO hero)
        {
            int filled = FilledFields(hero, out int total);
            row.Q<Label>("progress").text = filled == total ? "defined" : $"{filled}/{total} defined";
        }

        /// <summary>How much of a hero's definition has been written, for the list's progress line.</summary>
        private static int FilledFields(HeroSO hero, out int total)
        {
            var vision = hero.Vision ?? new HeroVision();
            var fields = new[]
            {
                hero.Blurb, vision.Fantasy, vision.Role, vision.SignatureMechanic, vision.UltraStyle,
                vision.Overlaps
            };
            total = fields.Length + 1;
            int filled = fields.Count(f => !string.IsNullOrWhiteSpace(f));
            if (vision.Branches != null && vision.Branches.Any(b => !string.IsNullOrWhiteSpace(b.Becomes)))
            {
                filled++;
            }
            return filled;
        }

        // --- the detail pane -------------------------------------------------------------------------

        private void Select(HeroSO hero)
        {
            _hero = hero;
            _detail.Clear();
            _detail.Unbind();
            if (hero == null)
            {
                _detail.Add(new HelpBox("No HeroSO assets found.", HelpBoxMessageType.Info));
                return;
            }

            if (hero.Vision == null)
            {
                hero.Vision = new HeroVision();
                EditorUtility.SetDirty(hero);
            }
            _serializedHero = new SerializedObject(hero);

            _detail.Add(BuildHeader(hero));

            var player = Section("Player-facing");
            player.Add(Field("Label"));
            player.Add(Field("Blurb"));
            _detail.Add(player);

            var vision = Section("Vision — developer-facing, never shown");
            vision.Add(Field("Vision.Fantasy"));
            vision.Add(Field("Vision.Role"));
            vision.Add(Field("Vision.SignatureMechanic"));
            vision.Add(Field("Vision.UltraStyle"));
            vision.Add(Field("Vision.Ultras"));
            vision.Add(Field("Vision.Branches"));
            vision.Add(Field("Vision.Overlaps"));
            vision.Add(Field("Vision.OpenQuestions"));
            _detail.Add(vision);

            _gridToday = Section("Branches — intended vs. on the grid today");
            _detail.Add(_gridToday);
            RebuildGridToday();

            _detail.Bind(_serializedHero);
            _detail.TrackSerializedObjectValue(_serializedHero, _ =>
            {
                RebuildGridToday();
                _list.RefreshItems();
            });
        }

        private VisualElement BuildHeader(HeroSO hero)
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6f } };

            var portrait = new Image { sprite = hero.Sprite, scaleMode = ScaleMode.ScaleToFit };
            portrait.style.width = 96f;
            portrait.style.height = 96f;
            portrait.style.marginRight = 10f;
            header.Add(portrait);

            var info = new VisualElement { style = { flexGrow = 1f } };
            info.Add(new Label(hero.DisplayName) { style = { fontSize = 18f, unityFontStyleAndWeight = FontStyle.Bold } });
            info.Add(Dim($"key {hero.SaveKey} · attacks with {StatCatalog.DisplayName(hero.ResolvedAttackStat)}"));
            info.Add(Dim("base " + StatLine(hero.BaseStats)));
            if (hero.SphereGrid != null)
            {
                info.Add(Dim($"grid {hero.SphereGrid.name} · {hero.SphereGrid.Nodes.Count} nodes · " +
                             $"{SphereGridOps.TotalGridCost(hero.SphereGrid)} xp in all"));
            }

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4f } };
            buttons.Add(new Button(() => { Selection.activeObject = hero; EditorGUIUtility.PingObject(hero); }) { text = "Select asset" });
            if (hero.SphereGrid != null)
            {
                buttons.Add(new Button(() =>
                {
                    var window = GetWindow<SphereGridEditorWindow>("Sphere Grid Editor");
                    window.SetTarget(hero.SphereGrid);
                }) { text = "Open Sphere Grid" });
            }
            info.Add(buttons);
            header.Add(info);
            return header;
        }

        private void RebuildGridToday()
        {
            if (_gridToday == null || _hero == null)
            {
                return;
            }
            // Keep the section's title, drop the rest.
            while (_gridToday.childCount > 1)
            {
                _gridToday.RemoveAt(1);
            }

            var grid = _hero.SphereGrid;
            if (grid == null)
            {
                _gridToday.Add(new HelpBox("This hero has no sphere grid.", HelpBoxMessageType.Warning));
                return;
            }

            var branches = GroupByBranch(grid);
            var intended = _hero.Vision?.Branches ?? new List<BranchVision>();

            var letters = branches.Keys.Where(k => k != Trunk).OrderBy(k => k).ToList();
            if (branches.ContainsKey(Trunk))
            {
                letters.Insert(0, Trunk);
            }
            foreach (var described in intended)
            {
                string letter = Normalize(described.GridBranch);
                if (!string.IsNullOrEmpty(letter) && !letters.Contains(letter))
                {
                    letters.Add(letter);
                }
            }

            foreach (var letter in letters)
            {
                branches.TryGetValue(letter, out var nodes);
                var vision = intended.FirstOrDefault(b => Normalize(b.GridBranch) == letter);
                _gridToday.Add(BranchRow(grid, letter, vision, nodes ?? new List<SphereGridNode>()));
            }

            var undescribed = intended.Where(b => string.IsNullOrEmpty(Normalize(b.GridBranch))).ToList();
            if (undescribed.Count > 0)
            {
                _gridToday.Add(new HelpBox($"{undescribed.Count} branch vision(s) have no Grid Branch letter, " +
                                           "so they cannot be matched to the grid.", HelpBoxMessageType.Info));
            }
        }

        private VisualElement BranchRow(SphereGridSO grid, string letter, BranchVision vision, List<SphereGridNode> nodes)
        {
            var box = new VisualElement();
            box.style.marginTop = 6f;
            box.style.paddingLeft = 6f;
            box.style.paddingRight = 6f;
            box.style.paddingTop = 4f;
            box.style.paddingBottom = 4f;
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 1f;
            var border = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = border;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4f;

            string title = letter == Trunk ? "Trunk (before the fork)" : $"Branch {letter.ToUpperInvariant()}";
            box.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2f } });

            var columns = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            box.Add(columns);

            var left = Column("Intended");
            if (letter == Trunk)
            {
                left.Add(Dim("The shared start every build walks through."));
            }
            else if (vision == null)
            {
                left.Add(Dim("No vision for this branch yet — add a Branch with Grid Branch \"" + letter + "\"."));
            }
            else
            {
                AddText(left, "Becomes", vision.Becomes);
                AddText(left, "Kit", vision.IntendedKit);
                AddText(left, "Summon", vision.Summon);
            }
            columns.Add(left);

            var right = Column("On the grid today");
            if (nodes.Count == 0)
            {
                right.Add(Dim("No nodes on the grid carry this branch letter."));
            }
            else
            {
                foreach (var line in GrantLines(grid, nodes))
                {
                    right.Add(new Label(line) { style = { whiteSpace = WhiteSpace.Normal } });
                }
                right.Add(Dim(TotalsLine(nodes)));
            }
            columns.Add(right);
            return box;
        }

        /// <summary>One line per ability and summon on the branch, cheapest to reach first.</summary>
        private IEnumerable<string> GrantLines(SphereGridSO grid, List<SphereGridNode> nodes)
        {
            var lines = new List<KeyValuePair<int, string>>();
            foreach (var node in nodes)
            {
                if (node.Kind == SphereNodeKind.MagicKnown && !string.IsNullOrEmpty(node.GrantedMagicKey))
                {
                    int cost = CostToReach(grid, node.Key, out bool materials);
                    string name = node.GrantedMagicKey;
                    string scaling = "";
                    if (_magicByKey.TryGetValue(node.GrantedMagicKey, out var magic))
                    {
                        name = string.IsNullOrEmpty(magic.DisplayName) ? magic.name : magic.DisplayName;
                        var stats = magic.Effects.Select(e => e.ScalingStat).Where(s => s != StatType.None).Distinct().ToList();
                        if (stats.Count > 0)
                        {
                            scaling = " · " + string.Join("/", stats.Select(StatCatalog.ShortName));
                        }
                    }
                    else
                    {
                        name += " (missing!)";
                    }
                    string flags = (node.UnlockedByDefault ? " · starts known" : $" · {cost} xp") +
                                   (materials ? " + materials" : "") + $" · {node.GrantedCharges} ch";
                    lines.Add(new KeyValuePair<int, string>(cost, $"✦ {name}{scaling}{flags}"));
                }
                else if (node.Kind == SphereNodeKind.Summon && !string.IsNullOrEmpty(node.GrantedSummonKey))
                {
                    int cost = CostToReach(grid, node.Key, out bool materials);
                    string name = node.GrantedSummonKey;
                    string kind = "";
                    if (_summonByKey.TryGetValue(node.GrantedSummonKey, out var summon))
                    {
                        name = summon.Label;
                        kind = summon.Kind == SummonKind.ReplaceParty ? " · party replacement"
                            : summon.Kind == SummonKind.JoinParty ? " · fights beside the party"
                            : " · special attack";
                    }
                    else
                    {
                        name += " (missing!)";
                    }
                    int upgrades = grid.Nodes.Count(n => n.GrantedSummonKey == node.GrantedSummonKey &&
                                                         (int)n.Kind >= (int)SphereNodeKind.SummonPower);
                    lines.Add(new KeyValuePair<int, string>(cost,
                        $"★ {name}{kind} · {cost} xp{(materials ? " + materials" : "")} · {upgrades} upgrades"));
                }
            }
            if (lines.Count == 0)
            {
                return new[] { "No abilities or summons — stats only." };
            }
            return lines.OrderBy(l => l.Key).Select(l => l.Value);
        }

        private static string TotalsLine(List<SphereGridNode> nodes)
        {
            var stats = new StatBlock();
            var resists = new SortedDictionary<string, float>();
            int slots = 0;
            foreach (var node in nodes)
            {
                switch (node.Kind)
                {
                    case SphereNodeKind.Stat:
                        foreach (var gain in node.Gains.NonZero())
                        {
                            stats[gain.Type] = stats[gain.Type] + gain.Amount;
                        }
                        break;
                    case SphereNodeKind.Resistance:
                        string type = node.ResistType.ToString();
                        resists[type] = (resists.TryGetValue(type, out var have) ? have : 0f) + node.ResistPercent;
                        break;
                    case SphereNodeKind.MagicSlot:
                        slots++;
                        break;
                }
            }

            var sb = new StringBuilder($"{nodes.Count} nodes · ");
            sb.Append(StatLine(stats));
            foreach (var resist in resists)
            {
                sb.Append($" · {resist.Key} {resist.Value:0}%");
            }
            if (slots > 0)
            {
                sb.Append($" · +{slots} slot{(slots == 1 ? "" : "s")}");
            }
            return sb.ToString();
        }

        // --- branch recovery -------------------------------------------------------------------------

        private static Dictionary<string, List<SphereGridNode>> GroupByBranch(SphereGridSO grid)
        {
            var groups = new Dictionary<string, List<SphereGridNode>>();
            foreach (var node in grid.Nodes)
            {
                string branch = BranchOf(grid, node.Key);
                if (!groups.TryGetValue(branch, out var list))
                {
                    list = new List<SphereGridNode>();
                    groups[branch] = list;
                }
                list.Add(node);
            }
            return groups;
        }

        /// <summary>The node's own branch letter, else the nearest lettered node before it on its
        /// shortest path from the start, else the trunk.</summary>
        private static string BranchOf(SphereGridSO grid, string key)
        {
            string own = LetterOf(key);
            if (own != null)
            {
                return own;
            }
            var path = SphereGridOps.PathTo(grid, key);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                string letter = LetterOf(path[i]);
                if (letter != null)
                {
                    return letter;
                }
            }
            return Trunk;
        }

        private static string LetterOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            var parts = key.Split('-');
            if (parts.Length >= 3 && parts[1].Length == 1 && char.IsLetter(parts[1][0]))
            {
                return parts[1].ToLowerInvariant();
            }
            return null;
        }

        private static string Normalize(string branch)
        {
            return string.IsNullOrWhiteSpace(branch) ? null : branch.Trim().ToLowerInvariant();
        }

        /// <summary>XP to walk the shortest route from the start to this node, free nodes excluded.</summary>
        private static int CostToReach(SphereGridSO grid, string key, out bool materials)
        {
            int cost = 0;
            materials = false;
            foreach (var step in SphereGridOps.PathTo(grid, key))
            {
                var node = SphereGridOps.FindNode(grid, step);
                if (node == null || SphereGridOps.IsDefaultUnlocked(node))
                {
                    continue;
                }
                cost += node.XpCost;
                materials |= SphereGridOps.HasMaterialCost(node);
            }
            return cost;
        }

        // --- small helpers ---------------------------------------------------------------------------

        private static VisualElement Section(string title)
        {
            var section = new VisualElement { style = { marginTop = 10f } };
            section.Add(new Label(title) { style = { fontSize = 13f, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4f } });
            return section;
        }

        private static VisualElement Column(string title)
        {
            var column = new VisualElement { style = { flexGrow = 1f, flexBasis = 0f, marginRight = 8f } };
            column.Add(new Label(title) { style = { opacity = 0.6f, fontSize = 10f, marginBottom = 2f } });
            return column;
        }

        private static PropertyField Field(string path)
        {
            return new PropertyField { bindingPath = path };
        }

        private static Label Dim(string text)
        {
            return new Label(text) { style = { opacity = 0.65f, whiteSpace = WhiteSpace.Normal } };
        }

        private static void AddText(VisualElement parent, string caption, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            parent.Add(new Label($"<b>{caption}:</b> {text}") { enableRichText = true, style = { whiteSpace = WhiteSpace.Normal, marginBottom = 2f } });
        }

        private static string StatLine(StatBlock stats)
        {
            var parts = stats.NonZero().Select(s => $"{StatCatalog.ShortName(s.Type)} {s.Amount}").ToList();
            return parts.Count == 0 ? "no stats" : string.Join(", ", parts);
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }

        private static Dictionary<string, T> LoadByKey<T>(System.Func<T, string> key) where T : Object
        {
            var map = new Dictionary<string, T>();
            foreach (var asset in LoadAll<T>())
            {
                string k = key(asset);
                if (!string.IsNullOrEmpty(k) && !map.ContainsKey(k))
                {
                    map[k] = asset;
                }
            }
            return map;
        }
    }
}
