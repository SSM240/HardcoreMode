using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.HardcoreMode
{
    /// <summary>
    /// Largely a copy of OuiFileSelectSlot with only rendering functionality.
    /// Meant for use with GameOverScreen.
    /// </summary>
    public class FakeFileSelectSlot : Entity
    {
        public SaveData SaveData;

        public int FileSlot;

        public string Name;

        public bool AssistModeEnabled;

        public bool VariantModeEnabled;

        public bool Exists;

        public bool Corrupted;

        public string Time;

        public int FurthestArea;

        public Sprite Portrait;

        public bool HasBlackgems;

        public StrawberriesCounter Strawberries;

        public DeathsCounter Deaths;

        public List<bool> Cassettes = new List<bool>();

        public List<bool[]> HeartGems = new List<bool[]>();

        private const int height = 300;

        private const int spacing = 10;

        private const float portraitSize = 200f;

        public bool StartingGame;

        public bool Renaming;

        public bool Assisting;

        private bool deleting;

        private float highlightEase;

        private float highlightEaseDelay;

        private float selectedEase;

        private float deletingEase;

        private Tween tween;

        private int deleteIndex = 0;

        private Wiggler wiggler;

        private float failedToDeleteEase;

        private float failedToDeleteTimer;

        private float screenFlash;

        private float newgameFade = 0f;

        private float timeScale = 1f;

        private Sprite normalCard;

        private Sprite goldCard;

        private Sprite normalTicket;

        private Sprite goldTicket;

        private int maxStrawberryCount;

        private int maxGoldenStrawberryCount;

        private int maxStrawberryCountIncludingUntracked;

        private int maxCassettes;

        private int maxCrystalHeartsExcludingCSides;

        private int maxCrystalHearts;

        private bool summitStamp;

        private bool farewellStamp;

        private int totalGoldenStrawberries;

        private int totalHeartGems;

        private int totalCassettes;

        public Vector2 IdlePosition => new Vector2(960f, 525 + 310 * (FileSlot - 1));

        public Vector2 SelectedPosition => new Vector2(960f, 440f);

        private bool Golden
        {
            get
            {
                if (!Corrupted && Exists)
                {
                    return SaveData.TotalStrawberries_Safe >= maxStrawberryCountIncludingUntracked;
                }
                return false;
            }
        }

        private Sprite Card
        {
            get
            {
                if (!Golden)
                {
                    return normalCard;
                }
                return goldCard;
            }
        }

        private Sprite Ticket
        {
            get
            {
                if (!Golden)
                {
                    return normalTicket;
                }
                return goldTicket;
            }
        }

        private FakeFileSelectSlot(int index)
        {
            FileSlot = index;
            base.Tag |= ((int)Tags.HUD | (int)Tags.PauseUpdate);
            Visible = false;
            Add(wiggler = Wiggler.Create(0.4f, 4f));
            normalTicket = new Sprite(MTN.FileSelect, "ticket");
            normalTicket.AddLoop("idle", "", 0.1f);
            normalTicket.Add("shine", "", 0.1f, "idle");
            normalTicket.CenterOrigin();
            normalTicket.Play("idle");
            normalCard = new Sprite(MTN.FileSelect, "card");
            normalCard.AddLoop("idle", "", 0.1f);
            normalCard.Add("shine", "", 0.1f, "idle");
            normalCard.CenterOrigin();
            normalCard.Play("idle");
            goldTicket = new Sprite(MTN.FileSelect, "ticketShine");
            goldTicket.AddLoop("idle", "", 0.1f, default(int));
            goldTicket.Add("shine", "", 0.05f, "idle", 0, 0, 0, 0, 0, 1, 2, 3, 4, 5);
            goldTicket.CenterOrigin();
            goldTicket.Play("idle");
            goldCard = new Sprite(MTN.FileSelect, "cardShine");
            goldCard.AddLoop("idle", "", 0.1f, 5);
            goldCard.Add("shine", "", 0.05f, "idle");
            goldCard.CenterOrigin();
            goldCard.Play("idle");
        }

        public FakeFileSelectSlot(int index, SaveData data)
            : this(index)
        {
            Exists = true;
            SaveData = data;
            Name = data.Name;
            if (!Dialog.Language.CanDisplay(Name))
            {
                Name = Dialog.Clean("FILE_DEFAULT");
            }
            if (!Settings.Instance.VariantsUnlocked && data.TotalHeartGems >= 24)
            {
                Settings.Instance.VariantsUnlocked = true;
            }
            AssistModeEnabled = data.AssistMode;
            VariantModeEnabled = data.VariantMode;
            Add(Deaths = new DeathsCounter(AreaMode.Normal, centeredX: false, data.TotalDeaths));
            Add(Strawberries = new StrawberriesCounter(centeredX: true, data.TotalStrawberries_Safe));
            Time = Dialog.FileTime(data.Time);
            if (TimeSpan.FromTicks(data.Time).TotalHours > 0.0)
            {
                timeScale = 0.725f;
            }
            FurthestArea = data.UnlockedAreas_Safe;
            foreach (AreaStats item in data.Areas_Safe)
            {
                if (item.ID_Safe > data.UnlockedAreas_Safe)
                {
                    break;
                }
                if (!AreaData.Areas[item.ID_Safe].Interlude_Safe && AreaData.Areas[item.ID_Safe].CanFullClear)
                {
                    bool[] array = new bool[3];
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = item.Modes[i].HeartGem;
                    }
                    Cassettes.Add(item.Cassette);
                    HeartGems.Add(array);
                }
            }
            Setup();
        }

        private void Setup()
        {
            string text = "portrait_madeline";
            string id = HMModule.hardcorePortrait;
            Portrait = GFX.PortraitsSpriteBank.Create(text);
            Portrait.Play(id);
            Portrait.Scale = Vector2.One * (200f / (float)GFX.PortraitsSpriteBank.SpriteData[text].Sources[0].XML.AttrInt("size", 160));
            Add(Portrait);
        }

        public Vector2 HiddenPosition(int x, int y)
        {
            return new Vector2(1920f, 1080f) / 2f + new Vector2(x, y) * new Vector2(1920f, 1080f);
        }

        public void Show()
        {
            SaveData instance = SaveData.Instance;
            SaveData.Instance = SaveData;
            LevelSetStats levelSetStats = SaveData?.GetLevelSetStats();
            if (levelSetStats != null)
            {
                StrawberriesCounter strawberries = Strawberries;
                strawberries.Amount = levelSetStats.TotalStrawberries;
                strawberries.OutOf = levelSetStats.MaxStrawberries;
                strawberries.ShowOutOf = (levelSetStats.Name != "Celeste" || strawberries.OutOf <= 0);
                strawberries.CanWiggle = false;
                if (levelSetStats.Name == "Celeste")
                {
                    maxStrawberryCount = 175;
                    maxGoldenStrawberryCount = 25;
                    maxStrawberryCountIncludingUntracked = 202;
                    maxCassettes = 8;
                    maxCrystalHeartsExcludingCSides = 16;
                    maxCrystalHearts = 24;
                    summitStamp = SaveData.Areas_Safe[7].Modes[0].Completed;
                    farewellStamp = SaveData.Areas_Safe[10].Modes[0].Completed;
                }
                else
                {
                    maxStrawberryCount = levelSetStats.MaxStrawberries;
                    maxGoldenStrawberryCount = levelSetStats.MaxGoldenStrawberries;
                    maxStrawberryCountIncludingUntracked = levelSetStats.MaxStrawberriesIncludingUntracked;
                    maxCassettes = levelSetStats.MaxCassettes;
                    maxCrystalHearts = levelSetStats.MaxHeartGems;
                    maxCrystalHeartsExcludingCSides = levelSetStats.MaxHeartGemsExcludingCSides;
                    summitStamp = (levelSetStats.TotalCompletions >= levelSetStats.MaxCompletions);
                    farewellStamp = false;
                }
                totalGoldenStrawberries = levelSetStats.TotalGoldenStrawberries;
                totalHeartGems = levelSetStats.TotalHeartGems;
                totalCassettes = levelSetStats.TotalCassettes;
                FurthestArea = SaveData.UnlockedAreas_Safe;
                Cassettes.Clear();
                HeartGems.Clear();
                foreach (AreaStats item in SaveData.Areas_Safe)
                {
                    if (item.ID_Safe > SaveData.UnlockedAreas_Safe)
                    {
                        break;
                    }
                    if (!AreaData.Areas[item.ID_Safe].Interlude_Safe && AreaData.Areas[item.ID_Safe].CanFullClear)
                    {
                        bool[] array = new bool[3];
                        for (int i = 0; i < array.Length; i++)
                        {
                            array[i] = item.Modes[i].HeartGem;
                        }
                        Cassettes.Add(item.Cassette);
                        HeartGems.Add(array);
                    }
                }
            }
            SaveData.Instance = instance;
            orig_Show();
        }

        private void ShowWithoutOrig()
        {
            SaveData instance = SaveData.Instance;
            SaveData.Instance = SaveData;
            LevelSetStats levelSetStats = SaveData?.GetLevelSetStats();
            if (levelSetStats != null)
            {
                StrawberriesCounter strawberries = Strawberries;
                strawberries.Amount = levelSetStats.TotalStrawberries;
                strawberries.OutOf = levelSetStats.MaxStrawberries;
                strawberries.ShowOutOf = (levelSetStats.Name != "Celeste" || strawberries.OutOf <= 0);
                strawberries.CanWiggle = false;
                if (levelSetStats.Name == "Celeste")
                {
                    maxStrawberryCount = 175;
                    maxGoldenStrawberryCount = 25;
                    maxStrawberryCountIncludingUntracked = 202;
                    maxCassettes = 8;
                    maxCrystalHeartsExcludingCSides = 16;
                    maxCrystalHearts = 24;
                    summitStamp = SaveData.Areas_Safe[7].Modes[0].Completed;
                    farewellStamp = SaveData.Areas_Safe[10].Modes[0].Completed;
                }
                else
                {
                    maxStrawberryCount = levelSetStats.MaxStrawberries;
                    maxGoldenStrawberryCount = levelSetStats.MaxGoldenStrawberries;
                    maxStrawberryCountIncludingUntracked = levelSetStats.MaxStrawberriesIncludingUntracked;
                    maxCassettes = levelSetStats.MaxCassettes;
                    maxCrystalHearts = levelSetStats.MaxHeartGems;
                    maxCrystalHeartsExcludingCSides = levelSetStats.MaxHeartGemsExcludingCSides;
                    summitStamp = (levelSetStats.TotalCompletions >= levelSetStats.MaxCompletions);
                    farewellStamp = false;
                }
                totalGoldenStrawberries = levelSetStats.TotalGoldenStrawberries;
                totalHeartGems = levelSetStats.TotalHeartGems;
                totalCassettes = levelSetStats.TotalCassettes;
                FurthestArea = SaveData.UnlockedAreas_Safe;
                Cassettes.Clear();
                HeartGems.Clear();
                foreach (AreaStats item in SaveData.Areas_Safe)
                {
                    if (item.ID_Safe > SaveData.UnlockedAreas_Safe)
                    {
                        break;
                    }
                    if (!AreaData.Areas[item.ID_Safe].Interlude_Safe && AreaData.Areas[item.ID_Safe].CanFullClear)
                    {
                        bool[] array = new bool[3];
                        for (int i = 0; i < array.Length; i++)
                        {
                            array[i] = item.Modes[i].HeartGem;
                        }
                        Cassettes.Add(item.Cassette);
                        HeartGems.Add(array);
                    }
                }
            }
            SaveData.Instance = instance;
        }

        public void MoveTo(float x, float y)
        {
            Vector2 from = Position;
            Vector2 to = new Vector2(x, y);
            StartTween(0.25f, delegate (Tween f)
            {
                Position = Vector2.Lerp(from, to, f.Eased);
            });
        }

        public void Hide(int x, int y)
        {
            Vector2 from = Position;
            Vector2 to = HiddenPosition(x, y);
            StartTween(0.25f, delegate (Tween f)
            {
                Position = Vector2.Lerp(from, to, f.Eased);
            }, hide: true);
        }

        private void StartTween(float duration, Action<Tween> callback, bool hide = false)
        {
            if (tween != null && tween.Entity == this)
            {
                tween.RemoveSelf();
            }
            Add(tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, duration));
            tween.OnUpdate = callback;
            tween.OnComplete = delegate
            {
                if (hide)
                {
                    Visible = false;
                }
                tween = null;
            };
            tween.Start();
        }

        public override void Update()
        {
            Ticket.Update();
            Card.Update();
            
            if (highlightEaseDelay <= 0f)
            {
                highlightEase = Calc.Approach(highlightEase, 1f, Engine.DeltaTime * 4f);
            }
            else
            {
                highlightEaseDelay -= Engine.DeltaTime;
            }
            base.Depth = -1000;
            if (Renaming || Assisting)
            {
                selectedEase = Calc.Approach(selectedEase, 0f, Engine.DeltaTime * 4f);
            }
            deletingEase = Calc.Approach(deletingEase, deleting ? 1f : 0f, Engine.DeltaTime * 4f);
            failedToDeleteEase = Calc.Approach(failedToDeleteEase, (failedToDeleteTimer > 0f) ? 1f : 0f, Engine.DeltaTime * 4f);
            failedToDeleteTimer -= Engine.DeltaTime;
            screenFlash = Calc.Approach(screenFlash, 0f, Engine.DeltaTime * 4f);
            base.Update();
        }

        public override void Render()
        {
            float scaleFactor = Ease.CubeInOut(highlightEase);
            float num = wiggler.Value * 8f;
            if (selectedEase > 0f)
            {
                Vector2 value = Position + new Vector2(0f, -150f + 350f * selectedEase);
                float lineHeight = ActiveFont.LineHeight;
            }
            Vector2 vector = Position + Vector2.UnitX * scaleFactor * 360f;
            Ticket.RenderPosition = vector;
            Ticket.Render();
            if (highlightEase > 0f && Exists && !Corrupted)
            {
                int num2 = -280;
                int num3 = 600;
                for (int j = 0; j < Cassettes.Count; j++)
                {
                    MTN.FileSelect[Cassettes[j] ? "cassette" : "dot"].DrawCentered(vector + new Vector2((float)num2 + ((float)j + 0.5f) * 75f, -75f));
                    bool[] array = HeartGems[j];
                    int num4 = 0;
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (array[k])
                        {
                            num4++;
                        }
                    }
                    Vector2 vector2 = vector + new Vector2((float)num2 + ((float)j + 0.5f) * 75f, -12f);
                    if (num4 == 0)
                    {
                        MTN.FileSelect["dot"].DrawCentered(vector2);
                        continue;
                    }
                    vector2.Y -= (float)(num4 - 1) * 0.5f * 14f;
                    int l = 0;
                    int num5 = 0;
                    for (; l < array.Length; l++)
                    {
                        if (array[l])
                        {
                            MTN.FileSelect["heartgem" + l].DrawCentered(vector2 + new Vector2(0f, num5 * 14));
                            num5++;
                        }
                    }
                }
                Deaths.Position = vector + new Vector2(num2, 68f) - Position;
                Deaths.Render();
                ActiveFont.Draw(Time, vector + new Vector2(num2 + num3, 68f), new Vector2(1f, 0.5f), Vector2.One * timeScale, Color.Black * 0.6f);
            }
            else if (Corrupted)
            {
                ActiveFont.Draw(Dialog.Clean("file_corrupted"), vector, new Vector2(0.5f, 0.5f), Vector2.One, Color.Black * 0.8f);
            }
            else if (!Exists)
            {
                //ActiveFont.Draw(Dialog.Clean("file_hardcore_deleted"), vector, new Vector2(0.5f, 0.5f), Vector2.One, Color.Black * 0.8f);
            }
            Vector2 vector3 = Position - Vector2.UnitX * scaleFactor * 360f;
            int num6 = 64;
            int num7 = 16;
            float num8 = Card.Width - (float)(num6 * 2) - 200f - (float)num7;
            float x = (0f - Card.Width) / 2f + (float)num6 + 200f + (float)num7 + num8 / 2f;
            float scale = Exists ? 1f : newgameFade;
            if (!Corrupted)
            {
                if (newgameFade > 0f || Exists)
                {
                    if (HMModule.HMSaveData.HardcoreModeEnabled)
                    {
                        MTN.FileSelect["hardcoretab"].DrawCentered(vector3, Color.White * scale);
                    }

                    if (AssistModeEnabled)
                    {
                        MTN.FileSelect["assist"].DrawCentered(vector3, Color.White * scale);
                    }
                    else if (VariantModeEnabled)
                    {
                        MTN.FileSelect["variants"].DrawCentered(vector3, Color.White * scale);
                    }
                }
                if (Exists && SaveData.CheatMode)
                {
                    MTN.FileSelect["cheatmode"].DrawCentered(vector3, Color.White * scale);
                }
            }
            Card.RenderPosition = vector3;
            Card.Render();
            if (!Corrupted)
            {
                if (Exists)
                {
                    if (SaveData.TotalStrawberries_Safe >= maxStrawberryCount)
                    {
                        MTN.FileSelect["strawberry"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (SaveData.Areas_Safe.Count > 7 && summitStamp)
                    {
                        MTN.FileSelect["flag"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (totalCassettes >= maxCassettes)
                    {
                        MTN.FileSelect["cassettes"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (totalHeartGems >= maxCrystalHeartsExcludingCSides)
                    {
                        MTN.FileSelect["heart"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (totalGoldenStrawberries >= maxGoldenStrawberryCount)
                    {
                        MTN.FileSelect["goldberry"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (totalHeartGems >= maxCrystalHearts)
                    {
                        MTN.FileSelect["goldheart"].DrawCentered(vector3, Color.White * scale);
                    }
                    if (SaveData.Areas_Safe.Count > 10 && farewellStamp)
                    {
                        MTN.FileSelect["farewell"].DrawCentered(vector3, Color.White * scale);
                    }
                }
                if (Exists || Renaming || newgameFade > 0f)
                {
                    Portrait.RenderPosition = vector3 + new Vector2((0f - Card.Width) / 2f + (float)num6 + 100f, 0f);
                    Portrait.Color = Color.White * scale;
                    Portrait.Render();
                    MTN.FileSelect[(!Golden) ? "portraitOverlay" : "portraitOverlayGold"].DrawCentered(Portrait.RenderPosition, Color.White * scale);
                    string name = Name;
                    Vector2 vector4 = vector3 + new Vector2(x, -32 + ((!Exists) ? 64 : 0));
                    float num9 = Math.Min(1f, 440f / ActiveFont.Measure(name).X);
                    ActiveFont.Draw(name, vector4, new Vector2(0.5f, 1f), Vector2.One * num9, Color.Black * 0.8f * scale);
                    if (Renaming && base.Scene.BetweenInterval(0.3f))
                    {
                        ActiveFont.Draw("|", new Vector2(vector4.X + ActiveFont.Measure(name).X * num9 * 0.5f, vector4.Y), new Vector2(0f, 1f), Vector2.One * num9, Color.Black * 0.8f * scale);
                    }
                }
                if (Exists)
                {
                    if (FurthestArea < AreaData.Areas.Count)
                    {
                        ActiveFont.Draw(Dialog.Clean(AreaData.Areas[FurthestArea].Name), vector3 + new Vector2(x, -10f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, Color.Black * 0.6f);
                    }
                    Strawberries.Position = vector3 + new Vector2(x, 55f) - Position;
                    Strawberries.Render();
                }
            }
            else
            {
                ActiveFont.Draw(Dialog.Clean("file_failedtoload"), vector3, new Vector2(0.5f, 0.5f), Vector2.One, Color.Black * 0.8f);
            }
            if (deletingEase > 0f)
            {
                float num10 = Ease.CubeOut(deletingEase);
                Vector2 value3 = new Vector2(960f, 540f);
                float lineHeight2 = ActiveFont.LineHeight;
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * num10 * 0.9f);
                ActiveFont.Draw(Dialog.Clean("file_delete_really"), value3 + new Vector2(0f, -16f - 64f * (1f - num10)), new Vector2(0.5f, 1f), Vector2.One, Color.White * num10);
                ActiveFont.DrawOutline(Dialog.Clean("file_delete_yes"), value3 + new Vector2(((deleting && deleteIndex == 0) ? num : 0f) * 1.2f * num10, 16f + 64f * (1f - num10)), new Vector2(0.5f, 0f), Vector2.One * 0.8f, deleting ? SelectionColor(deleteIndex == 0) : Color.Gray, 2f, Color.Black * num10);
                ActiveFont.DrawOutline(Dialog.Clean("file_delete_no"), value3 + new Vector2(((deleting && deleteIndex == 1) ? num : 0f) * 1.2f * num10, 16f + lineHeight2 + 64f * (1f - num10)), new Vector2(0.5f, 0f), Vector2.One * 0.8f, deleting ? SelectionColor(deleteIndex == 1) : Color.Gray, 2f, Color.Black * num10);
                if (failedToDeleteEase > 0f)
                {
                    Vector2 vector5 = new Vector2(960f, 980f - 100f * deletingEase);
                    Vector2 scale2 = Vector2.One * 0.8f;
                    if (failedToDeleteEase < 1f && failedToDeleteTimer > 0f)
                    {
                        vector5 += new Vector2(-5 + Calc.Random.Next(10), -5 + Calc.Random.Next(10));
                        scale2 = Vector2.One * (0.8f + 0.2f * (1f - failedToDeleteEase));
                    }
                    ActiveFont.DrawOutline(Dialog.Clean("file_delete_failed"), vector5, new Vector2(0.5f, 0f), scale2, Color.PaleVioletRed * deletingEase, 2f, Color.Black * deletingEase);
                }
            }
            if (screenFlash > 0f)
            {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.White * Ease.CubeOut(screenFlash));
            }
        }

        public Color SelectionColor(bool selected)
        {
            if (selected)
            {
                if (!Settings.Instance.DisableFlashes && !base.Scene.BetweenInterval(0.1f))
                {
                    return TextMenu.HighlightColorB;
                }
                return TextMenu.HighlightColorA;
            }
            return Color.White;
        }

        /// <summary>
        /// Creates an effect making it appear that the file has been deleted.
        /// (The actual file deletion is handled separately.)
        /// </summary>
        public void FakeDelete()
        {
            if (!Settings.Instance.DisableFlashes)
            {
                screenFlash = 1f;
            }
            Audio.Play("event:/ui/main/savefile_delete");
            Exists = false;
        }

        private bool orig_get_Golden()
        {
            if (!Corrupted && Exists)
            {
                return SaveData.TotalStrawberries_Safe >= 202;
            }
            return false;
        }

        public void orig_Show()
        {
            Visible = true;
            deleting = false;
            StartingGame = false;
            Renaming = false;
            Assisting = false;
            selectedEase = 0f;
            highlightEase = 0f;
            highlightEaseDelay = 0.35f;
            Vector2 from = Position;
            StartTween(0.25f, delegate (Tween f)
            {
                Position = Vector2.Lerp(from, IdlePosition, f.Eased);
            });
        }

        /// <summary>
        /// Instantly snaps the slot to its "selected" position.
        /// </summary>
        public void SnapShow()
        {
            ShowWithoutOrig();
            Visible = true;
            deleting = false;
            StartingGame = false;
            Renaming = false;
            Assisting = false;
            selectedEase = 0f;
            highlightEase = 1f;
            highlightEaseDelay = 0f;
            Position = IdlePosition;
        }
    }
}
