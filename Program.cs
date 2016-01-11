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
        internal static Menu _root, _debug;
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
            _root.AddItem(new MenuItem("loaddebug", "Debug")).SetValue(false);
            _root.AddToMainMenu();

            if (_root.Item("loaddebug").GetValue<bool>())
            {
                _debug = new Menu("TinyDebug", "debugmenu", true);
                _debug.AddItem(new MenuItem("debug", "Debug")).SetValue(true);
                _debug.AddItem(new MenuItem("debugtarget", "Target: "))
                    .SetValue(new StringList(HeroManager.AllHeroes.Select(x => x.Name).ToArray()));
                _debug.AddToMainMenu();
            }

            Game.OnUpdate += Game_OnUpdate;

            Obj_AI_Base.OnBuffAdd += Obj_AI_Base_OnBuffAdd;
            Obj_AI_Base.OnBuffRemove += Obj_AI_Base_OnBuffRemove;
            Obj_AI_Base.OnEnterLocalVisiblityClient += Obj_AI_Base_OnEnterLocalVisiblityClient;

            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Obj_AI_Base_OnEnterLocalVisiblityClient(AttackableUnit sender, EventArgs args)
        {
            var hero = sender as Obj_AI_Hero;
            if (hero != null && hero.IsValid)
            {
                foreach (var b in hero.Buffs)
                {
                    var o = Resources.ResourceManager.GetObject(b.Name);
                     
                    var bmp = (Bitmap) o;
                    if (bmp != null)
                    {
                        NewAura(hero, new Buff(b.Name, b.StartTime, b.EndTime, hero), bmp);
                    }
                }
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            foreach (var hero in HeroManager.AllHeroes)
            {
                if (hero.IsEnemy && !_root.Item("tenemy").GetValue<bool>() ||
                    hero.IsAlly && !hero.IsMe && !_root.Item("tally").GetValue<bool>() ||
                    hero.IsMe && !_root.Item("tself").GetValue<bool>())
                    continue;

                const int endanim = 65;
                const int xoffset = -7;
                int yoffset = hero.IsMe ? 20 : 29;

                if (!hero.IsHPBarRendered)
                    continue;

                foreach (var buff in activebuffs.Values)
                {
                    if (buff.Target.NetworkId != hero.NetworkId)
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
                hero.IsAlly && !hero.IsMe && !_root.Item("tally").GetValue<bool>() ||
                hero.IsMe && !_root.Item("tself").GetValue<bool>())
                return;

            const int endanim = 65;
            const int xoffset = -7;
            int yoffset = hero.IsMe ? 40 : 49;

            if (buff.Target.NetworkId != hero.NetworkId)
                return;

            var b = buff.Sprite = new Render.Sprite(bmp, new Vector2());

            b.Scale = new Vector2(0.2f, 0.2f);
            b.VisibleCondition =
                sender =>
                    hero.IsHPBarRendered && hero.ServerPosition.IsOnScreen() &&
                    activebuffs.ContainsKey(buff.Name + buff.Target.NetworkId);

            b.PositionUpdate = delegate
            {
                var barpos = hero.HPBarPosition;
                var spelltime = 1000 * (buff.EndTick - buff.StartTick);
                var timeleft = buff.EndTick * 1000 - Utils.GameTimeTickCount;

                var xslide = timeleft / (spelltime / endanim);
                var end = endanim + xoffset + xslide;

                return new Vector2(barpos.X + end, barpos.Y + yoffset);
            };

            if (!activebuffs.ContainsKey(buff.Name + buff.Target.NetworkId))
            {
                b.Add();
                activebuffs.Add(buff.Name + buff.Target.NetworkId, buff);
            }
        }

        private static void RemoveAura(Buff buff)
        {
            if (activebuffs.ContainsKey(buff.Name + buff.Target.NetworkId))
            {
                buff.Sprite.Dispose();
                activebuffs.Remove(buff.Name + buff.Target.NetworkId);
            }
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            foreach (var buff in activebuffs.Values)
            {
                var endtick = buff.EndTick * 1000;
                if (endtick - Utils.GameTimeTickCount < 0)
                {
                    RemoveAura(buff);
                    break;
                }

                try
                {
                    if (buff.Target != null && buff.Target.IsVisible && !buff.Target.IsDead)
                    {
                        var storedtick = (int) buff.EndTick;

                        var livetick = buff.Target.GetBuff(buff.Name);
                        if (livetick != null && storedtick != (int) livetick.EndTime)
                        {
                            buff.EndTick = (int) livetick.EndTime;
                            break;
                        }
                    }
                }

                catch (Exception e)
                {
                    Console.WriteLine("Failed to update TinyAura: " + Utils.GameTimeTickCount);
                }
            }
        }

        private static void Obj_AI_Base_OnBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args)
        {
            if (_root.Item("loaddebug").GetValue<bool>())
            {
                if (_debug.Item("debug").GetValue<bool>() && args.Buff.Caster.IsValid)
                {
                    if (args.Buff.Caster.Name == _debug.Item("debugtarget").GetValue<StringList>().SelectedValue)
                    {
                        Console.WriteLine(sender.Name + " : " + args.Buff.Name.ToLower() + " : " + (args.Buff.EndTime - args.Buff.StartTime));
                        Game.PrintChat(sender.Name + " : " + args.Buff.Name.ToLower() + " : " + (args.Buff.EndTime - args.Buff.StartTime));
                    }
                }
            }

            var hero = sender as Obj_AI_Hero;
            if (hero != null && args.Buff.Caster.IsValid)
            {
                var buff = new Buff(args.Buff.Name.ToLower(), args.Buff.StartTime, args.Buff.EndTime, hero);
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
