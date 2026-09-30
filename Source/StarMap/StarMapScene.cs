using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace StarWarKiiro.StarMap
{
    // 星图渲染场景:独立相机 + RenderTexture。
    // 场景整体放在主地图上空 10 万单位处,相机的远裁剪面很短,
    // 所以主场景的所有几何体天然被剔除,不需要注册新 Layer
    [StaticConstructorOnStartup]
    public static class StarMapScene
    {
        private const float SceneHeight = 100000f;

        private static GameObject root;
        private static GameObject systemRoot;
        private static GameObject orbitsRoot;
        private static GameObject selectionRoot;
        private static Camera cam;
        private static RenderTexture rt;
        private static StarSystem system;
        private static Shader shader;
        // 所有程序生成的 Mesh:GameObject 销毁不会释放 Mesh 资产,需登记后统一销毁防泄漏
        private static readonly List<Mesh> createdMeshes = new List<Mesh>();

        public static StarSystem System => system;
        public static RenderTexture Texture => rt;

        private static Shader GetShader()
        {
            if (shader != null) return shader;
            // 按优先级找游戏包体里自带的纯色着色器
            foreach (var name in new[] { "Sprites/Default", "Unlit/Color", "GUI/Text Shader" })
            {
                shader = Shader.Find(name);
                if (shader != null) return shader;
            }
            Log.Warning("[StarWarKiiro] 找不到可用的无光照着色器,星图可能不显示");
            return null;
        }

        public static void EnsureCreated(int width, int height)
        {
            if (root == null)
            {
                // 星系数据来自存档组件(种子固化),场景只是它的一个可视化,可随时重建
                system = Find.World?.GetComponent<WorldComponent_StarSystem>()?.System;
                if (system == null) return;
                shader = GetShader();

                root = new GameObject("StarWarKiiro_StarMap");
                root.transform.position = new Vector3(0f, SceneHeight, 0f);

                systemRoot = new GameObject("System");
                systemRoot.transform.SetParent(root.transform, false);
                BuildSystem();

                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 1000f, 0f);
                camGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 俯视 XZ 平面
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 160f;
                cam.nearClipPlane = 100f;
                cam.farClipPlane = 3000f;
                cam.cullingMask = -1;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.015f, 0.02f, 0.05f);
                cam.enabled = false; // 只在星图窗口打开时手动 Render
            }

            int w = Mathf.Max(64, width);
            int h = Mathf.Max(64, height);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null)
                {
                    cam.targetTexture = null;
                    rt.Release();
                    Object.Destroy(rt);
                }
                rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt;
            }
        }

        public static void Regenerate()
        {
            if (root == null) return;
            SetSelection(Vector3.zero, 0f, false);
            Find.World?.GetComponent<WorldComponent_StarSystem>()?.RerollSeed();
            system = Find.World?.GetComponent<WorldComponent_StarSystem>()?.System;
            if (system == null) return;
            BuildSystem();
        }

        public static void Dispose()
        {
            DestroySelectionMeshes();
            DestroySystemMeshes();
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
                rt = null;
            }
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
            cam = null;
            systemRoot = null;
            orbitsRoot = null;
            selectionRoot = null;
        }

        public static void Render()
        {
            UpdateResearchVisibility();
            if (cam != null && rt != null) cam.Render();
        }

        // 轨道线需要「高等数学」;其余(恒星/行星/小行星带)在星图解锁时即可见
        private static void UpdateResearchVisibility()
        {
            if (orbitsRoot != null)
                orbitsRoot.SetActive(StarWarKiiroDefOf.IsFinished(StarWarKiiroDefOf.StarWarKiiro_AdvancedMathematics));
        }

        // 窗口里做平移:屏幕像素位移换算成世界位移
        public static void Pan(Vector2 screenDelta)
        {
            if (cam == null || rt == null) return;
            float k = cam.orthographicSize * 2f / rt.height;
            var pos = cam.transform.position;
            pos.x -= screenDelta.x * k;
            pos.z += screenDelta.y * k; // 相机 up 轴对应世界 +Z
            cam.transform.position = pos;
        }

        public static void Zoom(float wheelDelta)
        {
            if (cam == null) return;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Pow(1.1f, wheelDelta), 15f, 600f);
        }

        // 星图数据坐标(XZ 平面) → 相机视口坐标(0~1),供窗口画行星名标签
        public static bool TryGetViewport(Vector3 sceneLocalPos, out Vector2 viewport)
        {
            viewport = Vector2.zero;
            if (cam == null || root == null) return false;
            var world = root.transform.position + sceneLocalPos;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f) return false;
            viewport = new Vector2(vp.x, vp.y);
            return true;
        }

        public static Vector3 PlanetLocalPos(StarSystem.Planet p)
        {
            float rad = p.AngleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * p.OrbitRadius, 0f, Mathf.Sin(rad) * p.OrbitRadius);
        }

        // 世界半径 → 屏幕像素半径(以 GUI 地图区高度为准,和点击判定同一坐标空间)
        public static float WorldToScreenRadius(float worldRadius, float mapHeight)
        {
            if (cam == null) return worldRadius;
            return worldRadius / (cam.orthographicSize * 2f) * mapHeight;
        }

        // 选中高亮:在目标位置画一圈亮环。radius 为世界单位(略大于本体)
        public static void SetSelection(Vector3 localPos, float worldRadius, bool active)
        {
            if (root == null || shader == null) return;

            if (selectionRoot != null)
            {
                DestroySelectionMeshes();
                Object.Destroy(selectionRoot);
                selectionRoot = null;
            }
            if (!active) return;

            selectionRoot = new GameObject("Selection");
            selectionRoot.transform.SetParent(root.transform, false);

            float outer = worldRadius + 1.6f;
            float inner = worldRadius + 0.7f;
            var ring = AddMeshObject(selectionRoot.transform, localPos,
                RingMesh(inner, outer, 96), new Color(0.35f, 0.95f, 1f, 0.95f));
            // 稍抬高一点,避免和本体面片共面闪烁
            if (ring != null) ring.transform.localPosition += new Vector3(0f, 0.5f, 0f);

            var outerRing = AddMeshObject(selectionRoot.transform, localPos,
                RingMesh(outer + 0.8f, outer + 1.15f, 96), new Color(0.35f, 0.95f, 1f, 0.4f));
            if (outerRing != null) outerRing.transform.localPosition += new Vector3(0f, 0.5f, 0f);
        }

        private static void BuildSystem()
        {
            for (int i = systemRoot.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(systemRoot.transform.GetChild(i).gameObject);
            DestroySystemMeshes();

            orbitsRoot = new GameObject("Orbits");
            orbitsRoot.transform.SetParent(systemRoot.transform, false);

            var s = system;
            // 恒星 + 行星 + 小行星带:天文学解锁星图后即可见
            AddDisc(systemRoot.transform, Vector3.zero, s.StarRadius, s.StarColor);

            if (s.HasAsteroidBelt)
                AddRing(systemRoot.transform, (s.BeltInner + s.BeltOuter) / 2f, s.BeltOuter - s.BeltInner,
                    new Color(0.5f, 0.5f, 0.55f, 0.35f));

            foreach (var p in s.Planets)
            {
                // 轨道线单独挂在 orbitsRoot,由高等数学控制显隐
                AddRing(orbitsRoot.transform, p.OrbitRadius, 0.35f, new Color(1f, 1f, 1f, 0.16f));
                AddDisc(systemRoot.transform, PlanetLocalPos(p), p.Radius, p.Color);
                if (p == s.HomePlanet)
                {
                    // 母星高亮:青色双环(不是轨道,始终显示)
                    AddRing(systemRoot.transform, p.Radius + 1.0f, 0.3f, new Color(0.3f, 1f, 0.85f, 0.9f));
                    AddRing(systemRoot.transform, p.Radius + 1.8f, 0.2f, new Color(0.3f, 1f, 0.85f, 0.5f));
                }
            }

            UpdateResearchVisibility();
        }

        private static void AddDisc(Transform parent, Vector3 localPos, float radius, Color color)
        {
            AddMeshObject(parent, localPos, DiscMesh(radius), color);
        }

        private static void AddRing(Transform parent, float radius, float thickness, Color color)
        {
            AddMeshObject(parent, Vector3.zero, RingMesh(radius - thickness / 2f, radius + thickness / 2f), color);
        }

        private static GameObject AddMeshObject(Transform parent, Vector3 localPos, Mesh mesh, Color color)
        {
            if (shader == null) return null;
            createdMeshes.Add(mesh);
            var go = new GameObject("mesh");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(shader) { color = color };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static void DestroySelectionMeshes()
        {
            // 选中环的 Mesh 挂在 selectionRoot 下,重建/关闭时释放
            if (selectionRoot == null) return;
            foreach (Transform child in selectionRoot.transform)
            {
                var mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    createdMeshes.Remove(mf.sharedMesh);
                    Object.Destroy(mf.sharedMesh);
                }
            }
        }

        private static void DestroySystemMeshes()
        {
            // 释放星系本体的所有网格(恒星/行星/轨道/母星环)
            for (int i = createdMeshes.Count - 1; i >= 0; i--)
                Object.Destroy(createdMeshes[i]);
            createdMeshes.Clear();
        }

        // 圆盘网格:中心 + 圆周三角扇,正反两面都生成索引,免得纠结绕序/背面剔除
        private static Mesh DiscMesh(float radius, int seg = 64)
        {
            var verts = new Vector3[seg + 1];
            verts[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % seg;
                tris[i * 6] = 0; tris[i * 6 + 1] = b; tris[i * 6 + 2] = a;
                tris[i * 6 + 3] = 0; tris[i * 6 + 4] = a; tris[i * 6 + 5] = b;
            }
            var m = new Mesh { vertices = verts, triangles = tris };
            m.RecalculateNormals();
            return m;
        }

        // 圆环网格:内外两圈顶点连成四边形带
        private static Mesh RingMesh(float inner, float outer, int seg = 128)
        {
            var verts = new Vector3[seg * 2];
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
            }
            var tris = new int[seg * 12];
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                int a = i * 2, b = i * 2 + 1, c = j * 2, d = j * 2 + 1;
                int o = i * 12;
                tris[o] = a; tris[o + 1] = b; tris[o + 2] = c;
                tris[o + 3] = b; tris[o + 4] = d; tris[o + 5] = c;
                tris[o + 6] = a; tris[o + 7] = c; tris[o + 8] = b;
                tris[o + 9] = b; tris[o + 10] = c; tris[o + 11] = d;
            }
            var m = new Mesh { vertices = verts, triangles = tris };
            m.RecalculateNormals();
            return m;
        }
    }
}
