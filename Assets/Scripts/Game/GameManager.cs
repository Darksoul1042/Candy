using System;
using System.Collections;
using System.IO;
using UnityEngine;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Punto de unión entre la lógica (GameSession), la presentación (BoardView, UIManager) y el guardado (SaveSystem).</summary>
    public sealed class GameManager : MonoBehaviour
    {
        private const float HintDelay = 6f;

        private Camera cam;
        private AnimationManager anim;
        private AudioManager audioMgr;
        private Fx fx;
        private BoardView view;
        private UIManager ui;
        private SaveSystem save;
        private GameSession session;
        private int level;
        private bool inLevel;
        private float idleTimer;
        private int lastW, lastH;

        public bool Busy { get; private set; }
        public BoosterKind? PendingBooster { get; private set; }

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>(); // sin AudioListener no se oye nada
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.32f, 0.38f);
            cam.transform.position = new Vector3(0, 0, -10);

            anim = gameObject.AddComponent<AnimationManager>();
            audioMgr = gameObject.AddComponent<AudioManager>();
            fx = gameObject.AddComponent<Fx>();
            view = new GameObject("Board").AddComponent<BoardView>();
            ui = new GameObject("UI").AddComponent<UIManager>();
            ui.Build();
            gameObject.AddComponent<InputController>().Init(this, view, cam);

            save = new SaveSystem(new FileStorage(Path.Combine(Application.persistentDataPath, "petalopop_save.txt")));
            audioMgr.SetEnabled(save.Data.SoundOn);
            ui.SetSoundLabel(save.Data.SoundOn);

            ui.LevelChosen += StartLevel;
            ui.RestartRequested += () => StartLevel(level);
            ui.NextRequested += () => StartLevel(Mathf.Min(LevelCatalog.Count, level + 1));
            ui.MenuRequested += LeaveToMenu;
            ui.BoosterClicked += OnBoosterClicked;
            ui.SoundToggled += () =>
            {
                audioMgr.SetEnabled(!audioMgr.SoundOn);
                save.SetSound(audioMgr.SoundOn, audioMgr.SoundOn);
                ui.SetSoundLabel(audioMgr.SoundOn);
                audioMgr.Click();
            };

            ShowMenu();
        }

        // ------------------------------------------------------------------ Flujo
        private void ShowMenu()
        {
            StopAllCoroutines();
            inLevel = false;
            Busy = false;
            PendingBooster = null;
            view.gameObject.SetActive(false);
            ui.ShowLevelSelect(save);
        }

        private void LeaveToMenu()
        {
            if (inLevel && session != null && session.State == GameState.Playing)
                save.RecordResult(level, false, session.Score, 0, session.Boosters); // conserva mejor puntuación e inventario
            audioMgr.Click();
            ShowMenu();
        }

        private void StartLevel(int n)
        {
            StopAllCoroutines();
            level = Mathf.Clamp(n, 1, LevelCatalog.Count);
            save.SetCurrent(level);
            try
            {
                session = new GameSession(LevelProvider.Get(level), null, null, save.CreateInventory());
            }
            catch (Exception e)
            {
                Debug.LogError("Nivel " + level + " inválido, se usa el catálogo: " + e.Message);
                session = new GameSession(LevelCatalog.Get(level), null, null, save.CreateInventory());
            }
            inLevel = true;
            Busy = false;
            PendingBooster = null;
            idleTimer = 0f;
            view.gameObject.SetActive(true);
            view.Build(session.Board, anim, fx, audioMgr, Vector2.zero);
            FitCamera();
            ui.HideLevelSelect();
            ui.HideEnd();
            ui.ShowHud(true);
            ui.SetHint("");
            ui.Refresh(session, level, PendingBooster);
        }

        private void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) { lastW = Screen.width; lastH = Screen.height; FitCamera(); }
            if (!inLevel || session == null || Busy || session.State != GameState.Playing) return;
            idleTimer += Time.deltaTime;
            if (idleTimer >= HintDelay)
            {
                idleTimer = 0f;
                if (session.TryGetHint(out var a, out var b)) StartCoroutine(view.Pulse(a, b));
            }
        }

        // Ajusta el tamaño ortográfico para que el tablero y la interfaz quepan en cualquier pantalla vertical.
        private void FitCamera()
        {
            if (view == null || cam == null || session == null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float needW = session.Board.Width * view.CellSize + 0.8f;
            float needH = session.Board.Height * view.CellSize + 6f; // margen para HUD arriba y potenciadores abajo
            cam.orthographicSize = Mathf.Max(needH * 0.5f, needW * 0.5f / aspect);
        }

        // ------------------------------------------------------------------ Acciones del jugador
        public void RequestSwap(Position a, Position b)
        {
            if (Busy || !inLevel || session.State != GameState.Playing) return;
            if (PendingBooster == BoosterKind.FreeSwap)
            {
                var r = session.UseFreeSwap(a, b);
                if (!r.Accepted) { Reject("Esas fichas no se pueden cambiar"); return; }
                PendingBooster = null;
                StartCoroutine(Play(r));
                return;
            }
            StartCoroutine(Play(session.TrySwap(a, b)));
        }

        public void RequestBoosterTap(Position cell)
        {
            if (Busy || !inLevel || session.State != GameState.Playing || !PendingBooster.HasValue) return;
            MoveResult r;
            switch (PendingBooster.Value)
            {
                case BoosterKind.Hammer: r = session.UseHammer(cell); break;
                case BoosterKind.ObstacleRemover: r = session.UseObstacleRemover(cell); break;
                case BoosterKind.SpecialMaker: r = session.UseSpecialMaker(cell, SpecialKind.Bomb); break;
                default: return;
            }
            if (!r.Accepted) { Reject("No se puede usar ahí"); return; }
            PendingBooster = null;
            StartCoroutine(Play(r));
        }

        private void OnBoosterClicked(BoosterKind kind)
        {
            if (Busy || !inLevel || session.State != GameState.Playing) return;
            audioMgr.Click();
            if (session.Boosters.Count(kind) <= 0) { Reject("No te quedan de ese potenciador"); return; }
            if (kind == BoosterKind.ExtraMoves)
            {
                session.UseExtraMoves();
                PendingBooster = null;
                ui.SetHint("");
                ui.Refresh(session, level, PendingBooster);
                audioMgr.Special();
                return;
            }
            PendingBooster = PendingBooster == kind ? (BoosterKind?)null : kind;
            ui.SetHint(PendingBooster.HasValue ? HintFor(kind) : "");
            ui.Refresh(session, level, PendingBooster);
        }

        private static string HintFor(BoosterKind k)
        {
            switch (k)
            {
                case BoosterKind.Hammer: return "Toca la ficha que quieres romper";
                case BoosterKind.FreeSwap: return "Desliza o toca dos fichas contiguas";
                case BoosterKind.ObstacleRemover: return "Toca hielo, caja, piedra o gelatina";
                default: return "Toca una ficha para convertirla en bomba";
            }
        }

        private void Reject(string message)
        {
            audioMgr.Invalid();
            ui.SetHint(message);
        }

        private IEnumerator Play(MoveResult result)
        {
            Busy = true;
            idleTimer = 0f;
            ui.SetHint("");
            yield return view.PlaySteps(result.Steps);
            ui.Refresh(session, level, PendingBooster);
            if (session.State != GameState.Playing)
            {
                bool won = session.State == GameState.Won;
                save.RecordResult(level, won, session.Score, session.Scoring.Stars, session.Boosters);
                if (won) audioMgr.Win(); else audioMgr.Lose();
                ui.ShowEnd(session, level < LevelCatalog.Count);
            }
            Busy = false;
        }
    }
}
