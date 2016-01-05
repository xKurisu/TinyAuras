﻿using LeagueSharp;
using LeagueSharp.Common;

namespace TinyAuras
{
    internal class Buff
    {
        public float StartTick;
        public float EndTick;
        public string Name;
        public Obj_AI_Base Source;
        public Render.Sprite Sprite;

        public Buff(string name, float start, float end, Obj_AI_Base source)
        {
            Name = name;
            StartTick = start;
            EndTick = end;
            Source = source;
        }
    }
}
