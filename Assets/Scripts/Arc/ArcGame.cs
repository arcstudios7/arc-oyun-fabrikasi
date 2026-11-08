using UnityEngine;

namespace Arc
{
    /// <summary>
    /// Base class for every game. Gives you for free: title menu, how-to-play text, pause (Esc / Android back),
    /// game over screen, score + best score, restart. You only write Setup / ResetRound / Tick.
    ///
    /// Flow:  Setup() once → ResetRound() → [Menu] → StartGame() → Tick() every frame → GameOver() → [Game Over] → restart
    /// </summary>
    public abstract class ArcGame : MonoBehaviour
    {
        public enum State { Menu, Playing, Paused, GameOver }

        public State Current { get; private set; } = State.Menu;

        /// <summary>Current score. Increase it in Tick().</summary>
        public int Score;

        /// <summary>Seconds since StartGame (pauses excluded).</summary>
        public float PlayTime { get; private set; }

        bool newBest;
        float stateTime;

        /// <summary>Short Turkish "how to play" text shown on the menu.</summary>
        protected virtual string HowToPlay => "";

        /// <summary>Title on the end screen. Default: "KAZANDIN!" / "OYUN BİTTİ".</summary>
        protected virtual string EndTitle(bool won) => won ? "KAZANDIN!" : "OYUN BİTTİ";

        /// <summary>Set false for games without a score (e.g. puzzle with levels) to hide score UI.</summary>
        protected virtual bool ShowScore => true;

        /// <summary>Build the world ONCE: camera setup, static level, player object.</summary>
        protected abstract void Setup();

        /// <summary>Put everything back to the starting position (destroy spawned objects, reset variables). Must be safe to call many times.</summary>
        protected abstract void ResetRound();

        /// <summary>Gameplay update, called every frame ONLY while playing. dt = Time.deltaTime.</summary>
        protected abstract void Tick(float dt);

        /// <summary>In-game HUD (called from OnGUI while playing). Default: score + best.</summary>
        protected virtual void DrawHud()
        {
            if (!ShowScore) return;
            Hud.Text("Skor: " + Score, 24, 18, 1.2f);
            Hud.Text("En iyi: " + Save.Best(), 24, 64, 0.8f, Palette.Lighter(Palette.Accent, 0.3f));
        }

        /// <summary>Optional hooks.</summary>
        protected virtual void OnGameStarted() { }
        protected virtual void OnGameEnded(bool won) { }

        /// <summary>Optional: called every frame while NOT playing (menu / game over), e.g. idle animations.</summary>
        protected virtual void IdleTick(float dt) { }

        protected virtual void Start()
        {
            Setup();
            ResetRound();
            Current = State.Menu;
        }

        protected virtual void Update()
        {
            stateTime += Time.unscaledDeltaTime;

            if (Inputs.Back)
            {
                if (Current == State.Playing) Pause();
                else if (Current == State.Paused) Resume();
                else if (Current == State.Menu && Application.platform == RuntimePlatform.Android) Application.Quit();
                else if (Current == State.GameOver) GoToMenu();
            }

            switch (Current)
            {
                case State.Playing:
                    PlayTime += Time.deltaTime;
                    Tick(Time.deltaTime);
                    break;
                case State.Menu:
                case State.GameOver:
                    IdleTick(Time.deltaTime);
                    // Keyboard shortcut; touch players use the button (prevents accidental instant restarts).
                    if (stateTime > 0.6f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                        StartGame();
                    break;
            }
        }

        public void StartGame()
        {
            ResetRound();
            Score = 0;
            PlayTime = 0f;
            newBest = false;
            Time.timeScale = 1f;
            SetState(State.Playing);
            OnGameStarted();
        }

        /// <summary>Call when the player loses (or wins with won=true). Safe to call more than once.</summary>
        public void GameOver(bool won = false)
        {
            if (Current != State.Playing) return;
            newBest = ShowScore && Save.SubmitBest(Score);
            if (won) Sfx.Win(); else Sfx.Lose();
            SetState(State.GameOver);
            lastWon = won;
            OnGameEnded(won);
        }

        bool lastWon;

        public void Pause()
        {
            if (Current != State.Playing) return;
            Time.timeScale = 0f;
            SetState(State.Paused);
        }

        public void Resume()
        {
            if (Current != State.Paused) return;
            Time.timeScale = 1f;
            SetState(State.Playing);
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            ResetRound();
            SetState(State.Menu);
        }

        void SetState(State s)
        {
            Current = s;
            stateTime = 0f;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        protected virtual void OnGUI()
        {
            GUI.depth = 0;
            switch (Current)
            {
                case State.Menu:
                    Hud.Overlay(new Color(0f, 0f, 0f, 0.35f));
                    Hud.Center(Game.GameInfo.Title, -230, 2.2f, Palette.Accent);
                    if (!string.IsNullOrEmpty(HowToPlay)) Hud.Center(HowToPlay, -60, 0.75f);
                    if (ShowScore && Save.Best() > 0) Hud.Center("En iyi skor: " + Save.Best(), 60, 0.8f, Palette.Lighter(Palette.Accent, 0.3f));
                    if (Hud.Button("OYNA", 170)) StartGame();
                    Hud.Center("ARC Studios", 300, 0.5f, new Color(1f, 1f, 1f, 0.5f));
                    break;

                case State.Playing:
                    DrawHud();
                    if (Hud.CornerButton("II")) Pause();
                    break;

                case State.Paused:
                    DrawHud();
                    Hud.Overlay(new Color(0f, 0f, 0f, 0.55f));
                    Hud.Center("DURAKLATILDI", -120, 1.6f);
                    if (Hud.Button("DEVAM", 20)) Resume();
                    if (Hud.Button("MENÜ", 140)) GoToMenu();
                    break;

                case State.GameOver:
                    Hud.Overlay(new Color(0f, 0f, 0f, 0.55f));
                    Hud.Center(EndTitle(lastWon), -200, 1.8f, lastWon ? Palette.Good : Palette.Danger);
                    if (ShowScore)
                    {
                        Hud.Center("Skor: " + Score, -80, 1.3f);
                        Hud.Center(newBest ? "YENİ REKOR!" : "En iyi: " + Save.Best(), 0, 0.9f, Palette.Accent);
                    }
                    if (stateTime > 0.6f && Hud.Button("TEKRAR OYNA", 130)) StartGame();
                    if (stateTime > 0.6f && Hud.Button("MENÜ", 250, 300, 80)) GoToMenu();
                    break;
            }
        }
    }
}
