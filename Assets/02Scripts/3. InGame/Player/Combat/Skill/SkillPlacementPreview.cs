using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace alpha.ingame.player
{
    /// <summary>설치 홀로그램과 터렛의 공격 범위를 게임 화면에 표시한다.</summary>
    public sealed class SkillPlacementPreview
    {
        private readonly List<Renderer> _renderers = new();
        private readonly MaterialPropertyBlock _properties = new();
        private readonly GameObject _root;
        private readonly Material _material;
        private Mesh _footprintMesh;
        private Mesh _attackAreaMesh;
        private Mesh _attackRangeMesh;
        private MeshRenderer _attackAreaRenderer;

        public SkillPlacementPreview(GameObject prefab, float clearanceRadius, float attackRange = 0f)
        {
            if (prefab == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("스킬 홀로그램용 URP Unlit 셰이더를 찾을 수 없습니다.");
                return;
            }

            _material = new Material(shader) { name = "Skill Placement Hologram" };
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_Blend", 0f);
            _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_ZWrite", 0);
            _material.SetOverrideTag("RenderType", "Transparent");
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.renderQueue = (int)RenderQueue.Transparent;

            _root = new GameObject(prefab.name + " Hologram");
            _root.SetActive(false);
            CopyVisual(prefab.transform, _root.transform);
            if (_renderers.Count == 0) CreateFootprint(clearanceRadius);
            if (attackRange > 0f) CreateAttackRange(attackRange);
        }

        public void Show(Pose pose, bool hasTarget, bool canPlace)
        {
            if (_root == null) return;
            if (_root.activeSelf != hasTarget) _root.SetActive(hasTarget);
            if (!hasTarget) return;

            _root.transform.SetPositionAndRotation(pose.position, pose.rotation);

            Color color = canPlace ? new Color(0.1f, 0.8f, 1f, 0.45f)
                : new Color(1f, 0.18f, 0.12f, 0.55f);
            color.a *= 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
            _properties.SetColor("_BaseColor", color);
            foreach (Renderer renderer in _renderers)
                renderer.SetPropertyBlock(_properties);
            if (_attackAreaRenderer != null)
            {
                color.a *= 0.18f;
                _properties.SetColor("_BaseColor", color);
                _attackAreaRenderer.SetPropertyBlock(_properties);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            if (_material != null) Object.Destroy(_material);
            if (_footprintMesh != null) Object.Destroy(_footprintMesh);
            if (_attackAreaMesh != null) Object.Destroy(_attackAreaMesh);
            if (_attackRangeMesh != null) Object.Destroy(_attackRangeMesh);
        }

        private void CreateFootprint(float radius)
        {
            _footprintMesh = CreateDiscMesh(Mathf.Max(0.05f, radius), 0.03f,
                32, "Skill Placement Footprint");
            AddGroundMarker("Placement Footprint", _footprintMesh);
        }

        private void CreateAttackRange(float radius)
        {
            const int segments = 128;
            float outerRadius = Mathf.Max(0.1f, radius);
            _attackAreaMesh = CreateDiscMesh(outerRadius, 0.04f,
                segments, "Turret Attack Area");
            _attackAreaRenderer = AddGroundMarker("Turret Attack Area", _attackAreaMesh, false);
            float innerRadius = Mathf.Max(0f, outerRadius - Mathf.Clamp(outerRadius * 0.015f, 0.15f, 0.5f));
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(x * innerRadius, 0.05f, z * innerRadius);
                vertices[i * 2 + 1] = new Vector3(x * outerRadius, 0.05f, z * outerRadius);
                if (i == segments) continue;

                int vertex = i * 2;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            _attackRangeMesh = new Mesh { name = "Turret Attack Range" };
            _attackRangeMesh.vertices = vertices;
            _attackRangeMesh.triangles = triangles;
            _attackRangeMesh.RecalculateNormals();
            _attackRangeMesh.RecalculateBounds();
            AddGroundMarker("Turret Attack Range", _attackRangeMesh);
        }

        private static Mesh CreateDiscMesh(float radius, float height, int segments, string name)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = new Vector3(0f, height, 0f);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, height,
                    Mathf.Sin(angle) * radius);
                if (i == segments) continue;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private MeshRenderer AddGroundMarker(string name, Mesh mesh, bool useMainColor = true)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(_root.transform, false);
            // 프리팹 루트가 축소되어 있어도 지정한 반지름을 월드 크기로 표시한다.
            Vector3 rootScale = _root.transform.localScale;
            marker.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(rootScale.x), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(rootScale.y), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(rootScale.z), 0.0001f));
            marker.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (useMainColor) _renderers.Add(renderer);
            return renderer;
        }

        private void CopyVisual(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceRenderer != null)
            {
                destination.gameObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = destination.gameObject.AddComponent<MeshRenderer>();
                renderer.enabled = sourceRenderer.enabled;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials.Length);
                Material[] materials = new Material[materialCount];
                for (int i = 0; i < materialCount; i++) materials[i] = _material;
                renderer.sharedMaterials = materials;
                _renderers.Add(renderer);
            }

            foreach (Transform sourceChild in source)
            {
                GameObject child = new GameObject(sourceChild.name);
                child.transform.SetParent(destination, false);
                CopyVisual(sourceChild, child.transform);
                child.SetActive(sourceChild.gameObject.activeSelf);
            }
        }
    }
}
