using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArcherSimulator.EditorTools
{
    /// <summary>
    /// Assets/Prefab/Bow 의 활 프리팹(Blink 원본 복사본)을 같은 길이로 맞추고 당김 애니메이터를 붙인다.
    /// FBX마다 내부 스케일이 달라(4.5 / 16.4 / 100) 그대로 두면 활 크기가 제각각이다.
    /// 메뉴: Archer Simulator > 활 프리팹 크기·애니메이터 맞추기
    /// </summary>
    public static class BowPrefabTools
    {
        /// <summary>활 전체 길이 (가장 긴 축, m). 씬 빌더의 활 길이와 같다.</summary>
        public const float BowLength = 1.2f;

        private const string AnimatorFolder = "Assets/Blink/Art/Weapons/Stylized/Animator/";

        /// <summary>강화 단계 순서(Lv1, Lv2, Lv3)의 활 프리팹과 당김 애니메이터 컨트롤러.</summary>
        internal static readonly (string prefab, string controller)[] Bows =
        {
            ("Assets/Prefab/Bow/Bow1_1_1.prefab", AnimatorFolder + "Bow1_1.controller"),
            ("Assets/Prefab/Bow/Bow2_1_1.prefab", AnimatorFolder + "Bow2_1.controller"),
            ("Assets/Prefab/Bow/Bow3_1_1.prefab", AnimatorFolder + "Bow3_1.controller"),
        };

        [MenuItem("Archer Simulator/활 프리팹 크기·애니메이터 맞추기")]
        public static void SetupBowPrefabs()
        {
            var report = new System.Text.StringBuilder();
            foreach (var (prefabPath, controllerPath) in Bows)
                report.AppendLine(Setup(prefabPath, controllerPath));

            AssetDatabase.SaveAssets();
            Debug.Log("[BowPrefabTools]\n" + report);
            EditorUtility.DisplayDialog("활 프리팹", report.ToString(), "확인");
        }

        private static string Setup(string prefabPath, string controllerPath)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return $"{name}: 프리팹 없음 (건너뜀)";

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 크기: 루트 스케일로 가장 긴 축을 BowLength에 맞춘다 (내부 본·메시는 건드리지 않음)
                root.transform.localScale = Vector3.one;
                Bounds bounds = WorldBounds(root);
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                string sizeInfo;
                if (longest > 0.0001f)
                {
                    float scale = BowLength / longest;
                    root.transform.localScale = Vector3.one * scale;
                    sizeInfo = $"{longest:0.###}m → {BowLength}m (×{scale:0.###})";
                }
                else
                {
                    sizeInfo = "크기 계산 실패 (렌더러 없음)";
                }

                // 애니메이터: 해당 FBX의 shotcycle 클립을 쓰는 Blink 컨트롤러 (상태 'Shooting')
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                string controllerInfo = controller != null ? controller.name : "컨트롤러 없음!";
                return $"{name}: {sizeInfo}, Animator = {controllerInfo}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = BoundsOf(renderers[0]);
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(BoundsOf(renderer));
            return bounds;
        }

        /// <summary>프리팹 편집 씬에서는 SkinnedMeshRenderer.bounds가 비어 있을 수 있어 메시 bounds로 대신 계산한다.</summary>
        private static Bounds BoundsOf(Renderer renderer)
        {
            Bounds b = renderer.bounds;
            if (b.size.sqrMagnitude > 0f) return b;

            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer.GetComponent<MeshFilter>() != null ? renderer.GetComponent<MeshFilter>().sharedMesh : null;
            if (mesh == null) return b;

            Bounds local = mesh.bounds;
            Transform t = renderer.transform;
            var world = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one;
                world.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, corner)));
            }
            return world;
        }
    }
}
