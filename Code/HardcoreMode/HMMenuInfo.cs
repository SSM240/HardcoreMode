using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HardcoreMode
{
    /// <summary>
    /// Text menu item for showing the "return to safe ground" message.
    /// </summary>
    public class HMMenuInfo : TextMenu.Item
    {
        public const float Scale = 0.7f;

        public string Text;

        public HMMenuInfo(string text)
        {
            Text = text;
            Selectable = false;
        }

        public override float LeftWidth()
        {
            return ActiveFont.Measure(Text).X * Scale;
        }

        public override float Height()
        {
            return ActiveFont.LineHeight * Scale;
        }

        public override void Render(Vector2 position, bool highlighted)
        {
            if (Text.Length > 0)
            {
                float alpha = Container.Alpha;
                Color fillColor = Calc.HexToColor("ff7777") * alpha;
                Color strokeColor = Color.Black * (alpha * alpha * alpha);
                position += new Vector2(Container.Width * 0.5f, 16f);
                Vector2 justify = new Vector2(0.5f, 0.5f);
                ActiveFont.DrawOutline(Text, position, justify, Vector2.One * Scale, fillColor, 2f, strokeColor);
            }
        }
    }
}
