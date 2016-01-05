using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using TinyAuras.Properties;

namespace TinyAuras
{
    internal class Program
    {
        internal static Menu _root;
        static void Main(string[] args)
        {
            // super duper simple minamalistic aura tracker :^)
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        internal static Obj_AI_Hero Player => ObjectManager.Player;
        private static Dictionary<string, Buff> activebuffs = new Dictionary<string, Buff>();

        private static readonly Render.Text text = 
            new Render.Text(0, 0, "", 11, new ColorBGRA(255, 255, 255, 255), "monospace");

        private static readonly Render.Text timer = 
            new Render.Text(0, 0, "", 11, new ColorBGRA(255, 255, 255, 255), "monospace");
        private static void Game_OnGameLoad(EventArgs args)
        {
            _root = new Menu("TinyAuras", "tinyauras", true);
            _root.AddItem(new MenuItem("tself", "Track Self")).SetValue(true);
            _root.AddItem(new MenuItem("tally", "Track Allies")).SetValue(true);
            _root.AddItem(new MenuItem("tenemy", "Track Enemies")).SetValue(true);
            _root.AddItem(new MenuItem("debug", "Debug")).SetValue(false);
            _root.AddToMainMenu();

            Game.OnUpdate += Game_OnUpdate;

            Obj_AI_Base.OnBuffAdd += Obj_AI_Base_OnBuffAdd;
            Obj_AI_Base.OnBuffRemove += Obj_AI_Base_OnBuffRemove;
            Drawing.OnDraw += Drawing_OnDraw;

        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            foreach (var hero in HeroManager.AllHeroes)
            {
                if (hero.IsEnemy && !_root.Item("tenemy").GetValue<bool>() ||
                    hero.IsAlly && !_root.Item("tally").GetValue<bool>() ||
                    hero.IsMe && !_root.Item("tself").GetValue<bool>())
                    continue;

                const int endanim = 65;
                const int xoffset = -7;
                int yoffset = hero.IsMe ? 20 : 29;

                if (!hero.IsHPBarRendered)
                    continue;

                foreach (var buff in activebuffs.Values)
                {
                    if (buff.Source.NetworkId != hero.NetworkId)
                        continue;

                    var barpos = hero.HPBarPosition;
                    var spelltime = 1000 * (buff.EndTick - buff.StartTick);
                    var timeleft = buff.EndTick * 1000 - Utils.GameTimeTickCount;

                    var xslide = timeleft / (spelltime / endanim);
                    var end = endanim + xoffset + xslide;

                    timer.X = (int) (barpos.X + end) + 15;
                    timer.Y = (int) barpos.Y + yoffset + 21;

                    timer.text = (buff.EndTick - Game.Time).ToString("0.0");
                    timer.OnEndScene();

                    Drawing.DrawLine(barpos.X + end + 4, barpos.Y + yoffset + 15, barpos.X + 57,
                        barpos.Y + yoffset + 15, 1, System.Drawing.Color.White);

                    text.X = (int) (barpos.X + end) + 3;
                    text.Y = (int) barpos.Y + yoffset + 13;
                    text.text = "l";
                    text.OnEndScene();
                }
            }
        }

        private static void NewAura(Obj_AI_Hero hero, Buff buff, Bitmap bmp)
        {
            if (hero.IsEnemy && !_root.Item("tenemy").GetValue<bool>() ||
                hero.IsAlly && !_root.Item("tally").GetValue<bool>() ||
                hero.IsMe && !_root.Item("tself").GetValue<bool>())
                return;

            const int endanim = 65;
            const int xoffset = -7;
            int yoffset = hero.IsMe ? 40 : 49;

            if (buff.Source.NetworkId != hero.NetworkId)
                return;

            var b = buff.Sprite = new Render.Sprite(bmp, new Vector2());

            b.Scale = new Vector2(0.2f, 0.2f);
            b.VisibleCondition = sender => hero.IsHPBarRendered && activebuffs.ContainsKey(buff.Name + buff.Source.NetworkId);
            b.PositionUpdate = delegate
            {
                var barpos = hero.HPBarPosition;
                var spelltime = 1000 * (buff.EndTick - buff.StartTick);
                var timeleft = buff.EndTick * 1000 - Utils.GameTimeTickCount;

                var xslide = timeleft / (spelltime / endanim);
                var end = endanim + xoffset + xslide;

                return new Vector2(barpos.X + end, barpos.Y + yoffset);
            };

            if (!activebuffs.ContainsKey(buff.Name + buff.Source.NetworkId))
            {
                b.Add();
                activebuffs.Add(buff.Name + buff.Source.NetworkId, buff);
            }
        }

        private static void RemoveAura(Buff buff)
        {
            if (activebuffs.ContainsKey(buff.Name + buff.Source.NetworkId))
            {
                buff.Sprite.Dispose();
                activebuffs.Remove(buff.Name + buff.Source.NetworkId);
            }
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            foreach (var buff in activebuffs.Values)
            {
                var livetick = (int) buff.Source.GetBuff(buff.Name).EndTime;
                var storedtick = (int) buff.EndTick;

                if (livetick != storedtick)
                {
                    buff.EndTick = livetick;
                    break;
                }

                var endtick = buff.EndTick * 1000;
                if (endtick - Utils.GameTimeTickCount < 0)
                {
                    RemoveAura(buff);
                    break;
                }
            }
        }

        private static void Obj_AI_Base_OnBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args)
        {
            if (_root.Item("debug").GetValue<bool>() && args.Buff.Caster.IsValid && args.Buff.Caster.IsMe)
            {
                Console.WriteLine(sender.Name + " : " + args.Buff.Name.ToLower() + " : " + (args.Buff.EndTime - args.Buff.StartTime));
                Game.PrintChat(sender.Name + " : " + args.Buff.Name.ToLower() + " : " + (args.Buff.EndTime - args.Buff.StartTime));
            }

            var hero = sender as Obj_AI_Hero;
            if (hero != null && args.Buff.Caster.IsValid)
            {
                var buff = new Buff(args.Buff.Name.ToLower(), args.Buff.StartTime, args.Buff.EndTime, sender);
                var o = Resources.ResourceManager.GetObject(args.Buff.Name.ToLower());

                var bmp = (Bitmap) o;
                if (bmp != null)
                {
                    NewAura(hero, buff, bmp);
                }
            }
        }      

        private static void Obj_AI_Base_OnBuffRemove(Obj_AI_Base sender, Obj_AI_BaseBuffRemoveEventArgs args)
        {
            var buff = activebuffs.Values.FirstOrDefault(v => v.Name == args.Buff.Name.ToLower());
            if (buff != null && args.Buff.Caster.IsValid)
            {
                RemoveAura(buff);
            }
        }
    }
}
