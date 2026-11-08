using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Interfaz construida por código: selector de niveles, HUD con objetivos, barra de potenciadores y fin de partida.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        private static readonly string[] ColorNames = { "coral", "ámbar", "menta", "azul", "violeta", "naranja" };
        private static readonly BoosterKind[] BoosterOrder =
            { BoosterKind.Hammer, BoosterKind.FreeSwap, BoosterKind.ObstacleRemover, BoosterKind.ExtraMoves, BoosterKind.SpecialMaker };
        private static readonly string[] BoosterLabels = { "Martillo", "Cambio", "Limpiar", "+5 mov.", "Bomba" };

        public event Action<int> LevelChosen;
        public event Action RestartRequested, NextRequested, MenuRequested, SoundToggled;
        public event Action<BoosterKind> BoosterClicked;

        private Font font;
        private Transform canvas;
        private GameObject hud, endPanel, selectPanel;
        private Text levelText, scoreText, movesText, objectivesText, hintText, endTitle, endBody, soundLabel;
        private GameObject nextButton;
        private readonly Dictionary<BoosterKind, Text> boosterLabels = new Dictionary<BoosterKind, Text>();
        private readonly Dictionary<BoosterKind, Image> boosterImages = new Dictionary<BoosterKind, Image>();
        private Transform gridRoot;

        public void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvas = canvasGo.transform;

            BuildHud();
            BuildEndPanel();
            BuildSelectPanel();
            ShowHud(false);
            endPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ HUD
        private void BuildHud()
        {
            hud = Rect("Hud", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            levelText = Label("Level", hud.transform, 60, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(40, -110), new Vector2(0, -30));
            movesText = Label("Moves", hud.transform, 60, TextAnchor.UpperRight, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(-40, -30));
            scoreText = Label("Score", hud.transform, 50, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -190), new Vector2(-40, -110));
            objectivesText = Label("Objectives", hud.transform, 42, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -420), new Vector2(-40, -190));
            objectivesText.color = new Color(1f, 1f, 1f, 0.85f);
            hintText = Label("Hint", hud.transform, 44, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 330), new Vector2(-40, 400));
            hintText.color = new Color(1f, 0.95f, 0.6f);

            for (int i = 0; i < BoosterOrder.Length; i++)
            {
                var kind = BoosterOrder[i];
                float ax = 0.1f + i * 0.2f;
                var b = Button(hud.transform, BoosterLabels[i], new Vector2(ax, 0), new Vector2(0, 220), new Vector2(170, 170), new Color(0.98f, 0.76f, 0.22f), 36, () => BoosterClicked?.Invoke(kind));
                boosterLabels[kind] = b.label;
                boosterImages[kind] = b.image;
            }
            var menu = Button(hud.transform, "Niveles", new Vector2(0.27f, 0), new Vector2(0, 80), new Vector2(300, 90), new Color(0.35f, 0.62f, 0.95f), 44, () => MenuRequested?.Invoke());
            var snd = Button(hud.transform, "Sonido: sí", new Vector2(0.73f, 0), new Vector2(0, 80), new Vector2(300, 90), new Color(0.7f, 0.48f, 0.92f), 44, () => SoundToggled?.Invoke());
            soundLabel = snd.label;
        }

        public void ShowHud(bool on) => hud.SetActive(on);
        public void SetSoundLabel(bool on) => soundLabel.text = on ? "Sonido: sí" : "Sonido: no";
        public void SetHint(string text) => hintText.text = text ?? "";

        public void Refresh(GameSession s, int level, BoosterKind? active)
        {
            levelText.text = "Nivel " + level;
            movesText.text = "Movimientos: " + s.Moves.MovesLeft;
            scoreText.text = "Puntos: " + s.Score + " / " + s.Config.TargetScore;
            var sb = new StringBuilder();
            foreach (var o in s.Objectives.Items)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ObjectiveName(o.Spec)).Append(": ").Append(Math.Min(o.Current, o.Target)).Append(" / ").Append(o.Target);
                if (o.Done) sb.Append("  (listo)");
            }
            objectivesText.text = sb.ToString();
            foreach (var kind in BoosterOrder)
            {
                int n = s.Boosters.Count(kind);
                boosterLabels[kind].text = BoosterLabels[Array.IndexOf(BoosterOrder, kind)] + "\n" + n;
                boosterImages[kind].color = n <= 0 ? new Color(0.5f, 0.5f, 0.5f)
                    : (active.HasValue && active.Value == kind) ? new Color(1f, 0.55f, 0.3f) : new Color(0.98f, 0.76f, 0.22f);
            }
        }

        private static string ObjectiveName(ObjectiveSpec s)
        {
            switch (s.Kind)
            {
                case ObjectiveKind.CollectType: return "Fichas " + ColorNames[Mathf.Clamp(s.Type - 1, 0, ColorNames.Length - 1)];
                case ObjectiveKind.ClearJelly: return "Gelatina";
                case ObjectiveKind.BreakFrozen: return "Hielo";
                default: return "Cajas";
            }
        }

        // ------------------------------------------------------------------ Fin de partida
        private void BuildEndPanel()
        {
            endPanel = Rect("EndPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            endPanel.AddComponent<Image>().color = new Color(0.02f, 0.07f, 0.1f, 0.9f);
            endTitle = Label("Title", endPanel.transform, 100, TextAnchor.MiddleCenter, new Vector2(0, 0.62f), new Vector2(1, 0.76f), Vector2.zero, Vector2.zero);
            endBody = Label("Body", endPanel.transform, 56, TextAnchor.MiddleCenter, new Vector2(0, 0.46f), new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            var next = Button(endPanel.transform, "Siguiente", new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(520, 130), new Color(0.35f, 0.8f, 0.55f), 60, () => { endPanel.SetActive(false); NextRequested?.Invoke(); });
            nextButton = next.image.gameObject;
            Button(endPanel.transform, "Reintentar", new Vector2(0.5f, 0.29f), Vector2.zero, new Vector2(520, 130), new Color(0.98f, 0.76f, 0.22f), 60, () => { endPanel.SetActive(false); RestartRequested?.Invoke(); });
            Button(endPanel.transform, "Niveles", new Vector2(0.5f, 0.20f), Vector2.zero, new Vector2(520, 130), new Color(0.35f, 0.62f, 0.95f), 60, () => { endPanel.SetActive(false); MenuRequested?.Invoke(); });
        }

        public void ShowEnd(GameSession s, bool hasNext)
        {
            bool won = s.State == GameState.Won;
            endTitle.text = won ? "¡Nivel superado!" : "Sin movimientos";
            endBody.text = (won ? "Estrellas: " + s.Scoring.Stars + " / 3\n" : "") + "Puntos: " + s.Score;
            nextButton.SetActive(won && hasNext);
            endPanel.SetActive(true);
        }

        public void HideEnd() => endPanel.SetActive(false);

        // ------------------------------------------------------------------ Selector de niveles
        private void BuildSelectPanel()
        {
            selectPanel = Rect("SelectPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            selectPanel.AddComponent<Image>().color = new Color(0.08f, 0.32f, 0.38f, 1f);
            Label("Title", selectPanel.transform, 110, TextAnchor.MiddleCenter, new Vector2(0, 0.86f), new Vector2(1, 0.96f), Vector2.zero, Vector2.zero).text = "Pétalo Pop";
            var grid = Rect("Grid", selectPanel.transform, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.84f), Vector2.zero, Vector2.zero);
            var g = grid.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(170, 170);
            g.spacing = new Vector2(20, 20);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            g.childAlignment = TextAnchor.MiddleCenter;
            gridRoot = grid.transform;
        }

        public void ShowLevelSelect(SaveSystem save)
        {
            ShowHud(false);
            endPanel.SetActive(false);
            for (int i = gridRoot.childCount - 1; i >= 0; i--) Destroy(gridRoot.GetChild(i).gameObject);
            for (int l = 1; l <= LevelCatalog.Count; l++)
            {
                int level = l;
                bool unlocked = save.IsUnlocked(l);
                int stars = save.StarsOf(l);
                string label = unlocked ? l + "\n" + (stars > 0 ? new string('*', stars) : "-") : l + "\nbloq.";
                var color = !unlocked ? new Color(0.45f, 0.5f, 0.52f) : stars >= 3 ? new Color(0.98f, 0.76f, 0.22f) : stars > 0 ? new Color(0.35f, 0.8f, 0.55f) : new Color(0.35f, 0.62f, 0.95f);
                var b = Button(gridRoot, label, Vector2.zero, Vector2.zero, new Vector2(170, 170), color, 48, () => { selectPanel.SetActive(false); LevelChosen?.Invoke(level); });
                b.image.GetComponent<Button>().interactable = unlocked;
                var rt = b.image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // lo posiciona el GridLayoutGroup
            }
            selectPanel.SetActive(true);
        }

        public void HideLevelSelect() => selectPanel.SetActive(false);

        // ------------------------------------------------------------------ Utilidades de construcción
        private static GameObject Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return go;
        }

        private Text Label(string name, Transform parent, int size, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = Rect(name, parent, aMin, aMax, offMin, offMax);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private (Image image, Text label) Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize, Action onClick)
        {
            var go = Rect("Button", parent, anchor, anchor, pos - size * 0.5f, pos + size * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = Label("Label", go.transform, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = label;
            t.color = new Color(0.04f, 0.16f, 0.1f);
            return (img, t);
        }
    }
}
