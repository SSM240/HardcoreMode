using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HardcoreMode
{
    public class HMIcon : Entity
    {
        private const float translucentAlpha = 0.4f;

        private static readonly Vector2 bottomLeftPosition = new Vector2(30f, 1080f - 30f);

        private static readonly Vector2 bottomRightPosition = new Vector2(1920f - 30f, 1080f - 30f);

        private static readonly Vector2 topRightPosition = new Vector2(1920f - 30f, 30f);

        private static readonly Vector2 bottomLeftJustify = new Vector2(0f, 1f);

        private static readonly Vector2 bottomRightJustify = new Vector2(1f, 1f);

        private static readonly Vector2 topRightJustify = new Vector2(1f, 0f);

        private float alpha;

        public HMIcon()
        {
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = -1;
            if (HMModule.HMSettings.HardcoreIcon == 1)
            {
                alpha = translucentAlpha;
            }
            else
            {
                alpha = 1f;
            }
        }

        public override void Update()
        {
            Level level = SceneAs<Level>();
            Session session = level.Session;
            // fighting jank with jank!
            if (session.Area.GetLevelSet() == "Celeste" &&
                session.Area.ID == 10 && session.Level == "end-cinematic" &&
                Settings.Instance.SpeedrunClock != SpeedrunType.Off)
            {
                RemoveSelf();
                return;
            }
            if (HMModule.HMSettings.HardcoreIcon == 1)
            {
                alpha = Calc.Approach(alpha, translucentAlpha, 4f * Engine.DeltaTime);
            }
            else
            {
                alpha = Calc.Approach(alpha, 1f, 4f * Engine.DeltaTime);
            }
            base.Update();
        }

        public override void Render()
        {
            if (HMModule.HMSettings.HardcoreIcon > 0)
            {
                // only render one at once
                if (Scene.Entities.FindFirst<HMIcon>() == this)
                {
                    Vector2 position;
                    Vector2 justify;
                    switch(HMModule.HMSettings.IconPosition)
                    {
                        default:
                        case IconPositions.BottomLeft:
                            position = bottomLeftPosition;
                            justify = bottomLeftJustify;
                            break;
                        case IconPositions.BottomRight:
                            position = bottomRightPosition;
                            justify = bottomRightJustify;
                            break;
                        case IconPositions.TopRight:
                            position = topRightPosition;
                            justify = topRightJustify;
                            break;
                    }
                    MTexture icon = GFX.Gui["hardcoreicon"];
                    icon.DrawJustified(position, justify, Color.White * alpha, 0.75f);
                }
            }
        }
    }
}
