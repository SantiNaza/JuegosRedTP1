using System;
using System.Collections.Generic;
using UnityEngine;
using TP.Core;

namespace TP.UI
{
    /// <summary>Crea el Canvas y las pantallas, y muestra la que corresponde a cada estado del juego.</summary>
    public sealed class UIService : IDisposable
    {
        private readonly EventBus events;
        private readonly Canvas canvas;
        private readonly MenuScreen menu;
        private readonly UpgradesScreen upgrades;
        private readonly PauseScreen pause;
        private readonly EndScreen end;

        public HudScreen Hud { get; }

        public UIService(EventBus events, IGameFlow flow, IMatchStats stats, IPlayerService player,
                         IProgressionService progression, IAudioService audio, Texture menuBackground,
                         List<string> modelNames, bool showFps)
        {
            this.events = events;
            UIFactory.EnsureEventSystem();
            canvas = UIFactory.CreateCanvas("UI", 10);
            Transform root = canvas.transform;

            Hud = new HudScreen(root, events, player, showFps);
            upgrades = new UpgradesScreen(root, menuBackground, progression, audio, ShowMenu);
            menu = new MenuScreen(root, menuBackground, flow, progression, audio, modelNames, ShowUpgrades);
            pause = new PauseScreen(root, flow, audio);
            end = new EndScreen(root, flow, stats, progression, audio);

            events.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        public void Dispose()
        {
            events.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            Hud.Dispose();
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            HideAll();
            switch (e.Current)
            {
                case GameState.Menu: menu.Show(); break;
                case GameState.Playing: Hud.Show(); break;
                case GameState.Paused: Hud.Show(); pause.Show(); break;
                case GameState.Victory: Hud.Show(); end.Show(true); break;
                case GameState.Defeat: Hud.Show(); end.Show(false); break;
            }
        }

        private void ShowMenu()
        {
            HideAll();
            menu.Show();
        }

        private void ShowUpgrades()
        {
            HideAll();
            upgrades.Show();
        }

        private void HideAll()
        {
            menu.Hide();
            upgrades.Hide();
            Hud.Hide();
            pause.Hide();
            end.Hide();
        }
    }
}
