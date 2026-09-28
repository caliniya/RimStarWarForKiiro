using System.Collections.Generic;
using UnityEngine;

namespace StarWarKiiro.StarMap
{
    // 星系数据模型:纯数据,不涉及渲染。随机生成用独立种子,以后可以固定种子复现同一个星系
    public class StarSystem
    {
        public class Planet
        {
            public string Name;
            public string TypeName;
            public float OrbitRadius;
            public float AngleDeg;
            public float Radius;
            public Color Color;
        }

        public int Seed;
        public string Name;
        public float StarRadius;
        public Color StarColor;
        public bool HasAsteroidBelt;
        public float BeltInner;
        public float BeltOuter;
        public List<Planet> Planets = new List<Planet>();

        // 玩家所在的星球:名字取自 Find.World.info.name(世界生成时官方随机命名)
        public Planet HomePlanet;
        public string HomeWorldName;

        public void AssignHomePlanet(string worldName)
        {
            HomeWorldName = worldName;
            // 优先选岩质行星当母星,没有就取第一颗
            HomePlanet = Planets.Find(p => p.TypeName == "岩质行星") ?? Planets[0];
        }

        private static readonly string[] NamePool =
        {
            "荧惑", "岁星", "辰星", "太白", "镇星",
            "天枢", "天璇", "天玑", "天权", "玉衡", "开阳", "摇光",
            "紫微", "天狼", "织女", "河鼓", "轩辕",
            "毕宿", "参宿", "心宿", "角宿", "斗宿", "牛宿", "奎宿"
        };

        private static readonly Color[] RockyColors =
        {
            new Color(0.75f, 0.70f, 0.62f), new Color(0.62f, 0.50f, 0.40f), new Color(0.60f, 0.60f, 0.65f)
        };

        private static readonly Color[] GasColors =
        {
            new Color(0.72f, 0.62f, 0.45f), new Color(0.45f, 0.55f, 0.70f), new Color(0.60f, 0.50f, 0.65f)
        };

        private static readonly Color[] IceColors =
        {
            new Color(0.75f, 0.85f, 0.95f), new Color(0.68f, 0.80f, 0.88f)
        };

        private static readonly Color[] StarColors =
        {
            new Color(1.00f, 0.90f, 0.55f), new Color(0.95f, 0.95f, 1.00f), new Color(0.70f, 0.82f, 1.00f)
        };

        public static StarSystem Generate(int seed)
        {
            var rng = new System.Random(seed);
            var s = new StarSystem { Seed = seed };

            s.Name = NamePool[rng.Next(NamePool.Length)] + "星系";
            s.StarRadius = 8f + (float)rng.NextDouble() * 3f;
            s.StarColor = StarColors[rng.Next(StarColors.Length)];

            int count = rng.Next(4, 9);
            var nameOrder = new List<int>();
            for (int i = 0; i < NamePool.Length; i++) nameOrder.Add(i);
            // Fisher-Yates 洗牌,取前 count 个当行星名
            for (int i = nameOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (nameOrder[i], nameOrder[j]) = (nameOrder[j], nameOrder[i]);
            }

            float orbit = s.StarRadius + 10f;
            for (int i = 0; i < count; i++)
            {
                orbit += 10f + (float)rng.NextDouble() * 12f;
                var p = new Planet
                {
                    Name = NamePool[nameOrder[i]],
                    OrbitRadius = orbit,
                    AngleDeg = (float)rng.NextDouble() * 360f
                };

                double roll = rng.NextDouble();
                if (roll < 0.50)
                {
                    p.TypeName = "岩质行星";
                    p.Radius = 1.2f + (float)rng.NextDouble() * 1.0f;
                    p.Color = RockyColors[rng.Next(RockyColors.Length)];
                }
                else if (roll < 0.80)
                {
                    p.TypeName = "气态巨行星";
                    p.Radius = 3.0f + (float)rng.NextDouble() * 1.5f;
                    p.Color = GasColors[rng.Next(GasColors.Length)];
                }
                else
                {
                    p.TypeName = "冰冻行星";
                    p.Radius = 2.0f + (float)rng.NextDouble() * 1.0f;
                    p.Color = IceColors[rng.Next(IceColors.Length)];
                }

                s.Planets.Add(p);
            }

            if (rng.NextDouble() < 0.6 && count >= 2)
            {
                // 小行星带:塞进两个相邻轨道之间
                float a = s.Planets[rng.Next(s.Planets.Count - 1)].OrbitRadius;
                float b = s.Planets[s.Planets.Count - 1].OrbitRadius;
                for (int i = 0; i < s.Planets.Count - 1; i++)
                {
                    if (s.Planets[i].OrbitRadius < b) a = s.Planets[i].OrbitRadius;
                }
                s.HasAsteroidBelt = true;
                s.BeltInner = a + 5f;
                s.BeltOuter = s.BeltInner + 4f;
            }

            return s;
        }
    }
}
