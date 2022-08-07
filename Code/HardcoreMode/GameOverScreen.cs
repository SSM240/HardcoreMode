using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HardcoreMode
{
    /// <summary>
    /// Scene that shows the Game Over sequence and deletes the save file.
    /// </summary>
    public class GameOverScreen : Scene
    {
        private Level level;

        private FakeFileSelectSlot fakeSlot;

        private FadeWipe fadeWipe;

        private TextMenu deleteButtonMenu;

        // fun fact: this button is technically useless, it's only there for show
        private TextMenu.Button deleteButton;

        private TextMenu.SubHeader deathInfo;

        public GameOverScreen(SaveData saveData, Level level)
        {
            this.level = level;
            Add(new HudRenderer());
            Add(deleteButtonMenu = new TextMenu());
            // there's probably a better way to do this but i don't care
            deleteButtonMenu.Add(new TextMenu.Header(Dialog.Clean("HARDCORE_GAMEOVER")));
            deleteButtonMenu.Add(new TextMenu.Header(""));
            deleteButtonMenu.Add(new TextMenu.Header(""));
            deleteButtonMenu.Add(new TextMenu.Header(""));
            deleteButtonMenu.Add(deleteButton = new TextMenu.Button(""));
            deleteButtonMenu.Add(deathInfo = new TextMenu.SubHeader(" "));
            fakeSlot = new FakeFileSelectSlot(1, saveData);
            Add(fakeSlot);
            fadeWipe = new FadeWipe(this, wipeIn: true);
            fakeSlot.Add(new Coroutine(GameOverRoutine()));
        }

        private IEnumerator GameOverRoutine()
        {
            // wait 1.8 seconds, but cancel that wait if confirm is pressed
            float timer = 1.8f;
            bool shown = false;
            while (timer > 0f)
            {
                if (Input.MenuConfirm.Pressed)
                {
                    fakeSlot.SnapShow();
                    fadeWipe?.Cancel();
                    break;
                }
                // show slot after 1 second
                else if (timer < 0.8f && !shown)
                {
                    fakeSlot.Show();
                    shown = true;
                }
                timer -= Engine.DeltaTime;
                yield return null;
            }
            
            deleteButton.Label = Dialog.Clean("FILE_DELETE");
            string areaName = Dialog.Clean(AreaData.Get(level.Session.Area).Name);
            string deathInfoString = $"{Dialog.Clean("HARDCORE_DIED_ON")} {areaName} ";
            if (level.Session.Area.Mode == AreaMode.BSide)
            {
                deathInfoString += Dialog.Clean("OVERWORLD_REMIX") + " ";
            }
            else if (level.Session.Area.Mode == AreaMode.CSide)
            {
                deathInfoString += Dialog.Clean("OVERWORLD_REMIX2") + " ";
            }
            deathInfoString += (!level.Session.Level.StartsWith("lvl_") ? "lvl_" : "") + level.Session.Level;
            deathInfo.Title = deathInfoString;

            yield return null;  // prevent same input from triggering multiple actions
            while (!Input.MenuConfirm.Pressed)
            {
                yield return null;
            }
            deleteButton.Disabled = true;
            fakeSlot.FakeDelete();
            yield return null;  // same here

            // wait 1 second, but cancel that wait if confirm is pressed
            timer = 1f;
            while (timer > 0 && !Input.MenuConfirm.Pressed)
            {
                timer -= Engine.DeltaTime;
                yield return null;
            }
            HMModule.Instance.RestoreDebugMode();
            new FadeWipe(this, wipeIn: false, onComplete: () =>
                Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu)
            );
        }
    }
}
