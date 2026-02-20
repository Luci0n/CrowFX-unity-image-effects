#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEditor.PackageManager;
using System.IO;
using CrowFX;

namespace CrowFX.EditorTools
{
[CustomEditor(typeof(CrowImageEffects))]
    public sealed class CrowImageEffectsEditor : Editor
    {
        // =============================================================================================
        // AUTO SECTION MODEL
        // =============================================================================================
        private sealed class SectionDef
        {
            public string Key;
            public string Title;
            public string Icon;
            public string Hint;
            public int Order;
            public AnimBool Fold;
            public Action Draw; // if null => auto-draw

            public SectionDef(string key) { Key = key; Title = key; Icon = "d_Settings"; Hint = ""; Order = 0; }
        }

        private readonly Dictionary<string, List<string>> _propsBySection = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimBool> _foldBySection = new(StringComparer.Ordinal);
        private readonly HashSet<string> _drawnThisSection = new(StringComparer.Ordinal);
        private readonly List<SectionDef> _sections = new();

        // Custom UI extras
        private AnimBool _foldResolutionPresets;

        // RGB Bleed sub-foldouts
        private AnimBool _foldBleedModeCombine;
        private AnimBool _foldBleedManual;
        private AnimBool _foldBleedRadial;
        private AnimBool _foldBleedEdge;
        private AnimBool _foldBleedSmear;
        private AnimBool _foldBleedPerChannel;
        private AnimBool _foldBleedSafety;
        private AnimBool _foldBleedWobble;
        private AnimBool _foldJitterAdvanced;

        private readonly List<AnimBool> _allFolds = new();

        // Search
        private const string Pref_Search = "CrowImageEffectsEditor.Search";
        private string _search = "";

        private static string _rootFromThisScript;

        private static string RootFromThisScript
        {
            get
            {
                if (!string.IsNullOrEmpty(_rootFromThisScript))
                    return _rootFromThisScript;

                var temp = CreateInstance<CrowImageEffectsEditor>();
                try
                {
                    var ms = MonoScript.FromScriptableObject(temp);
                    var scriptPath = AssetDatabase.GetAssetPath(ms);

                    if (string.IsNullOrEmpty(scriptPath))
                    {
                        _rootFromThisScript = "Assets";
                        return _rootFromThisScript;
                    }

                    scriptPath = scriptPath.Replace("\\", "/");

                    var editorIndex = scriptPath.LastIndexOf("/Editor/", StringComparison.Ordinal);
                    if (editorIndex >= 0)
                    {
                        _rootFromThisScript = scriptPath.Substring(0, editorIndex);
                        return _rootFromThisScript;
                    }

                    _rootFromThisScript = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/") ?? "Assets";
                    return _rootFromThisScript;
                }
                finally
                {
                    if (temp != null)
                        DestroyImmediate(temp);
                }
            }
        }

