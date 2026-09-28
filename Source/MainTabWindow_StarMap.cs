using RimWorld;
using UnityEngine;
using Verse;
using StarWarKiiro.StarMap;

namespace StarWarKiiro
{
    // 星图主界面:全屏窗口,内容是独立相机渲染的星系平面图(RT 贴图)
    public class MainTabWindow_StarMap : MainTabWindow
    {
        private static readonly Vector2 FullTab = new Vector2(UI.screenWidth, UI.screenHeight - 35f);

        public MainTabWindow_StarMap()
        {
            draggable = false; // 全屏窗口不允许拖拽
            forcePause = false;
        }

        public override Vector2 RequestedTabSize => FullTab;

        public override void PreClose()
        {
            base.PreClose();
            StarMapScene.Dispose(); // 关闭即销毁场景,下次打开重新生成
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(16f, 12f, 400f, 40f), "星图");
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(inRect.width - 216f, 12f, 200f, 30f), "重新生成星系"))
                StarMapScene.Regenerate();

            var mapRect = new Rect(0f, 60f, inRect.width, inRect.height - 60f);
            HandleMapInput(mapRect);

            StarMapScene.EnsureCreated((int)mapRect.width, (int)mapRect.height);
            StarMapScene.Render();
            GUI.DrawTexture(mapRect, StarMapScene.Texture, ScaleMode.StretchToFill);

            DrawLabels(mapRect);

            Widgets.Label(new Rect(16f, inRect.height - 34f, 500f, 30f),
                "拖拽平移 · 滚轮缩放 · 当前为程序生成占位星系");
        }

        private static void HandleMapInput(Rect mapRect)
        {
            var e = Event.current;
            if (e == null || !mapRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                StarMapScene.Zoom(e.delta.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                StarMapScene.Pan(e.delta);
                e.Use();
            }
        }

        private static void DrawLabels(Rect mapRect)
        {
            var sys = StarMapScene.System;
            if (sys == null) return;

            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            if (StarMapScene.TryGetViewport(Vector3.zero, out var starVp) && starVp is { x: >= 0f and <= 1f, y: >= 0f and <= 1f })
            {
                Text.Font = GameFont.Medium;
                var r = new Rect(starVp.x * mapRect.width + mapRect.x + 14f,
                    (1f - starVp.y) * mapRect.height + mapRect.y - 14f, 200f, 30f);
                Widgets.Label(r, sys.Name);
            }

            Text.Font = GameFont.Small;
            foreach (var p in sys.Planets)
            {
                if (!StarMapScene.TryGetViewport(StarMapScene.PlanetLocalPos(p), out var vp)) continue;
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                var r = new Rect(vp.x * mapRect.width + mapRect.x + 10f,
                    (1f - vp.y) * mapRect.height + mapRect.y - 10f, 160f, 40f);
                if (p == sys.HomePlanet)
                {
                    GUI.color = new Color(0.4f, 1f, 0.9f, 0.95f);
                    Widgets.Label(r, $"母星 · {sys.HomeWorldName}\n{p.TypeName}");
                }
                else
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.7f);
                    Widgets.Label(r, $"{p.Name}\n{p.TypeName}");
                }
            }
            GUI.color = Color.white;
        }
    }
}
