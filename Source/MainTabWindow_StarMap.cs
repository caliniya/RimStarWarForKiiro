using RimWorld;
using UnityEngine;
using Verse;
using StarWarKiiro.StarMap;

namespace StarWarKiiro
{
    // 星图主界面:全屏窗口,内容是独立相机渲染的星系平面图(RT 贴图)
    public class MainTabWindow_StarMap : MainTabWindow
    {
        private const float TopBarHeight = 60f;
        private const float PanelWidth = 290f;
        private const float MinHitRadiusPx = 26f;

        // 每次现算,跟随分辨率变化(static readonly 会在类型加载时固化旧值)
        public override Vector2 RequestedTabSize => new Vector2(UI.screenWidth, UI.screenHeight - 35f);

        private enum BodyKind
        {
            None,
            Star,
            Planet,
            Belt
        }

        private BodyKind selectedKind = BodyKind.None;
        private StarSystem.Planet selectedPlanet;
        private Vector2 panelScroll;

        public MainTabWindow_StarMap()
        {
            draggable = false; // 全屏窗口不允许拖拽
            forcePause = false;
        }

        public override void PreClose()
        {
            base.PreClose();
            ClearSelection();
            StarMapScene.Dispose(); // 关闭即销毁场景,下次打开重新生成
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(16f, 12f, 400f, 40f), "星图");
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(inRect.width - 216f, 12f, 200f, 30f), "重新生成星系"))
            {
                ClearSelection();
                StarMapScene.Regenerate();
            }

            // mapRect / mousePosition / Widgets.Label 都在 DoWindowContents 同一坐标空间(内容区左上为原点)
            var mapRect = new Rect(0f, TopBarHeight, inRect.width, inRect.height - TopBarHeight);

            StarMapScene.EnsureCreated((int)mapRect.width, (int)mapRect.height);

            // 详情面板打开时,点击区域避开面板
            var inputRect = mapRect;
            if (selectedKind != BodyKind.None)
                inputRect.width = Mathf.Max(0f, mapRect.width - PanelWidth - 24f);

            HandleMapInput(inputRect, mapRect);

            StarMapScene.Render();
            GUI.DrawTexture(mapRect, StarMapScene.Texture, ScaleMode.StretchToFill);

            DrawLabels(mapRect);
            if (selectedKind != BodyKind.None)
                DrawDetailPanel(inRect);

            Widgets.Label(new Rect(16f, inRect.height - 34f, 780f, 30f),
                "点击星体/小行星带查看详情 · 拖拽平移 · 滚轮缩放 · 空白处取消 · 轨道数据需高等数学 · 详细数据需光学观测");
        }

        private void HandleMapInput(Rect inputRect, Rect mapRect)
        {
            var e = Event.current;
            if (e == null) return;
            if (!inputRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                StarMapScene.Zoom(e.delta.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 按下即选中:避免 MouseUp 抖动/拖拽误判导致点不中
                TrySelectAt(e.mousePosition, mapRect);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                StarMapScene.Pan(e.delta);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                ClearSelection();
                e.Use();
            }
        }

        // 星体圆心(与 Event.current.mousePosition 同一窗口内容坐标空间)
        private static bool TryGetBodyScreen(Vector3 localPos, Rect mapRect, out Vector2 sp)
        {
            sp = Vector2.zero;
            if (!StarMapScene.TryGetViewport(localPos, out var vp)) return false;
            // 视口(0,0 左下) → 内容区坐标(0,0 左上,mapRect 顶边为 y=mapRect.y)
            sp = new Vector2(
                mapRect.x + vp.x * mapRect.width,
                mapRect.y + (1f - vp.y) * mapRect.height);
            return true;
        }

        // 行星名标签布局(与 DrawLabels 一致,body 为内容区坐标)
        private static Rect PlanetLabelRect(Vector2 bodyScreen)
        {
            return new Rect(bodyScreen.x + 10f, bodyScreen.y - 10f, 160f, 40f);
        }

        private void TrySelectAt(Vector2 mousePos, Rect mapRect)
        {
            var sys = StarMapScene.System;
            if (sys == null) return;

            float bestScore = float.MaxValue;
            BodyKind bestKind = BodyKind.None;
            StarSystem.Planet bestPlanet = null;

            void Consider(BodyKind kind, StarSystem.Planet planet, float score)
            {
                if (score < bestScore)
                {
                    bestScore = score;
                    bestKind = kind;
                    bestPlanet = planet;
                }
            }

            void ConsiderDisc(BodyKind kind, StarSystem.Planet planet, Vector2 center, float hitR)
            {
                if (hitR <= 0f) return;
                float dist = Vector2.Distance(center, mousePos);
                if (dist > hitR) return;
                Consider(kind, planet, dist / hitR);
            }

            // 行星:本体圆 + 名称标签
            foreach (var p in sys.Planets)
            {
                if (!TryGetBodyScreen(StarMapScene.PlanetLocalPos(p), mapRect, out var sp)) continue;
                float hitR = Mathf.Max(MinHitRadiusPx, StarMapScene.WorldToScreenRadius(p.Radius, mapRect.height));
                ConsiderDisc(BodyKind.Planet, p, sp, hitR);

                var label = PlanetLabelRect(sp);
                if (label.Contains(mousePos))
                {
                    var labelCenter = new Vector2(label.x + label.width * 0.5f, label.y + label.height * 0.5f);
                    float labelScore = 0.35f + 0.4f * Mathf.Clamp01(Vector2.Distance(labelCenter, mousePos) / 120f);
                    Consider(BodyKind.Planet, p, labelScore);
                }
            }

            // 恒星:判定圈限制在「到最近行星距离」以内,缩小视图时不吞内圈行星
            if (TryGetBodyScreen(Vector3.zero, mapRect, out var starSp))
            {
                float starHitR = Mathf.Max(MinHitRadiusPx + 8f, StarMapScene.WorldToScreenRadius(sys.StarRadius, mapRect.height));
                float nearestPlanetDist = float.MaxValue;
                foreach (var p in sys.Planets)
                {
                    if (!TryGetBodyScreen(StarMapScene.PlanetLocalPos(p), mapRect, out var pp)) continue;
                    nearestPlanetDist = Mathf.Min(nearestPlanetDist, Vector2.Distance(pp, starSp));
                }
                if (nearestPlanetDist < float.MaxValue)
                    starHitR = Mathf.Min(starHitR, Mathf.Max(MinHitRadiusPx * 0.6f, nearestPlanetDist * 0.45f));
                ConsiderDisc(BodyKind.Star, null, starSp, starHitR);
            }

            // 小行星带:环带命中
            if (sys.HasAsteroidBelt && TryGetBodyScreen(Vector3.zero, mapRect, out var beltCenter))
            {
                float innerPx = StarMapScene.WorldToScreenRadius(sys.BeltInner, mapRect.height);
                float outerPx = StarMapScene.WorldToScreenRadius(sys.BeltOuter, mapRect.height);
                float dist = Vector2.Distance(beltCenter, mousePos);
                const float beltSlop = 14f;
                if (dist >= innerPx - beltSlop && dist <= outerPx + beltSlop)
                {
                    float mid = (innerPx + outerPx) * 0.5f;
                    float half = Mathf.Max(1f, (outerPx - innerPx) * 0.5f + beltSlop);
                    Consider(BodyKind.Belt, null, Mathf.Abs(dist - mid) / half);
                }
            }

            switch (bestKind)
            {
                case BodyKind.Planet when bestPlanet != null:
                    SelectPlanet(bestPlanet);
                    break;
                case BodyKind.Star:
                    SelectStar();
                    break;
                case BodyKind.Belt:
                    SelectBelt();
                    break;
                default:
                    ClearSelection();
                    break;
            }
        }

        private void SelectStar()
        {
            selectedKind = BodyKind.Star;
            selectedPlanet = null;
            panelScroll = Vector2.zero;
            var sys = StarMapScene.System;
            if (sys != null)
                StarMapScene.SetSelection(Vector3.zero, sys.StarRadius, true);
        }

        private void SelectPlanet(StarSystem.Planet p)
        {
            selectedKind = BodyKind.Planet;
            selectedPlanet = p;
            panelScroll = Vector2.zero;
            StarMapScene.SetSelection(StarMapScene.PlanetLocalPos(p), p.Radius, true);
        }

        private void SelectBelt()
        {
            selectedKind = BodyKind.Belt;
            selectedPlanet = null;
            panelScroll = Vector2.zero;
            var sys = StarMapScene.System;
            if (sys != null)
            {
                float mid = (sys.BeltInner + sys.BeltOuter) * 0.5f;
                StarMapScene.SetSelection(Vector3.zero, mid, true);
            }
        }

        private void ClearSelection()
        {
            selectedKind = BodyKind.None;
            selectedPlanet = null;
            StarMapScene.SetSelection(Vector3.zero, 0f, false);
        }

        private void DrawLabels(Rect mapRect)
        {
            var sys = StarMapScene.System;
            if (sys == null) return;

            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            if (TryGetBodyScreen(Vector3.zero, mapRect, out var starSp)
                && mapRect.Contains(starSp))
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(starSp.x + 14f, starSp.y - 14f, 200f, 30f), sys.Name);
            }

            Text.Font = GameFont.Small;
            foreach (var p in sys.Planets)
            {
                if (!TryGetBodyScreen(StarMapScene.PlanetLocalPos(p), mapRect, out var sp)) continue;
                if (!mapRect.Contains(sp)) continue;
                var r = PlanetLabelRect(sp);
                if (p == selectedPlanet)
                {
                    GUI.color = new Color(0.4f, 1f, 1f, 1f);
                    Widgets.Label(r, $"▶ {p.Name}\n{p.TypeName}");
                }
                else if (p == sys.HomePlanet)
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

        private void DrawDetailPanel(Rect inRect)
        {
            const float margin = 12f;
            var panel = new Rect(inRect.width - PanelWidth - margin, TopBarHeight - 4f,
                PanelWidth, inRect.height - TopBarHeight - 44f);
            Widgets.DrawMenuSection(panel);

            var inner = panel.ContractedBy(10f);
            var titleRect = new Rect(inner.x, inner.y, inner.width - 28f, 28f);
            var closeRect = new Rect(inner.xMax - 26f, inner.y, 26f, 26f);

            if (Widgets.ButtonInvisible(closeRect))
            {
                ClearSelection();
                return;
            }
            Widgets.Label(closeRect, "✕");

            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            Widgets.Label(titleRect, SelectedTitle());
            Text.Font = GameFont.Small;

            var line = new Rect(inner.x, titleRect.yMax + 4f, inner.width, 1f);
            Widgets.DrawLineHorizontal(line.x, line.y, line.width);

            var scrollOut = new Rect(inner.x, line.yMax + 6f, inner.width, inner.height - (line.yMax - inner.y) - 6f);
            var viewRect = new Rect(0f, 0f, scrollOut.width - 16f, 520f);
            Widgets.BeginScrollView(scrollOut, ref panelScroll, viewRect);

            bool hasOrbitData = StarWarKiiroDefOf.IsFinished(StarWarKiiroDefOf.StarWarKiiro_AdvancedMathematics);
            bool hasDetailData = StarWarKiiroDefOf.IsFinished(StarWarKiiroDefOf.StarWarKiiro_Optics);

            float y = 0f;
            var sys = StarMapScene.System;

            y = Section(viewRect, y, "基础信息");
            if (selectedKind == BodyKind.Star && sys != null)
            {
                y = Row(viewRect, y, "类型", "恒星");
                y = Row(viewRect, y, "星系", sys.Name);
                y = Row(viewRect, y, "行星数量", sys.Planets.Count.ToString());
                y = Row(viewRect, y, "小行星带", sys.HasAsteroidBelt ? "有" : "无");
                y = Row(viewRect, y, "母星", sys.HomeWorldName);
            }
            else if (selectedKind == BodyKind.Planet && selectedPlanet != null && sys != null)
            {
                var p = selectedPlanet;
                y = Row(viewRect, y, "类型", p.TypeName);
                y = Row(viewRect, y, "母星", p == sys.HomePlanet ? $"是 · {sys.HomeWorldName}" : "否");
            }
            else if (selectedKind == BodyKind.Belt && sys != null)
            {
                y = Row(viewRect, y, "类型", "小行星带");
                y = Row(viewRect, y, "所在星系", sys.Name);
            }

            y += 6f;
            y = Section(viewRect, y, "轨道数据");
            if (!hasOrbitData)
            {
                y = Body(viewRect, y, "研究「高等数学」后解锁轨道参数。");
            }
            else if (selectedKind == BodyKind.Star && sys != null)
            {
                y = Row(viewRect, y, "本体半径", $"{sys.StarRadius:0.0}");
                y = Body(viewRect, y, "恒星位于系统中心，是各行星轨道的焦点。");
            }
            else if (selectedKind == BodyKind.Planet && selectedPlanet != null)
            {
                var p = selectedPlanet;
                y = Row(viewRect, y, "轨道半径", $"{p.OrbitRadius:0.0}");
                y = Row(viewRect, y, "当前相位", $"{p.AngleDeg:0}°");
                y = Row(viewRect, y, "本体半径", $"{p.Radius:0.0}");
            }
            else if (selectedKind == BodyKind.Belt && sys != null && sys.HasAsteroidBelt)
            {
                y = Row(viewRect, y, "内径", $"{sys.BeltInner:0.0}");
                y = Row(viewRect, y, "外径", $"{sys.BeltOuter:0.0}");
                y = Row(viewRect, y, "中径", $"{(sys.BeltInner + sys.BeltOuter) * 0.5f:0.0}");
            }

            y += 6f;
            y = Section(viewRect, y, "详细数据");
            if (!hasDetailData)
            {
                y = Body(viewRect, y, "研究「光学观测」后解锁详细观测数据。");
            }
            else
            {
                y = Body(viewRect, y, "详细观测数据待实装。后续将提供成分分析、资源评估与威胁等级等信息。");
            }

            Widgets.EndScrollView();
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private string SelectedTitle()
        {
            var sys = StarMapScene.System;
            switch (selectedKind)
            {
                case BodyKind.Star:
                    return sys?.Name ?? "恒星";
                case BodyKind.Belt:
                    return "小行星带";
                case BodyKind.Planet when selectedPlanet != null:
                    if (sys != null && selectedPlanet == sys.HomePlanet)
                        return $"母星 · {sys.HomeWorldName}";
                    return selectedPlanet.Name;
                default:
                    return "星体";
            }
        }

        private static float Section(Rect viewRect, float y, string title)
        {
            const float h = 26f;
            GUI.color = new Color(0.55f, 0.85f, 1f, 0.9f);
            Widgets.Label(new Rect(0f, y, viewRect.width, h), title);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            Widgets.DrawLineHorizontal(0f, y + h - 4f, viewRect.width);
            GUI.color = Color.white;
            return y + h;
        }

        private static float Row(Rect viewRect, float y, string label, string value)
        {
            const float h = 24f;
            var lr = new Rect(0f, y, viewRect.width * 0.38f, h);
            var vr = new Rect(viewRect.width * 0.38f, y, viewRect.width * 0.62f, h);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            Widgets.Label(lr, label);
            GUI.color = Color.white;
            Widgets.Label(vr, value);
            return y + h;
        }

        private static float Body(Rect viewRect, float y, string text)
        {
            var r = new Rect(0f, y, viewRect.width, 72f);
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            Widgets.Label(r, text);
            GUI.color = Color.white;
            return y + 72f;
        }
    }
}