        private static T LoadAssetAt<T>(string relativeToRoot) where T : UnityEngine.Object
        {
            var path = $"{RootFromThisScript}/{relativeToRoot}".Replace("\\", "/");
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private Texture2D _diceIcon;
        private Texture2D GetDiceIcon()
            => _diceIcon != null ? _diceIcon : (_diceIcon = LoadAssetAt<Texture2D>("Editor/Icons/dice_icon.png"));

        private Texture2D _iconLogo;
        private Texture2D GetIconLogo()
            => _iconLogo != null ? _iconLogo : (_iconLogo = LoadAssetAt<Texture2D>("Editor/Icons/header.png"));

        private Font _customFont;
        private Font GetCustomFont()
            => _customFont != null ? _customFont : (_customFont = LoadAssetAt<Font>("Editor/Font/JetBrainsMonoNL-Thin.ttf"));

        // Favorites
        private HashSet<string> _favoriteSections = new(StringComparer.Ordinal);
        private const string Pref_Favorites = "CrowImageEffectsEditor.Favorites";

        // =============================================================================================
        // THEME / STYLES (your existing look preserved)
        // =============================================================================================
        private static class Theme
        {
            public static readonly Color PanelBackground   = new Color(0.13f, 0.13f, 0.13f, 1f);
            public static readonly Color HeaderBackground  = new Color(0.16f, 0.16f, 0.16f, 1f);
            public static readonly Color BorderColor       = new Color(0f, 0f, 0f, 0.35f);
            public static readonly Color DividerColor      = new Color(1f, 1f, 1f, 0.06f);
            public static readonly Color TextPrimary       = new Color(1f, 1f, 1f, 0.86f);
            public static readonly Color TextSecondary     = new Color(1f, 1f, 1f, 0.70f);
            public static readonly Color HintBackground    = new Color(0f, 0f, 0f, 0.3f);
            public static readonly Color WarningBackground = new Color(1f, 1f, 1f, 0.065f);
            public static readonly Color ErrorBackground   = new Color(1f, 1f, 1f, 0.085f);
            public static readonly Color ButtonNormal      = new Color(1f, 1f, 1f, 0.055f);
            public static readonly Color ButtonHover       = new Color(1f, 1f, 1f, 0.085f);
            public static readonly Color ButtonActive      = new Color(1f, 1f, 1f, 0.12f);

            public static void DrawBorder(Rect rect)
            {
                if (Event.current.type != EventType.Repaint) return;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), BorderColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), BorderColor);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), BorderColor);
            }

            public static void DrawDivider(float padding = 2f)
            {
                var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
                rect.xMin += padding;
                rect.xMax -= padding;

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, DividerColor);
            }
        }

        private static class Styles
        {
            private static bool _initialized;
            private static Font _appliedFont;
            public static Texture2D PanelTexture;
            public static Texture2D HeaderTexture;

            public static GUIStyle Panel;
            public static GUIStyle HeaderLabel;
            public static GUIStyle HeaderHint;
            public static GUIStyle SectionTitle;
            public static GUIStyle SummaryText;
            public static GUIStyle HintText;
            public static GUIStyle PillButton;
            public static GUIStyle ResetButton;
            public static GUIStyle SubHeaderLabel;

            public static GUIStyle SearchField;
            public static GUIStyle SearchCancel;
            public static void ApplyFont(Font font)
            {
                if (font == null || font == _appliedFont) return;
                _appliedFont = font;

                HeaderLabel.font    = font;
                SubHeaderLabel.font = font;
                SectionTitle.font   = font;
                SummaryText.font    = font;
                HintText.font       = font;
                HeaderHint.font     = font;
                PillButton.font     = font;
                ResetButton.font    = font;
            }
            public static void Ensure()
            {
                if (_initialized) return;

                PanelTexture  = CreateColorTexture(Theme.PanelBackground);
                HeaderTexture = CreateColorTexture(Theme.HeaderBackground);

                Panel = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 8, 10),
                    margin = new RectOffset(0, 0, 6, 6),
                    normal = { background = PanelTexture }
                };

                HeaderLabel = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };

                SubHeaderLabel = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };

                HeaderHint = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    richText = true,
                    normal = { textColor = Theme.TextSecondary }
                };

                SectionTitle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };

                SummaryText = new GUIStyle(EditorStyles.miniLabel)
                {
                    richText = true,
                    normal = { textColor = Theme.TextPrimary }
                };

                HintText = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    richText = true,
                    normal = { textColor = Theme.TextPrimary }
                };

                PillButton = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(10, 10, 0, 0),
                    fontSize = 11,
                    normal = { textColor = Theme.TextPrimary }
                };

                ResetButton = new GUIStyle(PillButton)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                // Unity builtin styles (keep it native-feeling)
                SearchField = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
                SearchCancel = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.button;

                _initialized = true;
            }

            private static Texture2D CreateColorTexture(Color color)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, color);
                texture.Apply();
                return texture;
            }
        }

        private readonly struct EnabledScope : IDisposable
        {
            private readonly bool _previousState;
            public EnabledScope(bool enabled) { _previousState = GUI.enabled; GUI.enabled = enabled; }
            public void Dispose() => GUI.enabled = _previousState;
        }

        private enum HintType { Info, Warning, Error }

        private static class IconCache
        {
            private static readonly Dictionary<string, Texture> Cache = new(StringComparer.Ordinal);

            public static Texture Get(string name)
            {
                if (string.IsNullOrEmpty(name)) return null;

                if (Cache.TryGetValue(name, out var cached))
                    return cached;

                var content = EditorGUIUtility.IconContent(name.StartsWith("d_") ? name : "d_" + name);
                var texture = content?.image;

                if (texture == null)
                {
                    content = EditorGUIUtility.IconContent(name);
                    texture = content?.image;
                }

                Cache[name] = texture;
                return texture;
            }
        }

        // =============================================================================================
        // LIFECYCLE
        // =============================================================================================
        private void OnEnable()
        {
            _search = EditorPrefs.GetString(Pref_Search, "");
            LoadFavorites();
            InitExtraFoldouts();
            RebuildAll();
        }

        private void OnDisable()
        {
            UnregisterAllFoldListeners();
        }

        private void RebuildAll()
        {
            BuildPropertyMapFromAttributes();
            BuildSectionDefs();
        }

        private void InitExtraFoldouts()
        {
            _foldResolutionPresets = NewFold("Sampling.ResolutionPresets", defaultExpanded: false);

            _foldBleedModeCombine = NewFold("Bleed.ModeCombine", defaultExpanded: true);
            _foldBleedManual      = NewFold("Bleed.Manual",      defaultExpanded: false);
            _foldBleedRadial      = NewFold("Bleed.Radial",      defaultExpanded: false);
            _foldBleedEdge        = NewFold("Bleed.Edge",        defaultExpanded: false);
            _foldBleedSmear       = NewFold("Bleed.Smear",       defaultExpanded: false);
            _foldBleedPerChannel  = NewFold("Bleed.PerChannel",  defaultExpanded: false);
            _foldBleedSafety      = NewFold("Bleed.Safety",      defaultExpanded: false);
            _foldBleedWobble      = NewFold("Bleed.Wobble",      defaultExpanded: false);
            _foldJitterAdvanced   = NewFold("Jitter.Advanced",   defaultExpanded: false);
        }

        // =============================================================================================
        // FOLDS (auto-created per section, persisted in EditorPrefs)
        // =============================================================================================
        private AnimBool NewFold(string id, bool defaultExpanded)
        {
            var key = PrefKey(id);
            bool start = EditorPrefs.GetBool(key, defaultExpanded);
            var fold = new AnimBool(start);

            fold.valueChanged.AddListener(() =>
            {
                EditorPrefs.SetBool(key, fold.target);
                Repaint();
            });

            _allFolds.Add(fold);
            return fold;
        }

        private AnimBool GetOrCreateSectionFold(string sectionKey, bool defaultExpanded)
        {
            if (_foldBySection.TryGetValue(sectionKey, out var fold))
                return fold;

            fold = NewFold("Section." + sectionKey, defaultExpanded);
            _foldBySection[sectionKey] = fold;
            return fold;
        }

        private void UnregisterAllFoldListeners()
        {
            foreach (var f in _allFolds)
                if (f != null) f.valueChanged.RemoveAllListeners();
            _allFolds.Clear();
        }

        private static string PrefKey(string id) => "CrowImageEffectsEditor." + id;

        // =============================================================================================
        // SERIALIZED PROPERTY ACCESS
        // =============================================================================================
        private SerializedProperty SP(string name)
        {
            var p = serializedObject.FindProperty(name);
            if (p == null) Debug.LogWarning($"Property '{name}' not found on CrowImageEffects");
            return p;
        }

        // =============================================================================================
        // ATTRIBUTE DISCOVERY (fields + sections)
        // =============================================================================================
        private void BuildPropertyMapFromAttributes()
        {
            _propsBySection.Clear();

            var t = typeof(CrowImageEffects);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = t.GetFields(flags);

            var declIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i++)
                declIndex[fields[i].Name] = i;

            var discovered = new List<(string section, int order, int decl, string name)>(fields.Length);

            foreach (var f in fields)
            {
                if (f.IsStatic) continue;
                if (f.IsNotSerialized) continue;
                if (Attribute.IsDefined(f, typeof(HideInInspector))) continue;

                bool serializable = f.IsPublic || Attribute.IsDefined(f, typeof(SerializeField));
                if (!serializable) continue;

                var attr = f.GetCustomAttribute<EffectSectionAttribute>(inherit: true);
                if (attr == null) continue;

                var prop = serializedObject.FindProperty(f.Name);
                if (prop == null) continue;

                discovered.Add((attr.Section ?? "Misc", attr.Order, declIndex[f.Name], f.Name));
            }

            foreach (var g in discovered
                        .OrderBy(x => x.section, StringComparer.Ordinal)
                        .ThenBy(x => x.order)
                        .ThenBy(x => x.decl))
            {
                if (!_propsBySection.TryGetValue(g.section, out var list))
                {
                    list = new List<string>(32);
                    _propsBySection[g.section] = list;
                }
                list.Add(g.name);
            }
        }

        private Dictionary<string, EffectSectionMetaAttribute> ReadSectionMeta()
        {
            var meta = new Dictionary<string, EffectSectionMetaAttribute>(StringComparer.Ordinal);
            var metas = typeof(CrowImageEffects).GetCustomAttributes<EffectSectionMetaAttribute>(inherit: true);
            foreach (var m in metas)
                meta[m.Key] = m;
            return meta;
        }

        private void BuildSectionDefs()
        {
            _sections.Clear();

            var meta = ReadSectionMeta();
            var keys = _propsBySection.Keys.ToList();
            keys = keys.Distinct(StringComparer.Ordinal).ToList();

            foreach (var key in keys)
            {
                var def = new SectionDef(key);

                if (meta.TryGetValue(key, out var m))
                {
                    def.Title = m.Title;
                    def.Icon = m.Icon;
                    def.Hint = m.Hint;
                    def.Order = m.Order;
                    def.Fold = GetOrCreateSectionFold(key, m.DefaultExpanded);
                }
                else
                {
                    def.Title = key;
                    def.Icon = "d_Settings";
                    def.Hint = "";
                    def.Order = 500;
                    def.Fold = GetOrCreateSectionFold(key, defaultExpanded: false);
                }

                def.Draw = ResolveCustomDrawerOrNull(key);
                _sections.Add(def);
            }

            _sections.Sort((a, b) =>
            {
                bool aFav = _favoriteSections.Contains(a.Key);
                bool bFav = _favoriteSections.Contains(b.Key);

                if (aFav != bFav) return aFav ? -1 : 1; // favorites float to top

                int c = a.Order.CompareTo(b.Order);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Key, b.Key);
            });
        }

        private Action ResolveCustomDrawerOrNull(string sectionKey)
        {
            return sectionKey switch
            {
                "Master"      => () => DrawMasterContent(),
                "Pregrade"    => () => DrawPregradeContent(),
                "Sampling"    => () => DrawSamplingContent(),
                "Posterize"   => () => DrawPosterizeContent(),
                "Palette"     => () => DrawPaletteContent(),
                "TextureMask" => () => DrawMaskingContent(),
                "DepthMask"   => () => DrawDepthMaskContent(),
                "Jitter"      => () => DrawJitterContent(),
                "Bleed"       => () => DrawBleedContent(),
                "Ghost"       => () => DrawGhostContent(),
                "Edges"       => () => DrawEdgeContent(),
                "Unsharp"     => () => DrawUnsharpContent(),
                "Dither"      => () => DrawDitherContent(),
                "Shaders"     => () => DrawShadersContent(),
                _             => null
            };
        }

        // =============================================================================================
        // INSPECTOR
        // =============================================================================================
        public override void OnInspectorGUI()
        {
            Styles.Ensure();
            Styles.ApplyFont(GetCustomFont());
            serializedObject.Update();

            var fx = (CrowImageEffects)target;

            DrawSummaryPanel(fx);

            foreach (var s in _sections.ToList())
            {
                if (!string.IsNullOrWhiteSpace(_search) && !SectionHasAnyMatch(s.Key))
                    continue;

                DrawSection(
                    sectionKey: s.Key,
                    title: s.Title,
                    icon: s.Icon,
                    fold: s.Fold,
                    hint: s.Hint,
                    drawContent: s.Draw ?? (() => DrawAutoSection(s.Key))
                );
            }

            serializedObject.ApplyModifiedProperties();
            EnsureDepthModeIfNeeded(fx);

            if (Event.current.type == EventType.Layout)
                _drawnThisSection.Clear();
        }

        // =============================================================================================
        // TOP SUMMARY (with embedded search)
        // =============================================================================================
        private void DrawSummaryPanel(CrowImageEffects targetFx)
        {
            using (new EditorGUILayout.VerticalScope(Styles.Panel))
            {
            /*var diceIcon = GetDiceIcon();
            if (diceIcon != null)
            {
                bool isHovered = randomRect.Contains(Event.current.mousePosition);
                bool isPressed = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
                Color bgColor  = isPressed ? Theme.ButtonActive : isHovered ? Theme.ButtonHover : Theme.ButtonNormal;

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(randomRect, bgColor);
                    Theme.DrawBorder(randomRect);
                    float pad = 2f;
                    var iconRect = new Rect(randomRect.x + pad, randomRect.y + pad,
                                            randomRect.width - pad, randomRect.height - pad);
                    GUI.DrawTexture(iconRect, diceIcon, ScaleMode.ScaleToFit, true);
                } */
                using (new EditorGUILayout.HorizontalScope())
                {
                    var iconLogo = GetIconLogo();
                    if (iconLogo != null)
                    {
                        var iconRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(120f), GUILayout.Height(24f));
                        GUI.DrawTexture(iconRect, iconLogo, ScaleMode.StretchToFill, true);
                    }
                }

                string resolution = targetFx != null && targetFx.useVirtualGrid
                    ? $"{Mathf.Max(1, targetFx.virtualResolution.x)}×{Mathf.Max(1, targetFx.virtualResolution.y)}"
                    : "Screen";

                string pixelation = targetFx != null && targetFx.pixelSize > 1 ? $"×{targetFx.pixelSize}" : "Off";
                string ditherMode = targetFx != null ? targetFx.ditherMode.ToString() : "-";

                string ghostInfo = targetFx != null && targetFx.ghostEnabled
                    ? $"{targetFx.ghostFrames}f / +{targetFx.ghostCaptureInterval} / d{targetFx.ghostStartDelay}"
                    : "-";

                GUILayout.Space(4);

                var summary =
                    $"Grid: {resolution}   Pixel: {pixelation}   " +
                    $"Dither: {ditherMode}   Ghost: {ghostInfo}";

                EditorGUILayout.LabelField(summary, Styles.SummaryText);
                Theme.DrawDivider();

                GUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.SetNextControlName("CrowFX_Search");
                    var next = EditorGUILayout.TextField(new GUIContent("Search"), _search, Styles.SearchField);

                    bool changed = !string.Equals(next, _search, StringComparison.Ordinal);
                    if (changed)
                    {
                        _search = next ?? "";
                        EditorPrefs.SetString(Pref_Search, _search);
                    }

                    var clearRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
                    if (GUI.Button(clearRect, GUIContent.none, Styles.SearchCancel))
                    {
                        _search = "";
                        EditorPrefs.SetString(Pref_Search, _search);
                        GUI.FocusControl(null);
                        Repaint();
                    }
                }

                ShowHint("Type to filter settings by name (e.g., “ghost”, “dither”, “resolution”). Click X to clear.");
            }
        }

        // =============================================================================================
        // SECTION CONTAINERS (header has per-section reset button & randomization)
        // =============================================================================================
        private void DrawSection(string sectionKey, string title, string icon, AnimBool fold, Action drawContent, string hint)
        {
            using (new EditorGUILayout.VerticalScope(Styles.Panel))
            {
                var headerRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(headerRect, Styles.HeaderTexture, ScaleMode.StretchToFill);
                    Theme.DrawBorder(headerRect);
                }

                Rect starRect   = new Rect(headerRect.x + 2f,    headerRect.y + 4f, 16f, 18f);
                Rect resetRect  = new Rect(headerRect.xMax - 96f, headerRect.y + 4f, 92f, 18f);
                Rect randomRect = new Rect(headerRect.xMax - 114f, headerRect.y + 4f, 16f, 18f);
                Rect rightButtons = new Rect(randomRect.x, randomRect.y, resetRect.xMax - randomRect.x, randomRect.height);

                Rect ignoreLeft  = new Rect(starRect.x,   starRect.y,   starRect.width,               starRect.height);
                Rect ignoreRight = new Rect(randomRect.x, randomRect.y, resetRect.xMax - randomRect.x, randomRect.height);
                Rect ignoreAll = new Rect(ignoreLeft.x, ignoreLeft.y, ignoreRight.xMax - ignoreLeft.x, ignoreLeft.height);

                HandleHeaderClick(headerRect, fold, ignoreRect1: starRect, ignoreRect2: rightButtons);

                DrawStarButton(starRect, sectionKey);
                DrawSectionHeader(headerRect, title, icon, hint, fold.target, randomRect);
                DrawSectionEnabledDot(headerRect, sectionKey);
                DrawDiceButton(randomRect, sectionKey);

                if (HandleHeaderResetButton(resetRect, sectionKey))
                    RebuildAll();

                using (var fade = new EditorGUILayout.FadeGroupScope(fold.faded))
                {
                    if (fade.visible)
                    {
                        GUILayout.Space(8);
                        drawContent?.Invoke();
                    }
                }
            }
        }

        private void DrawStarButton(Rect rect, string sectionKey)
        {
            bool isFav = _favoriteSections.Contains(sectionKey);

            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isPressed = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            Color bgColor  = isPressed ? Theme.ButtonActive : isHovered ? Theme.ButtonHover : Theme.ButtonNormal;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, bgColor);
                Theme.DrawBorder(rect);
            }

            var starStyle = new GUIStyle(Styles.HeaderLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(0, 0, 0, 0)
            };

            var prev = GUI.contentColor;
            GUI.contentColor = isFav ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.25f);
            GUI.Label(rect, isFav ? "★" : "☆", starStyle);
            GUI.contentColor = prev;

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                ToggleFavorite(sectionKey);
                Repaint();
                Event.current.Use();
            }
        }

        private void DrawDiceButton(Rect randomRect, string sectionKey)
        {
            var diceIcon = GetDiceIcon();
            if (diceIcon != null)
            {
                bool isHovered = randomRect.Contains(Event.current.mousePosition);
                bool isPressed = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
                Color bgColor  = isPressed ? Theme.ButtonActive : isHovered ? Theme.ButtonHover : Theme.ButtonNormal;

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(randomRect, bgColor);
                    Theme.DrawBorder(randomRect);
                    float pad = 2f;
                    var iconRect = new Rect(randomRect.x + pad, randomRect.y + pad,
                                            randomRect.width - pad, randomRect.height - pad);
                    GUI.DrawTexture(iconRect, diceIcon, ScaleMode.ScaleToFit, true);
                }

                if (GUI.Button(randomRect, GUIContent.none, GUIStyle.none))
                {
                    if (EditorUtility.DisplayDialog("Randomize Section",
                        $"Randomize \"{sectionKey}\" values?\n\nThis cannot be undone.",
                        "Randomize", "Cancel"))
                    {
                        RandomizeSectionProperties(sectionKey);
                    }
                    Event.current.Use();
                }

                if (randomRect.Contains(Event.current.mousePosition))
                    GUI.Label(randomRect, new GUIContent("", "Randomize"), GUIStyle.none);
            }
            else
            {
                if (HeaderResetPill(randomRect, "?"))
                {
                    if (EditorUtility.DisplayDialog("Randomize Section",
                        $"Randomize \"{sectionKey}\" values?\n\nThis cannot be undone.",
                        "Randomize", "Cancel"))
                    {
                        RandomizeSectionProperties(sectionKey);
                    }
                }
            }
        }

        private void DrawSubSection(string title, string icon, AnimBool fold, Action drawContent, string hint)
        {
            var headerRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                var inset = headerRect;
                inset.xMin += 2f;
                inset.xMax -= 2f;

                GUI.DrawTexture(inset, Styles.HeaderTexture, ScaleMode.StretchToFill);
                Theme.DrawBorder(inset);
            }

            HandleHeaderClick(headerRect, fold, ignoreRect1: default, ignoreRect2: default);
            DrawSubSectionHeader(headerRect, title, icon, hint, fold.target);

            using (var fade = new EditorGUILayout.FadeGroupScope(fold.faded))
            {
                if (fade.visible)
                {
                    GUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(6);
                        using (new EditorGUILayout.VerticalScope())
                            drawContent?.Invoke();
                    }
                    GUILayout.Space(2);
                }
            }
        }

        private static void HandleHeaderClick(Rect rect, AnimBool fold, Rect ignoreRect1, Rect ignoreRect2 = default)
        {
            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                if (ignoreRect1.width > 0f && ignoreRect1.Contains(current.mousePosition)) return;
                if (ignoreRect2.width > 0f && ignoreRect2.Contains(current.mousePosition)) return;

                fold.target = !fold.target;
                current.Use();
            }
        }

        private void DrawSectionHeader(Rect rect, string title, string iconName, string hint, bool isExpanded, Rect rightButtonsRect)
        {
            var icon = IconCache.Get(iconName);

            var chevronRect = new Rect(rect.x + 24f, rect.y + 4f, 14f, 18f);
            GUI.Label(chevronRect, isExpanded ? "▾" : "▸", Styles.HeaderLabel);

            float xPos = rect.x + 40f;

            if (icon != null)
            {
                var iconRect = new Rect(xPos, rect.y + 5f, 16f, 16f);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                xPos = iconRect.xMax + 6f;
            }

            float maxTitleWidth = 120f;
            var titleRect = new Rect(xPos, rect.y + 4f, maxTitleWidth, 18f);
            GUI.Label(titleRect, title, Styles.HeaderLabel);

            if (!string.IsNullOrEmpty(hint))
            {
                float hintLeft  = xPos + maxTitleWidth + 4f;
                float hintRight = rightButtonsRect.xMin - 4f; // leftmost RIGHT-side button
                float hintWidth = hintRight - hintLeft;

                if (hintWidth > 20f)
                {
                    var hintRect = new Rect(hintLeft, rect.y + 6f, hintWidth, 16f);
                    GUI.Label(hintRect, $"<i>{hint}</i>", Styles.HeaderHint);
                }
            }
        }
        
        private void DrawSubSectionHeader(Rect rect, string title, string iconName, string hint, bool isExpanded)
        {
            var icon = IconCache.Get(iconName);

            var foldoutRect = new Rect(rect.x + 6f, rect.y + 4f, 14f, 14f);
            EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, true);

            float xPos = rect.x + 10f;

            if (icon != null)
            {
                var iconRect = new Rect(rect.x + 10f, rect.y + 3f, 16f, 16f);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                xPos = iconRect.xMax + 6f;
            }

            var titleRect = new Rect(xPos, rect.y + 2f, rect.width * 0.62f, 18f);
            GUI.Label(titleRect, title, Styles.SubHeaderLabel);

            if (!string.IsNullOrEmpty(hint))
            {
                var hintRect = new Rect(rect.x + rect.width * 0.62f, rect.y + 4f, rect.width * 0.36f, 16f);
                GUI.Label(hintRect, $"<i>{hint}</i>", Styles.HeaderHint);
            }
        }

        private void DrawHeader(string title, string hint)
        {
            var rect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, Styles.HeaderTexture, ScaleMode.StretchToFill);
                Theme.DrawBorder(rect);
            }

            var titleRect = new Rect(rect.x + 10f, rect.y + 4f, rect.width * 0.7f, 18f);
            GUI.Label(titleRect, title, Styles.SectionTitle);

            if (!string.IsNullOrEmpty(hint))
            {
                var hintRect = new Rect(rect.x + rect.width * 0.7f, rect.y + 6f, rect.width * 0.28f, 16f);
                GUI.Label(hintRect, $"<i>{hint}</i>", Styles.HeaderHint);
            }
        }

        private static Texture2D _dotCircleOn;
        private static Texture2D _dotCircleOff;

        private static Texture2D GetDotTexture(bool on)
        {
            ref var tex = ref on ? ref _dotCircleOn : ref _dotCircleOff;
            if (tex != null) return tex;

            const int size = 16;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            Color fill   = on ? new Color(0.9f, 0.2f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.15f);
            Color clear  = new Color(0f, 0f, 0f, 0f);
            float center = (size - 1) * 0.5f;
            float radius = (size * 0.5f) - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Smooth edge via alpha lerp in the last pixel
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, dist <= radius + 0.5f
                    ? new Color(fill.r, fill.g, fill.b, fill.a * alpha)
                    : clear);
            }

            tex.Apply();
            return tex;
        }

        private static readonly Dictionary<string, string> _dotPropOverrides = new(StringComparer.Ordinal)
        {
            { "Edges",       "edgeEnabled"     },
            { "Unsharp",     "unsharpEnabled"  },
            { "Pregrade",    "pregradeEnabled" },
            { "TextureMask", "useMask"         },
            { "DepthMask",   "useDepthMask"    },
            { "Jitter",      "jitterEnabled"   },
            { "Bleed",       "bleedIntensity"  },
            { "Palette",     "usePalette"      },
            { "Dither",      "ditherMode"      },
            { "Ghost",       "ghostEnabled"    },
            { "Sampling",    null              },
            { "Posterize",   null              },
            { "Shaders",     null              },
            { "Master",      null              },
        };

        private void DrawSectionEnabledDot(Rect headerRect, string sectionKey)
        {
            bool isOn = IsSectionActive(sectionKey);

            if (_dotPropOverrides.TryGetValue(sectionKey, out var overrideName))
            {
                if (overrideName == null) return;
            }
            else
            {
                string lower = sectionKey.ToLower();
                string[] candidates = {
                    lower + "Enabled",
                    lower.TrimEnd('s') + "Enabled",
                    "use" + sectionKey,
                    "enable" + sectionKey
                };

                bool found = false;
                foreach (var candidate in candidates)
                {
                    var p = serializedObject.FindProperty(candidate);
                    if (p != null && p.propertyType == SerializedPropertyType.Boolean)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return;
            }

            var dotRect = new Rect(headerRect.xMax + 1f, headerRect.y + 9f, 8f, 8f);

            if (Event.current.type == EventType.Repaint)
            {
                if (isOn)
                {
                    var glowRect = new Rect(dotRect.x - 3f, dotRect.y - 3f, dotRect.width + 6f, dotRect.height + 6f);
                    GUI.DrawTexture(glowRect, GetDotTexture(true), ScaleMode.ScaleToFit, true, 0,
                        new Color(1f, 0.1f, 0.1f, 0.25f), 0, 0);
                }
                GUI.DrawTexture(dotRect, GetDotTexture(isOn), ScaleMode.ScaleToFit, true);
            }
        }
        private bool IsSectionActive(string sectionKey)
        {
            if (!_dotPropOverrides.TryGetValue(sectionKey, out var name) || name == null) return false;
            var p = serializedObject.FindProperty(name);
            if (p == null) return false;
            return p.propertyType switch
            {
                SerializedPropertyType.Boolean => p.boolValue,
                SerializedPropertyType.Float   => p.floatValue > 0f,
                SerializedPropertyType.Integer => p.intValue > 0,
                SerializedPropertyType.Enum    => p.enumValueIndex != 0,
                _                              => false
            };
        }
        // =============================================================================================
        // BUTTONS + HINTS
        // =============================================================================================
        private bool PillButton(string label, float height, GUIStyle style, params GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(0f, height, options);

            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isHot = GUIUtility.hotControl != 0 && isHovered;
            bool isPressed = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;

            Color backgroundColor = !GUI.enabled ? new Color(1f, 1f, 1f, 0.03f)
                                : isPressed || isHot ? Theme.ButtonActive
                                : isHovered ? Theme.ButtonHover
                                : Theme.ButtonNormal;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, backgroundColor);
                Theme.DrawBorder(rect);
            }

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

            var previousColor = GUI.contentColor;
            GUI.contentColor = GUI.enabled ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(rect, label, style);
            GUI.contentColor = previousColor;

            return clicked;
        }

        private bool MiniPill(string label, params GUILayoutOption[] options)
            => PillButton(label, 18f, Styles.PillButton, options);

        private bool ResetPill(string label, params GUILayoutOption[] options)
            => PillButton(label, 18f, Styles.ResetButton, options);

        private bool HeaderResetPill(Rect rect, GUIContent content)
        {
            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isHot     = GUIUtility.hotControl != 0 && isHovered;
            bool isPressed = isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;

            Color backgroundColor = isPressed || isHot ? Theme.ButtonActive
                                : isHovered           ? Theme.ButtonHover
                                :                       Theme.ButtonNormal;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, backgroundColor);
                Theme.DrawBorder(rect);
            }

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

            var prev = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(rect, content, Styles.ResetButton);
            GUI.contentColor = prev;

            if (clicked) Event.current.Use();
            return clicked;
        }

        private bool HeaderResetPill(Rect rect, string label) => HeaderResetPill(rect, new GUIContent(label));

        private void ShowHint(string message, HintType type = HintType.Info)
        {
            var content = new GUIContent(message);
            float labelWidth = EditorGUIUtility.currentViewWidth - 48f;
            float height = Mathf.Max(18f, Styles.HintText.CalcHeight(content, labelWidth) + 6f);

            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            rect.xMin += 2f;
            rect.xMax -= 2f;

            Color backgroundColor = type switch
            {
                HintType.Warning => Theme.WarningBackground,
                HintType.Error   => Theme.ErrorBackground,
                _                => Theme.HintBackground
            };

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, backgroundColor);
                Theme.DrawBorder(rect);
            }

            var labelRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, rect.height - 6f);
            var previousColor = GUI.contentColor;
            GUI.contentColor = Theme.TextPrimary;
            GUI.Label(labelRect, content, Styles.HintText);
            GUI.contentColor = previousColor;
        }

        // =============================================================================================
        // SEARCH FILTER HELPERS
        // =============================================================================================
        private bool PassesSearch(string haystack)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool PropMatchesSearch(SerializedProperty p)
        {
            if (p == null) return false;
            if (string.IsNullOrWhiteSpace(_search)) return true;

            if (PassesSearch(p.displayName)) return true;
            if (PassesSearch(p.name)) return true;
            if (PassesSearch(p.propertyPath)) return true;
            return false;
        }

        private bool SectionHasAnyMatch(string sectionKey)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            if (!_propsBySection.TryGetValue(sectionKey, out var list)) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var p = serializedObject.FindProperty(list[i]);
                if (p != null && PropMatchesSearch(p)) return true;
            }

            return PassesSearch(sectionKey);
        }

        // =============================================================================================
        // AUTO DRAW CORE
        // =============================================================================================
        private void BeginSectionDrawn() => _drawnThisSection.Clear();

        private void MarkDrawn(string propName)
        {
            if (!string.IsNullOrEmpty(propName))
                _drawnThisSection.Add(propName);
        }

        // FIX: convenience to “claim” properties even when they are intentionally hidden by toggles.
        private void MarkDrawnMany(params string[] propNames)
        {
            if (propNames == null) return;
            for (int i = 0; i < propNames.Length; i++)
                MarkDrawn(propNames[i]);
        }

        private void DrawAutoSection(string sectionKey)
        {
            BeginSectionDrawn();
            DrawAutoRemaining(sectionKey);
        }

        private void DrawAutoRemaining(string sectionKey)
        {
            if (!_propsBySection.TryGetValue(sectionKey, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var name = list[i];
                if (_drawnThisSection.Contains(name)) continue;

                var p = serializedObject.FindProperty(name);
                if (p == null) continue;

                if (!PropMatchesSearch(p)) continue;

                EditorGUILayout.PropertyField(p, includeChildren: true);
            }
        }

        // =============================================================================================
        // HEADER RESET (per-section) + COPY UTILS
        // =============================================================================================
        private bool HandleHeaderResetButton(Rect resetRect, string sectionKey)
        {
            if (!HeaderResetPill(resetRect, "Reset"))
                return false;

            if (EditorUtility.DisplayDialog("Reset Section",
                $"Reset \"{sectionKey}\" values to defaults?\n\nThis cannot be undone.",
                "Reset", "Cancel"))
            {
                ResetSectionToDefaults(sectionKey);
            }

            return true;
        }

        private void ResetSectionToDefaults(string sectionKey)
        {
            var targetFx = (CrowImageEffects)target;
            if (targetFx == null) return;

            if (!_propsBySection.TryGetValue(sectionKey, out var props) || props == null || props.Count == 0)
                return;

            Undo.RecordObject(targetFx, $"Reset {sectionKey}");

            var tmpGO = new GameObject("CrowImageEffects_Defaults__TEMP") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var tmp = tmpGO.AddComponent<CrowImageEffects>();

                var soDst = new SerializedObject(targetFx);
                var soSrc = new SerializedObject(tmp);

                soDst.Update();
                soSrc.Update();

                for (int i = 0; i < props.Count; i++)
                {
                    var name = props[i];
                    var dst = soDst.FindProperty(name);
                    var src = soSrc.FindProperty(name);
                    if (dst == null || src == null) continue;

                    CopyPropertyValue(dst, src);
                }

                soDst.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetFx);
            }
            finally
            {
                DestroyImmediate(tmpGO);
            }
        }

        private static void CopyPropertyValue(SerializedProperty dst, SerializedProperty src)
        {
            if (dst == null || src == null) return;
            if (dst.propertyType != src.propertyType) return;

            if (dst.isArray && src.isArray && dst.propertyType != SerializedPropertyType.String)
            {
                dst.arraySize = src.arraySize;
                for (int i = 0; i < src.arraySize; i++)
                    CopyPropertyValue(dst.GetArrayElementAtIndex(i), src.GetArrayElementAtIndex(i));
                return;
            }

            switch (dst.propertyType)
            {
                case SerializedPropertyType.Integer: dst.intValue = src.intValue; break;
                case SerializedPropertyType.Boolean: dst.boolValue = src.boolValue; break;
                case SerializedPropertyType.Float: dst.floatValue = src.floatValue; break;
                case SerializedPropertyType.String: dst.stringValue = src.stringValue; break;
                case SerializedPropertyType.Color: dst.colorValue = src.colorValue; break;
                case SerializedPropertyType.ObjectReference: dst.objectReferenceValue = src.objectReferenceValue; break;
                case SerializedPropertyType.LayerMask: dst.intValue = src.intValue; break;
                case SerializedPropertyType.Enum: dst.enumValueIndex = src.enumValueIndex; break;
                case SerializedPropertyType.Vector2: dst.vector2Value = src.vector2Value; break;
                case SerializedPropertyType.Vector3: dst.vector3Value = src.vector3Value; break;
                case SerializedPropertyType.Vector4: dst.vector4Value = src.vector4Value; break;
                case SerializedPropertyType.Vector2Int: dst.vector2IntValue = src.vector2IntValue; break;
                case SerializedPropertyType.Vector3Int: dst.vector3IntValue = src.vector3IntValue; break;
                case SerializedPropertyType.Rect: dst.rectValue = src.rectValue; break;
                case SerializedPropertyType.RectInt: dst.rectIntValue = src.rectIntValue; break;
                case SerializedPropertyType.Bounds: dst.boundsValue = src.boundsValue; break;
                case SerializedPropertyType.BoundsInt: dst.boundsIntValue = src.boundsIntValue; break;
                case SerializedPropertyType.Quaternion: dst.quaternionValue = src.quaternionValue; break;
                case SerializedPropertyType.AnimationCurve: dst.animationCurveValue = src.animationCurveValue; break;
                case SerializedPropertyType.ExposedReference: dst.exposedReferenceValue = src.exposedReferenceValue; break;
                case SerializedPropertyType.ManagedReference: dst.managedReferenceValue = src.managedReferenceValue; break;

                case SerializedPropertyType.Generic:
                default:
                    var srcCopy = src.Copy();
                    var end = srcCopy.GetEndProperty();
                    bool enterChildren = true;

                    while (srcCopy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(srcCopy, end))
                    {
                        enterChildren = false;

                        if (!srcCopy.propertyPath.StartsWith(src.propertyPath, StringComparison.Ordinal))
                            continue;

                        var rel = srcCopy.propertyPath.Substring(src.propertyPath.Length);
                        if (rel.StartsWith(".")) rel = rel.Substring(1);

                        var dstChild = dst.serializedObject.FindProperty(dst.propertyPath + (string.IsNullOrEmpty(rel) ? "" : "." + rel));
                        if (dstChild == null) continue;
                        if (dstChild.propertyType != srcCopy.propertyType) continue;

                        CopyPropertyValue(dstChild, srcCopy);
                    }
                    break;
            }
        }

        // =============================================================================================
        // CUSTOM SECTION DRAWERS
        // =============================================================================================
        private void DrawMasterContent()
        {
            BeginSectionDrawn();

            var masterBlend = SP("masterBlend");
            if (PropMatchesSearch(masterBlend))
                EditorGUILayout.PropertyField(masterBlend, new GUIContent("Master Blend"));

            // FIX: always claim it (whether drawn or not) so auto-draw can’t duplicate it.
            MarkDrawnMany("masterBlend");

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("opacity global master"))
            {
                GUILayout.Space(6);
                ShowHint("Global opacity for the entire effect stack. Does not affect internal parameters.");
            }

            DrawAutoRemaining("Master");

            GUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (ResetPill("Reset ALL to Defaults", GUILayout.ExpandWidth(true)))
                {
                    if (EditorUtility.DisplayDialog("Reset Effects",
                        "Reset ALL values to factory defaults?\n\nThis cannot be undone.",
                        "Reset", "Cancel"))
                    {
                        ResetToDefaults();
                        RebuildAll();
                        GUI.FocusControl(null);
                    }
                }

                if (ResetPill("Randomize ALL", GUILayout.ExpandWidth(true)))
                {
                    if (EditorUtility.DisplayDialog("Randomize Effects",
                        "Randomize ALL values? This cannot be undone.\n\n(This will produce unpredictable effects, do NOT use if you suffer from epilepsy)",
                        "Randomize", "Cancel"))
                    {
                        RandomizeAllProperties();
                        GUI.FocusControl(null);
                    }
                }
            }
        }
            
        private Dictionary<string, FieldInfo> _fieldCache;

        private Dictionary<string, FieldInfo> GetFieldCache()
        {
            if (_fieldCache != null) return _fieldCache;
            _fieldCache = typeof(CrowImageEffects)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .ToDictionary(f => f.Name, StringComparer.Ordinal);
            return _fieldCache;
        }

        private void RandomizeSectionProperties(string sectionKey)
        {
            var targetFx = (CrowImageEffects)target;
            if (targetFx == null) return;

            if (!_propsBySection.TryGetValue(sectionKey, out var props) || props.Count == 0)
                return;

            Undo.RecordObject(targetFx, $"Randomize {sectionKey}");
            serializedObject.Update();
            RandomizeProps(props);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetFx);
        }

        private void RandomizeAllProperties()
        {
            var targetFx = (CrowImageEffects)target;
            if (targetFx == null) return;

            Undo.RecordObject(targetFx, "Randomize All");
            serializedObject.Update();

            foreach (var props in _propsBySection.Values)
                RandomizeProps(props);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetFx);
        }

        private void RandomizeProps(List<string> props)
        {
            var fields = GetFieldCache();

            foreach (var name in props)
            {
                var p = serializedObject.FindProperty(name);
                if (p == null) continue;

                RangeAttribute range = null;
                if (fields.TryGetValue(name, out var field))
                    range = field.GetCustomAttribute<RangeAttribute>();

                RandomizeProperty(p, range);
            }
        }

        private static void RandomizeProperty(SerializedProperty p, RangeAttribute range)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    p.floatValue = range != null
                        ? UnityEngine.Random.Range(range.min, range.max)
                        : UnityEngine.Random.Range(0f, Mathf.Max(1f, Mathf.Abs(p.floatValue) * 2f));
                    break;

                case SerializedPropertyType.Integer:
                    p.intValue = range != null
                        ? UnityEngine.Random.Range((int)range.min, (int)range.max + 1)
                        : UnityEngine.Random.Range(0, Mathf.Max(4, Mathf.Abs(p.intValue) * 2));
                    break;

                case SerializedPropertyType.Boolean:
                    p.boolValue = UnityEngine.Random.value > 0.5f;
                    break;

                case SerializedPropertyType.Vector2:
                    p.vector2Value = range != null
                        ? new Vector2(UnityEngine.Random.Range(range.min, range.max),
                                    UnityEngine.Random.Range(range.min, range.max))
                        : UnityEngine.Random.insideUnitCircle;
                    break;

                case SerializedPropertyType.Color:
                    p.colorValue = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
                    break;
            }
        }
        private void DrawPregradeContent()
        {
            BeginSectionDrawn();

            var pregradeEnabled = SP("pregradeEnabled");
            var exposure = SP("exposure");
            var contrast = SP("contrast");
            var gamma = SP("gamma");
            var saturation = SP("saturation");

            if (PropMatchesSearch(pregradeEnabled))
                EditorGUILayout.PropertyField(pregradeEnabled, new GUIContent("Enable Pre-grade"));

            using (new EnabledScope(pregradeEnabled != null && pregradeEnabled.boolValue))
            {
                if (PropMatchesSearch(exposure))   EditorGUILayout.PropertyField(exposure);
                if (PropMatchesSearch(contrast))   EditorGUILayout.PropertyField(contrast);
                if (PropMatchesSearch(gamma))      EditorGUILayout.PropertyField(gamma);
                if (PropMatchesSearch(saturation)) EditorGUILayout.PropertyField(saturation);
            }

            // FIX: claim all manual-managed props regardless of toggles
            MarkDrawnMany("pregradeEnabled", "exposure", "contrast", "gamma", "saturation");

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("pregrade quantization exposure contrast gamma saturation"))
            {
                GUILayout.Space(6);
                ShowHint("Applied before quantization.");
            }

            DrawAutoRemaining("Pregrade");
        }

        private void DrawSamplingContent()
        {
            BeginSectionDrawn();

            var pixelSize = SP("pixelSize");
            var useVirtualGrid = SP("useVirtualGrid");
            var virtualResolution = SP("virtualResolution");

            if (PropMatchesSearch(pixelSize))
                EditorGUILayout.PropertyField(pixelSize, new GUIContent("Pixel Size"));

            GUILayout.Space(6);

            if (PropMatchesSearch(useVirtualGrid))
                EditorGUILayout.PropertyField(useVirtualGrid, new GUIContent("Use Virtual Grid"));

            bool grid = useVirtualGrid != null && useVirtualGrid.boolValue;

            if (grid)
            {
                if (PropMatchesSearch(virtualResolution))
                    EditorGUILayout.PropertyField(virtualResolution, new GUIContent("Virtual Resolution"));

                GUILayout.Space(6);

                bool showPresets = string.IsNullOrWhiteSpace(_search) || PassesSearch("resolution preset grid virtual 240 288 320 360 448 480 576 720 1080 160 200 224 256 300 384 400 512 600 768 854 960 1024 1366");
                if (showPresets)
                {
                    DrawSubSection(
                        title: "Resolution Presets",
                        icon: "d_GridLayoutGroup Icon",
                        fold: _foldResolutionPresets,
                        hint: "Quick set",
                        drawContent: DrawResolutionPresets
                    );

                    GUILayout.Space(6);
                    ShowHint("Fixes sampling to a stable grid, preventing resolution-dependent flickering.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("screen backbuffer shimmer resize"))
                    ShowHint("Off = sampling follows the backbuffer resolution (may cause shimmering on resize).");
            }

            MarkDrawnMany("pixelSize", "useVirtualGrid", "virtualResolution");

            DrawAutoRemaining("Sampling");
        }

        private void DrawPosterizeContent()
        {
            BeginSectionDrawn();

            var usePerChannel = SP("usePerChannel");
            var levels = SP("levels");
            var levelsR = SP("levelsR");
            var levelsG = SP("levelsG");
            var levelsB = SP("levelsB");

            var animateLevels = SP("animateLevels");
            var minLevels = SP("minLevels");
            var maxLevels = SP("maxLevels");
            var speed = SP("speed");

            var luminanceOnly = SP("luminanceOnly");
            var invert = SP("invert");

            if (PropMatchesSearch(usePerChannel))
                EditorGUILayout.PropertyField(usePerChannel, new GUIContent("Per-Channel Levels"));

            bool perCh = usePerChannel != null && usePerChannel.boolValue;

            if (perCh)
            {
                if (levelsR != null && PropMatchesSearch(levelsR)) { levelsR.intValue = EditorGUILayout.IntSlider("Red", levelsR.intValue, 2, 512); }
                if (levelsG != null && PropMatchesSearch(levelsG)) { levelsG.intValue = EditorGUILayout.IntSlider("Green", levelsG.intValue, 2, 512); }
                if (levelsB != null && PropMatchesSearch(levelsB)) { levelsB.intValue = EditorGUILayout.IntSlider("Blue", levelsB.intValue, 2, 512); }

                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("per-channel quantization color shifting"))
                    ShowHint("Separate quantization per channel can create color-shifting effects.");
            }
            else
            {
                if (levels != null && PropMatchesSearch(levels)) { levels.intValue = EditorGUILayout.IntSlider("Levels", levels.intValue, 2, 512); }

                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("banding gradients quantization levels"))
                    ShowHint("Lower values create more pronounced banding. Higher = smoother gradients.");
            }

            GUILayout.Space(8);

            if (PropMatchesSearch(luminanceOnly)) EditorGUILayout.PropertyField(luminanceOnly, new GUIContent("Luminance Only"));
            if (PropMatchesSearch(invert))        EditorGUILayout.PropertyField(invert, new GUIContent("Invert"));

            GUILayout.Space(8);

            if (PropMatchesSearch(animateLevels)) EditorGUILayout.PropertyField(animateLevels, new GUIContent("Animate Levels"));

            bool anim = animateLevels != null && animateLevels.boolValue;
            if (anim)
            {
                float min = minLevels != null ? minLevels.intValue : 2;
                float max = maxLevels != null ? maxLevels.intValue : 2;

                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("range min max slider animate"))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(new GUIContent($"Range ({min:0}–{max:0})"), ref min, ref max, 2f, 512f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (minLevels != null) { minLevels.intValue = Mathf.Clamp(Mathf.RoundToInt(min), 2, 512); }
                        if (maxLevels != null) { maxLevels.intValue = Mathf.Clamp(Mathf.RoundToInt(max), 2, 512); }
                    }

                    if (PropMatchesSearch(speed)) EditorGUILayout.PropertyField(speed, new GUIContent("Animation Speed"));

                    if (string.IsNullOrWhiteSpace(_search) || PassesSearch("retro shimmer cycles"))
                        ShowHint("Cycles quantization levels over time for a retro shimmer effect.");
                }
            }

            // FIX: claim *all* of these always, so the “other branch” never leaks into auto-draw
            MarkDrawnMany(
                "usePerChannel",
                "levels", "levelsR", "levelsG", "levelsB",
                "luminanceOnly", "invert",
                "animateLevels", "minLevels", "maxLevels", "speed"
            );

            DrawAutoRemaining("Posterize");
        }

        private void DrawPaletteContent()
        {
            BeginSectionDrawn();

            var thresholdCurve = SP("thresholdCurve");
            var usePalette = SP("usePalette");
            var paletteTex = SP("paletteTex");

            if (PropMatchesSearch(thresholdCurve))
                EditorGUILayout.PropertyField(thresholdCurve, new GUIContent("Threshold Curve"));

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("curve tonal remap palette lookup"))
                ShowHint("Remaps tonal range before palette lookup. Use to bias towards lights or darks.");

            GUILayout.Space(6);

            if (PropMatchesSearch(usePalette))
                EditorGUILayout.PropertyField(usePalette, new GUIContent("Use Palette"));

            if (usePalette != null && usePalette.boolValue)
            {
                if (PropMatchesSearch(paletteTex))
                    EditorGUILayout.PropertyField(paletteTex, new GUIContent("Palette Texture"));

                if (paletteTex != null && paletteTex.objectReferenceValue == null)
                {
                    if (string.IsNullOrWhiteSpace(_search) || PassesSearch("missing texture palette"))
                        ShowHint("Palette enabled but no texture assigned.", HintType.Warning);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(_search) || PassesSearch("maps final colors palette"))
                        ShowHint("Maps final colors through the provided palette texture.");
                }
            }

            MarkDrawnMany("thresholdCurve", "usePalette", "paletteTex");
            DrawAutoRemaining("Palette");
        }

        private void DrawMaskingContent()
        {
            BeginSectionDrawn();

            var useMask = SP("useMask");
            var maskTex = SP("maskTex");
            var maskThreshold = SP("maskThreshold");

            if (PropMatchesSearch(useMask))
                EditorGUILayout.PropertyField(useMask, new GUIContent("Enable Texture Mask"));

            if (useMask != null && useMask.boolValue)
            {
                if (PropMatchesSearch(maskTex))       EditorGUILayout.PropertyField(maskTex, new GUIContent("Mask Texture"));
                if (PropMatchesSearch(maskThreshold)) EditorGUILayout.PropertyField(maskThreshold, new GUIContent("Mask Threshold"));

                if (maskTex != null && maskTex.objectReferenceValue == null)
                    ShowHint("Mask enabled but texture is missing.", HintType.Warning);
                else
                    ShowHint("White = effect applied, Black = original image (threshold determines cutoff).");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("grayscale texture selectively apply ui safe"))
                    ShowHint("Use a grayscale texture to selectively apply effects (great for UI-safe zones).");
            }

            MarkDrawnMany("useMask", "maskTex", "maskThreshold");
            DrawAutoRemaining("TextureMask");
        }

        private void DrawDepthMaskContent()
        {
            BeginSectionDrawn();

            var useDepthMask = SP("useDepthMask");
            var depthThreshold = SP("depthThreshold");

            if (PropMatchesSearch(useDepthMask))
                EditorGUILayout.PropertyField(useDepthMask, new GUIContent("Enable Depth Mask"));

            if (useDepthMask != null && useDepthMask.boolValue)
            {
                if (PropMatchesSearch(depthThreshold))
                    EditorGUILayout.PropertyField(depthThreshold, new GUIContent("Depth Threshold"));

                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("attenuates distance depth texture"))
                    ShowHint("Attenuates effects based on distance from camera. Automatically enables depth texture.");
            }

            MarkDrawnMany("useDepthMask", "depthThreshold");
            DrawAutoRemaining("DepthMask");
        }

        private void DrawJitterContent()
        {
            BeginSectionDrawn();

            var pEnabled  = SP("jitterEnabled");
            var pStrength = SP("jitterStrength");
            var pAmountPx = SP("jitterAmountPx");
            var pMode     = SP("jitterMode");
            var pSpeed    = SP("jitterSpeed");
            var pUseSeed  = SP("jitterUseSeed");
            var pSeed     = SP("jitterSeed");
            var pScanline = SP("jitterScanline");
            var pNoiseTex = SP("jitterNoiseTex");
            var pClampUV  = SP("jitterClampUV");
            var pWeights  = SP("jitterChannelWeights");
            var pDirR     = SP("jitterDirR");
            var pDirG     = SP("jitterDirG");
            var pDirB     = SP("jitterDirB");

            if (PropMatchesSearch(pEnabled))
                EditorGUILayout.PropertyField(pEnabled, new GUIContent("Enable"));

            bool enabled = pEnabled != null && pEnabled.boolValue;

            using (new EnabledScope(enabled))
            {
                GUILayout.Space(6);
                DrawJitterStrengthAndAmount(pStrength, pAmountPx);
                GUILayout.Space(6);
                DrawJitterModeAndSpeed(pMode, pSpeed, pNoiseTex);
                GUILayout.Space(6);
                DrawJitterSeed(pUseSeed, pSeed);
                GUILayout.Space(6);
                DrawJitterScanline(pScanline);
                GUILayout.Space(6);

                if (PropMatchesSearch(pClampUV))
                    EditorGUILayout.PropertyField(pClampUV, new GUIContent("Clamp UV"));

                bool showAdvanced = string.IsNullOrWhiteSpace(_search)
                                    || PassesSearch("advanced weights dir direction channel")
                                    || AnyMatch(pWeights, pDirR, pDirG, pDirB);

                if (showAdvanced)
                {
                    GUILayout.Space(8);
                    DrawSubSection("Advanced", "d_ToolHandleGlobal", _foldJitterAdvanced,
                        () => DrawJitterAdvanced(pWeights, pDirR, pDirG, pDirB), "weights + dirs");
                }

                if (string.IsNullOrWhiteSpace(_search))
                {
                    GUILayout.Space(6);
                    ShowHint(enabled
                        ? "Strength blends between base and jittered sampling. Amount (px) is the actual offset scale."
                        : "Enable to apply subtle per-channel sampling offsets.");
                }
            }

            MarkDrawnMany(
                "jitterEnabled", "jitterStrength", "jitterAmountPx", "jitterMode", "jitterSpeed",
                "jitterUseSeed", "jitterSeed",
                "jitterScanline", "jitterScanlineDensity", "jitterScanlineAmp",
                "jitterChannelWeights", "jitterDirR", "jitterDirG", "jitterDirB",
                "jitterNoiseTex", "jitterClampUV"
            );

            DrawAutoRemaining("Jitter");
        }

        private void DrawJitterStrengthAndAmount(SerializedProperty pStrength, SerializedProperty pAmountPx)
        {
            if (pStrength != null && PropMatchesSearch(pStrength))
            {
                EditorGUI.BeginChangeCheck();
                float v = EditorGUILayout.Slider(new GUIContent("Strength"), pStrength.floatValue, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) pStrength.floatValue = v;
            }

            if (pAmountPx == null || !PropMatchesSearch(pAmountPx)) return;

            EditorGUI.BeginChangeCheck();
            if (pAmountPx.propertyType == SerializedPropertyType.Float)
            {
                float v = EditorGUILayout.Slider(new GUIContent("Amount (px)"), pAmountPx.floatValue, 0f, 8f);
                if (EditorGUI.EndChangeCheck()) pAmountPx.floatValue = v;
            }
            else if (pAmountPx.propertyType == SerializedPropertyType.Integer)
            {
                int v = EditorGUILayout.IntSlider(new GUIContent("Amount (px)"), pAmountPx.intValue, 0, 16);
                if (EditorGUI.EndChangeCheck()) pAmountPx.intValue = v;
            }
            else
            {
                EditorGUILayout.PropertyField(pAmountPx, new GUIContent("Amount (px)"));
            }
        }

        private void DrawJitterModeAndSpeed(SerializedProperty pMode, SerializedProperty pSpeed, SerializedProperty pNoiseTex)
        {
            if (pMode != null && PropMatchesSearch(pMode))
                EditorGUILayout.PropertyField(pMode, new GUIContent("Mode"));

            bool showSpeed = pMode == null || pMode.enumValueIndex != 0;
            if (showSpeed && pSpeed != null && PropMatchesSearch(pSpeed))
            {
                EditorGUI.BeginChangeCheck();
                if (pSpeed.propertyType == SerializedPropertyType.Float)
                {
                    float v = EditorGUILayout.Slider(new GUIContent("Speed"), pSpeed.floatValue, 0f, 20f);
                    if (EditorGUI.EndChangeCheck()) pSpeed.floatValue = v;
                }
                else if (pSpeed.propertyType == SerializedPropertyType.Integer)
                {
                    int v = EditorGUILayout.IntSlider(new GUIContent("Speed"), pSpeed.intValue, 0, 20);
                    if (EditorGUI.EndChangeCheck()) pSpeed.intValue = v;
                }
                else
                {
                    EditorGUILayout.PropertyField(pSpeed, new GUIContent("Speed"));
                }
            }

            bool needsNoise = pMode != null && pMode.enumValueIndex == 3;
            if (!needsNoise) return;

            GUILayout.Space(6);
            if (pNoiseTex != null && PropMatchesSearch(pNoiseTex))
                EditorGUILayout.PropertyField(pNoiseTex, new GUIContent("Noise Tex"));

            if (pNoiseTex != null && pNoiseTex.objectReferenceValue == null)
                ShowHint("BlueNoiseTex mode: assign a noise texture (128×128+ recommended).", HintType.Warning);
        }

        private void DrawJitterSeed(SerializedProperty pUseSeed, SerializedProperty pSeed)
        {
            if (pUseSeed != null && PropMatchesSearch(pUseSeed))
                EditorGUILayout.PropertyField(pUseSeed, new GUIContent("Use Seed"));

            if (pUseSeed == null || !pUseSeed.boolValue) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.VerticalScope())
                {
                    if (pSeed != null && PropMatchesSearch(pSeed))
                        EditorGUILayout.PropertyField(pSeed, new GUIContent("Seed"));
                }
            }
        }

        private void DrawJitterScanline(SerializedProperty pScanline)
        {
            if (pScanline != null && PropMatchesSearch(pScanline))
                EditorGUILayout.PropertyField(pScanline, new GUIContent("Scanline"));

            if (pScanline == null || !pScanline.boolValue) return;

            var pScanDensity = SP("jitterScanlineDensity");
            var pScanAmp     = SP("jitterScanlineAmp");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.VerticalScope())
                {
                    if (pScanDensity != null && PropMatchesSearch(pScanDensity))
                    {
                        if (pScanDensity.propertyType == SerializedPropertyType.Float)
                            pScanDensity.floatValue = EditorGUILayout.Slider(new GUIContent("Density"), pScanDensity.floatValue, 64f, 2048f);
                        else if (pScanDensity.propertyType == SerializedPropertyType.Integer)
                            pScanDensity.intValue = EditorGUILayout.IntSlider(new GUIContent("Density"), pScanDensity.intValue, 64, 2048);
                        else
                            EditorGUILayout.PropertyField(pScanDensity, new GUIContent("Density"));
                    }

                    if (pScanAmp != null && PropMatchesSearch(pScanAmp))
                    {
                        if (pScanAmp.propertyType == SerializedPropertyType.Float)
                            pScanAmp.floatValue = EditorGUILayout.Slider(new GUIContent("Amp"), pScanAmp.floatValue, 0f, 2f);
                        else
                            EditorGUILayout.PropertyField(pScanAmp, new GUIContent("Amp"));
                    }
                }
            }
        }

        private void DrawJitterAdvanced(SerializedProperty pWeights, SerializedProperty pDirR,
                                        SerializedProperty pDirG,   SerializedProperty pDirB)
        {
            if (pWeights != null && PropMatchesSearch(pWeights)) EditorGUILayout.PropertyField(pWeights, new GUIContent("Channel Weights"));
            if (pDirR    != null && PropMatchesSearch(pDirR))    EditorGUILayout.PropertyField(pDirR,    new GUIContent("Dir R"));
            if (pDirG    != null && PropMatchesSearch(pDirG))    EditorGUILayout.PropertyField(pDirG,    new GUIContent("Dir G"));
            if (pDirB    != null && PropMatchesSearch(pDirB))    EditorGUILayout.PropertyField(pDirB,    new GUIContent("Dir B"));
        }

        private void DrawBleedContent()
        {
            BeginSectionDrawn();

            var bleedBlend     = SP("bleedBlend");
            var bleedIntensity = SP("bleedIntensity");

            if (PropMatchesSearch(bleedBlend))     EditorGUILayout.PropertyField(bleedBlend,     new GUIContent("Blend"));
            if (PropMatchesSearch(bleedIntensity)) EditorGUILayout.PropertyField(bleedIntensity, new GUIContent("Intensity"));

            bool active = bleedBlend != null && bleedBlend.floatValue > 0f &&
                        bleedIntensity != null && bleedIntensity.floatValue > 0f;

            GUILayout.Space(6);

            var bleedMode     = SP("bleedMode");
            var bleedBlendMode = SP("bleedBlendMode");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedMode, bleedBlendMode))
                DrawSubSection("Mode & Combine",              "d_Rigidbody Icon",         _foldBleedModeCombine, () => DrawBleedModeCombine(active),    "how to shift");

            var shiftR = SP("shiftR"); var shiftG = SP("shiftG"); var shiftB = SP("shiftB");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(shiftR, shiftG, shiftB))
                DrawSubSection("Manual Shifts",               "d_MoveTool",               _foldBleedManual,      () => DrawBleedManual(active),         "R/G/B vectors");

            var bleedRadialCenter = SP("bleedRadialCenter"); var bleedRadialStrength = SP("bleedRadialStrength");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedRadialCenter, bleedRadialStrength))
                DrawSubSection("Radial Mode",                 "d_TransformTool On",       _foldBleedRadial,      () => DrawBleedRadial(active),         "center + strength");

            var bleedEdgeOnly = SP("bleedEdgeOnly"); var bleedEdgeThreshold = SP("bleedEdgeThreshold"); var bleedEdgePower = SP("bleedEdgePower");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedEdgeOnly, bleedEdgeThreshold, bleedEdgePower))
                DrawSubSection("Edge Gating",                 "d_SceneViewFx",            _foldBleedEdge,        () => DrawBleedEdge(active),           "only on edges");

            var bleedSamples = SP("bleedSamples"); var bleedSmear = SP("bleedSmear"); var bleedFalloff = SP("bleedFalloff");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedSamples, bleedSmear, bleedFalloff))
                DrawSubSection("Smear / Multi-tap",           "d_PreTextureMipMapHigh",   _foldBleedSmear,       () => DrawBleedSmear(active),          "samples + length");

            var bleedIntensityR = SP("bleedIntensityR"); var bleedIntensityG = SP("bleedIntensityG");
            var bleedIntensityB = SP("bleedIntensityB"); var bleedAnamorphic = SP("bleedAnamorphic");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedIntensityR, bleedIntensityG, bleedIntensityB, bleedAnamorphic))
                DrawSubSection("Per-channel Intensity & Shape","d_PreMatSphere",           _foldBleedPerChannel,  () => DrawBleedPerChannel(active),     "R/G/B gain");

            var bleedClampUV = SP("bleedClampUV"); var bleedPreserveLuma = SP("bleedPreserveLuma");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedClampUV, bleedPreserveLuma))
                DrawSubSection("Safety / Luma",               "d_console.warnicon",       _foldBleedSafety,      () => DrawBleedSafety(active),         "clamp + luma");

            var bleedWobbleAmp = SP("bleedWobbleAmp"); var bleedWobbleFreq = SP("bleedWobbleFreq"); var bleedWobbleScanline = SP("bleedWobbleScanline");
            if (string.IsNullOrWhiteSpace(_search) || AnyMatch(bleedWobbleAmp, bleedWobbleFreq, bleedWobbleScanline))
                DrawSubSection("Wobble",                      "d_FilterByLabel",          _foldBleedWobble,      () => DrawBleedWobble(active),         "VHS drift");

            MarkDrawnMany(
                "bleedBlend", "bleedIntensity", "bleedMode", "bleedBlendMode",
                "shiftR", "shiftG", "shiftB",
                "bleedEdgeOnly", "bleedEdgeThreshold", "bleedEdgePower",
                "bleedRadialCenter", "bleedRadialStrength",
                "bleedSamples", "bleedSmear", "bleedFalloff",
                "bleedIntensityR", "bleedIntensityG", "bleedIntensityB", "bleedAnamorphic",
                "bleedClampUV", "bleedPreserveLuma",
                "bleedWobbleAmp", "bleedWobbleFreq", "bleedWobbleScanline"
            );

            GUILayout.Space(6);
            DrawAutoRemaining("Bleed");
        }

        private void DrawBleedModeCombine(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedMode     = SP("bleedMode");
                var bleedBlendMode = SP("bleedBlendMode");
                if (PropMatchesSearch(bleedMode))      EditorGUILayout.PropertyField(bleedMode,      new GUIContent("Bleed Mode"));
                if (PropMatchesSearch(bleedBlendMode)) EditorGUILayout.PropertyField(bleedBlendMode, new GUIContent("Blend Mode"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("active inactive blend intensity"))
                ShowHint(active ? "Active." : "Inactive until both Blend and Intensity are > 0.");
        }

        private void DrawBleedManual(bool active)
        {
            using (new EnabledScope(active))
            {
                var shiftR = SP("shiftR"); var shiftG = SP("shiftG"); var shiftB = SP("shiftB");
                if (PropMatchesSearch(shiftR)) EditorGUILayout.PropertyField(shiftR, new GUIContent("Shift R"));
                if (PropMatchesSearch(shiftG)) EditorGUILayout.PropertyField(shiftG, new GUIContent("Shift G"));
                if (PropMatchesSearch(shiftB)) EditorGUILayout.PropertyField(shiftB, new GUIContent("Shift B"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("uv offsets pixel-space"))
                ShowHint("Per-channel UV offsets (pixel-space once multiplied by intensity/texel size).");
        }

        private void DrawBleedRadial(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedRadialCenter   = SP("bleedRadialCenter");
                var bleedRadialStrength = SP("bleedRadialStrength");
                if (PropMatchesSearch(bleedRadialCenter))   EditorGUILayout.PropertyField(bleedRadialCenter,   new GUIContent("Radial Center"));
                if (PropMatchesSearch(bleedRadialStrength)) EditorGUILayout.PropertyField(bleedRadialStrength, new GUIContent("Radial Strength"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("push chromatic separation outward"))
                ShowHint("Push chromatic separation outward from a center point.");
        }

        private void DrawBleedEdge(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedEdgeOnly      = SP("bleedEdgeOnly");
                var bleedEdgeThreshold = SP("bleedEdgeThreshold");
                var bleedEdgePower     = SP("bleedEdgePower");
                if (PropMatchesSearch(bleedEdgeOnly)) EditorGUILayout.PropertyField(bleedEdgeOnly, new GUIContent("Edge Only"));
                using (new EnabledScope(bleedEdgeOnly != null && bleedEdgeOnly.boolValue))
                {
                    if (PropMatchesSearch(bleedEdgeThreshold)) EditorGUILayout.PropertyField(bleedEdgeThreshold, new GUIContent("Edge Threshold"));
                    if (PropMatchesSearch(bleedEdgePower))     EditorGUILayout.PropertyField(bleedEdgePower,     new GUIContent("Edge Power"));
                }
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("high-contrast edges cleaner separation"))
                ShowHint("Restricts bleed to high-contrast edges for cleaner separation.");
        }

        private void DrawBleedSmear(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedSamples = SP("bleedSamples");
                var bleedSmear   = SP("bleedSmear");
                var bleedFalloff = SP("bleedFalloff");
                if (PropMatchesSearch(bleedSamples)) EditorGUILayout.PropertyField(bleedSamples, new GUIContent("Samples"));
                if (PropMatchesSearch(bleedSmear))   EditorGUILayout.PropertyField(bleedSmear,   new GUIContent("Smear"));
                if (PropMatchesSearch(bleedFalloff)) EditorGUILayout.PropertyField(bleedFalloff, new GUIContent("Falloff"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("multi-sample trails cost"))
                ShowHint("Multi-sample trails (cost scales with Samples).");
        }

        private void DrawBleedPerChannel(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedIntensityR = SP("bleedIntensityR");
                var bleedIntensityG = SP("bleedIntensityG");
                var bleedIntensityB = SP("bleedIntensityB");
                var bleedAnamorphic = SP("bleedAnamorphic");
                if (PropMatchesSearch(bleedIntensityR)) EditorGUILayout.PropertyField(bleedIntensityR, new GUIContent("Intensity R"));
                if (PropMatchesSearch(bleedIntensityG)) EditorGUILayout.PropertyField(bleedIntensityG, new GUIContent("Intensity G"));
                if (PropMatchesSearch(bleedIntensityB)) EditorGUILayout.PropertyField(bleedIntensityB, new GUIContent("Intensity B"));
                if (PropMatchesSearch(bleedAnamorphic)) EditorGUILayout.PropertyField(bleedAnamorphic, new GUIContent("Anamorphic"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("fine-tune channel separation stretch"))
                ShowHint("Fine-tune channel separation + stretch horizontally/vertically.");
        }

        private void DrawBleedSafety(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedClampUV      = SP("bleedClampUV");
                var bleedPreserveLuma = SP("bleedPreserveLuma");
                if (PropMatchesSearch(bleedClampUV))      EditorGUILayout.PropertyField(bleedClampUV,      new GUIContent("Clamp UV"));
                if (PropMatchesSearch(bleedPreserveLuma)) EditorGUILayout.PropertyField(bleedPreserveLuma, new GUIContent("Preserve Luma"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("sampling outside screen stabilizes brightness"))
                ShowHint("Clamp avoids sampling outside screen. Preserve luma stabilizes brightness.");
        }

        private void DrawBleedWobble(bool active)
        {
            using (new EnabledScope(active))
            {
                var bleedWobbleAmp      = SP("bleedWobbleAmp");
                var bleedWobbleFreq     = SP("bleedWobbleFreq");
                var bleedWobbleScanline = SP("bleedWobbleScanline");
                if (PropMatchesSearch(bleedWobbleAmp))      EditorGUILayout.PropertyField(bleedWobbleAmp,      new GUIContent("Wobble Amp"));
                if (PropMatchesSearch(bleedWobbleFreq))     EditorGUILayout.PropertyField(bleedWobbleFreq,     new GUIContent("Wobble Freq"));
                if (PropMatchesSearch(bleedWobbleScanline)) EditorGUILayout.PropertyField(bleedWobbleScanline, new GUIContent("Scanline Wobble"));
            }
            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("animated drift subtle"))
                ShowHint("Animated drift (keep subtle).");
        }

        private bool AnyMatch(params SerializedProperty[] props)
        {
            for (int i = 0; i < props.Length; i++)
                if (props[i] != null && PropMatchesSearch(props[i])) return true;
            return false;
        }

        private void DrawGhostContent()
        {
            BeginSectionDrawn();

            var ghostEnabled = SP("ghostEnabled");
            var ghostBlend = SP("ghostBlend");
            var ghostCombineMode = SP("ghostCombineMode");
            var ghostOffsetPx = SP("ghostOffsetPx");
            var ghostFrames = SP("ghostFrames");
            var ghostCaptureInterval = SP("ghostCaptureInterval");
            var ghostStartDelay = SP("ghostStartDelay");
            var ghostWeightCurve = SP("ghostWeightCurve");

            if (PropMatchesSearch(ghostEnabled))
                EditorGUILayout.PropertyField(ghostEnabled, new GUIContent("Enable Ghosting"));

            using (new EnabledScope(ghostEnabled != null && ghostEnabled.boolValue))
            {
                if (PropMatchesSearch(ghostBlend))           EditorGUILayout.PropertyField(ghostBlend, new GUIContent("Amount"));
                if (PropMatchesSearch(ghostCombineMode))     EditorGUILayout.PropertyField(ghostCombineMode, new GUIContent("Combine Mode"));
                if (PropMatchesSearch(ghostOffsetPx))        EditorGUILayout.PropertyField(ghostOffsetPx, new GUIContent("Offset (px)"));
                GUILayout.Space(6);
                if (PropMatchesSearch(ghostFrames))          EditorGUILayout.PropertyField(ghostFrames, new GUIContent("History Frames"));
                if (PropMatchesSearch(ghostCaptureInterval)) EditorGUILayout.PropertyField(ghostCaptureInterval, new GUIContent("Capture Interval"));
                if (PropMatchesSearch(ghostStartDelay))      EditorGUILayout.PropertyField(ghostStartDelay, new GUIContent("Start Delay"));
                if (PropMatchesSearch(ghostWeightCurve))     EditorGUILayout.PropertyField(ghostWeightCurve, new GUIContent("Weight Curve"));
            }

            MarkDrawnMany(
                "ghostEnabled",
                "ghostBlend", "ghostCombineMode", "ghostOffsetPx",
                "ghostFrames", "ghostCaptureInterval", "ghostStartDelay", "ghostWeightCurve"
            );

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("ghost frames weighted composite curve"))
            {
                ShowHint(ghostEnabled != null && ghostEnabled.boolValue
                    ? "Blends a weighted composite of previous frames. Higher weight curve = favors newer frames."
                    : "Enable to blend previous frames for ghosting.");
            }

            DrawAutoRemaining("Ghost");
        }

        private void DrawEdgeContent()
        {
            BeginSectionDrawn();

            var edgeEnabled = SP("edgeEnabled");
            var edgeStrength = SP("edgeStrength");
            var edgeThreshold = SP("edgeThreshold");
            var edgeBlend = SP("edgeBlend");
            var edgeColor = SP("edgeColor");

            if (PropMatchesSearch(edgeEnabled))
                EditorGUILayout.PropertyField(edgeEnabled, new GUIContent("Enable Edges"));

            using (new EnabledScope(edgeEnabled != null && edgeEnabled.boolValue))
            {
                if (PropMatchesSearch(edgeStrength))  EditorGUILayout.PropertyField(edgeStrength, new GUIContent("Strength"));
                if (PropMatchesSearch(edgeThreshold)) EditorGUILayout.PropertyField(edgeThreshold, new GUIContent("Threshold"));
                if (PropMatchesSearch(edgeBlend))     EditorGUILayout.PropertyField(edgeBlend, new GUIContent("Blend"));
                if (PropMatchesSearch(edgeColor))     EditorGUILayout.PropertyField(edgeColor, new GUIContent("Color"));
            }

            // FIX: claim always
            MarkDrawnMany("edgeEnabled", "edgeStrength", "edgeThreshold", "edgeBlend", "edgeColor");

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("depth outline requires camera depth"))
                ShowHint("Depth-based outline effect. Requires camera depth.");

            DrawAutoRemaining("Edges");
        }

        private void DrawUnsharpContent()
        {
            BeginSectionDrawn();

            var unsharpEnabled = SP("unsharpEnabled");
            var unsharpAmount = SP("unsharpAmount");
            var unsharpRadius = SP("unsharpRadius");
            var unsharpThreshold = SP("unsharpThreshold");
            var unsharpLumaOnly = SP("unsharpLumaOnly");
            var unsharpChroma = SP("unsharpChroma");

            if (PropMatchesSearch(unsharpEnabled))
                EditorGUILayout.PropertyField(unsharpEnabled, new GUIContent("Enable Unsharp Mask"));

            using (new EnabledScope(unsharpEnabled != null && unsharpEnabled.boolValue))
            {
                if (PropMatchesSearch(unsharpAmount))    EditorGUILayout.PropertyField(unsharpAmount, new GUIContent("Amount"));
                if (PropMatchesSearch(unsharpRadius))    EditorGUILayout.PropertyField(unsharpRadius, new GUIContent("Radius"));
                if (PropMatchesSearch(unsharpThreshold)) EditorGUILayout.PropertyField(unsharpThreshold, new GUIContent("Threshold"));

                GUILayout.Space(6);

                if (PropMatchesSearch(unsharpLumaOnly)) EditorGUILayout.PropertyField(unsharpLumaOnly, new GUIContent("Luma Only"));
                using (new EnabledScope(unsharpLumaOnly != null && unsharpLumaOnly.boolValue))
                {
                    if (unsharpChroma != null && PropMatchesSearch(unsharpChroma))
                        EditorGUILayout.Slider(unsharpChroma, 0f, 1f, new GUIContent("Chroma Sharpen"));
                }
            }

            // FIX: claim always
            MarkDrawnMany("unsharpEnabled", "unsharpAmount", "unsharpRadius", "unsharpThreshold", "unsharpLumaOnly", "unsharpChroma");

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("subtracts blurred threshold noise"))
                ShowHint("Subtracts a blurred version. Threshold helps avoid amplifying noise.");

            DrawAutoRemaining("Unsharp");
        }

        private void DrawDitherContent()
        {
            BeginSectionDrawn();

            var ditherMode = SP("ditherMode");
            var ditherStrength = SP("ditherStrength");
            var blueNoise = SP("blueNoise");

            if (PropMatchesSearch(ditherMode))
                EditorGUILayout.PropertyField(ditherMode, new GUIContent("Dither Mode"));

            bool hasDither = ditherMode != null && ditherMode.enumValueIndex != (int)CrowImageEffects.DitherMode.None;

            using (new EnabledScope(hasDither))
            {
                if (PropMatchesSearch(ditherStrength))
                    EditorGUILayout.PropertyField(ditherStrength, new GUIContent("Strength"));

                bool needsBlueNoise = ditherMode != null && ditherMode.enumValueIndex == (int)CrowImageEffects.DitherMode.BlueNoise;
                if (needsBlueNoise)
                {
                    if (PropMatchesSearch(blueNoise))
                        EditorGUILayout.PropertyField(blueNoise, new GUIContent("Blue Noise Texture"));

                    if (blueNoise != null && blueNoise.objectReferenceValue == null)
                        ShowHint("Blue noise requires a texture (typically 128×128).", HintType.Error);
                    else
                        ShowHint("Blue noise provides more organic grain than Bayer patterns.");
                }
                else if (hasDither)
                {
                    if (string.IsNullOrWhiteSpace(_search) || PassesSearch("noise before quantization banding"))
                        ShowHint("Adds structured noise before quantization to reduce banding.");
                }
            }

            if (!hasDither)
            {
                if (string.IsNullOrWhiteSpace(_search) || PassesSearch("off quantization banding"))
                    ShowHint("Off = pure quantization (may exhibit visible banding).");
            }

            // FIX: claim always (blueNoise must not leak when not in BlueNoise mode)
            MarkDrawnMany("ditherMode", "ditherStrength", "blueNoise");

            DrawAutoRemaining("Dither");
        }

        private void DrawShadersContent()
        {
            BeginSectionDrawn();

            if (string.IsNullOrWhiteSpace(_search) || PassesSearch("shader path auto-find renamed"))
                ShowHint("Leave empty to auto-find shaders by name. Only assign if you've renamed shader paths.");

            GUILayout.Space(6);

            // No manual properties here; just auto draw.
            DrawAutoRemaining("Shaders");
        }

        // =============================================================================================
        // RESOLUTION PRESETS
        // =============================================================================================
        private void DrawResolutionPresets()
        {
            var virtualResolution = SP("virtualResolution");
            var useVirtualGrid = SP("useVirtualGrid");

            void SetRes(int w, int h)
            {
                if (virtualResolution == null) return;
                virtualResolution.vector2IntValue = new Vector2Int(Mathf.Max(1, w), Mathf.Max(1, h));
                GUI.FocusControl(null);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("120p", GUILayout.ExpandWidth(true))) SetRes(160, 120);
                if (MiniPill("144p", GUILayout.ExpandWidth(true))) SetRes(256, 144);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("224p", GUILayout.ExpandWidth(true))) SetRes(256, 224);
                if (MiniPill("240p", GUILayout.ExpandWidth(true))) SetRes(320, 240);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("240p (Wide)", GUILayout.ExpandWidth(true))) SetRes(426, 240);
                if (MiniPill("200p (PC)", GUILayout.ExpandWidth(true))) SetRes(320, 200);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("288p", GUILayout.ExpandWidth(true))) SetRes(384, 288);
                if (MiniPill("288p (Wide)", GUILayout.ExpandWidth(true))) SetRes(512, 288);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("300p", GUILayout.ExpandWidth(true))) SetRes(400, 300);
                if (MiniPill("360p", GUILayout.ExpandWidth(true))) SetRes(640, 360);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("360p (Wide)", GUILayout.ExpandWidth(true))) SetRes(854, 360);
                if (MiniPill("384p", GUILayout.ExpandWidth(true))) SetRes(512, 384);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("448p", GUILayout.ExpandWidth(true))) SetRes(512, 448);
                if (MiniPill("448p (Hi)", GUILayout.ExpandWidth(true))) SetRes(640, 448);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("480p", GUILayout.ExpandWidth(true))) SetRes(640, 480);
                if (MiniPill("480p (Wide)", GUILayout.ExpandWidth(true))) SetRes(720, 480);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("480p (16:9)", GUILayout.ExpandWidth(true))) SetRes(854, 480);
                if (MiniPill("480p (WS DVD)", GUILayout.ExpandWidth(true))) SetRes(720, 405);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("600p", GUILayout.ExpandWidth(true))) SetRes(800, 600);
                if (MiniPill("540p", GUILayout.ExpandWidth(true))) SetRes(960, 540);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("576p", GUILayout.ExpandWidth(true))) SetRes(720, 576);
                if (MiniPill("576p (Wide)", GUILayout.ExpandWidth(true))) SetRes(1024, 576);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("720p", GUILayout.ExpandWidth(true))) SetRes(1280, 720);
                if (MiniPill("768p", GUILayout.ExpandWidth(true))) SetRes(1024, 768);
            }
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("768p (WXGA)", GUILayout.ExpandWidth(true))) SetRes(1366, 768);
                if (MiniPill("1080p", GUILayout.ExpandWidth(true))) SetRes(1920, 1080);
            }

            GUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MiniPill("Reset (640×448)", GUILayout.ExpandWidth(true))) SetRes(640, 448);
                if (MiniPill("Screen", GUILayout.ExpandWidth(true)) && useVirtualGrid != null) useVirtualGrid.boolValue = false;
            }
        }

        // =============================================================================================
        // RESET ALL
        // =============================================================================================
        private void ResetToDefaults()
        {
            var targetFx = (CrowImageEffects)target;
            if (targetFx == null) return;

            var tmpGO = new GameObject("CrowImageEffects_Defaults__TEMP")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                var tmp = tmpGO.AddComponent<CrowImageEffects>();
                Undo.RecordObject(targetFx, "Reset All Effects");
                EditorUtility.CopySerializedManagedFieldsOnly(tmp, targetFx);
                EditorUtility.SetDirty(targetFx);
            }
            finally
            {
                DestroyImmediate(tmpGO);
            }
        }

        // =============================================================================================
        // DEPTH MODE
        // =============================================================================================
        private void EnsureDepthModeIfNeeded(CrowImageEffects targetFx)
        {
            if (targetFx == null) return;
            if (!targetFx.useDepthMask && !targetFx.edgeEnabled) return;

            var camera = targetFx.GetComponent<Camera>();
            if (camera != null && (camera.depthTextureMode & DepthTextureMode.Depth) == 0)
            {
                camera.depthTextureMode |= DepthTextureMode.Depth;
                EditorUtility.SetDirty(camera);
            }
        }

        // MISC
        private void LoadFavorites()
        {
            _favoriteSections.Clear();
            var raw = EditorPrefs.GetString(Pref_Favorites, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var k in raw.Split(','))
                    if (!string.IsNullOrEmpty(k)) _favoriteSections.Add(k);
        }

        private void SaveFavorites()
        {
            EditorPrefs.SetString(Pref_Favorites, string.Join(",", _favoriteSections));
        }

        private void ToggleFavorite(string sectionKey)
        {
            if (!_favoriteSections.Add(sectionKey))
                _favoriteSections.Remove(sectionKey);
            SaveFavorites();
            RebuildAll(); // re-sort sections
        }
    }
    #endif
}