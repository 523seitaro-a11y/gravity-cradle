using UnityEngine;
using System.Collections.Generic;

public class MagnetArea : MonoBehaviour
{
    [SerializeField] private bool magnetEnabled = true;
    [SerializeField] private bool showArea = true;
    [SerializeField] private Vector3 localCenter = Vector3.zero;
    [SerializeField] private Vector3 localSize = new Vector3(10f, 0.1f, 10f);
    [SerializeField] private Color areaColor = new Color(0f, 0.45f, 1f, 0.35f);

    private const string TriggerName = "Magnet Area Trigger";
    private const string VisualName = "Magnet Area Visual";
    private static readonly List<MagnetArea> ActiveAreas = new List<MagnetArea>();

    public bool MagnetEnabled => magnetEnabled;
    public static IReadOnlyList<MagnetArea> Areas => ActiveAreas;

    private void Awake()
    {
        ConfigureArea();
    }

    private void OnEnable()
    {
        if (!ActiveAreas.Contains(this))
        {
            ActiveAreas.Add(this);
        }

        ConfigureArea();
    }

    private void OnDisable()
    {
        ActiveAreas.Remove(this);
    }

    private void Reset()
    {
        localSize = EstimateDefaultSize();
        ConfigureArea();
    }

    private void OnValidate()
    {
        localSize = new Vector3(
            Mathf.Max(0.01f, localSize.x),
            Mathf.Max(0.01f, localSize.y),
            Mathf.Max(0.01f, localSize.z));

        ConfigureArea();
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (!magnetEnabled)
        {
            return false;
        }

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint) - localCenter;
        Vector3 halfSize = localSize * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfSize.x
            && Mathf.Abs(localPoint.y) <= halfSize.y
            && Mathf.Abs(localPoint.z) <= halfSize.z;
    }

    public bool IntersectsWorldBounds(Bounds worldBounds)
    {
        if (!magnetEnabled)
        {
            return false;
        }

        if (ContainsWorldPoint(worldBounds.center))
        {
            return true;
        }

        foreach (Vector3 boxCorner in GetBoundsCorners(worldBounds))
        {
            if (ContainsWorldPoint(boxCorner))
            {
                return true;
            }
        }

        foreach (Vector3 areaCorner in GetWorldAreaCorners())
        {
            if (worldBounds.Contains(areaCorner))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureArea()
    {
        RemoveLegacyTriggerCollider();

        ConfigureVisual();
    }

    private void RemoveLegacyTriggerCollider()
    {
        Transform triggerTransform = transform.Find(TriggerName);
        if (triggerTransform == null)
        {
            return;
        }

        Collider[] triggerColliders = triggerTransform.GetComponents<Collider>();
        foreach (Collider triggerCollider in triggerColliders)
        {
            if (Application.isPlaying)
            {
                Destroy(triggerCollider);
            }
            else
            {
                DestroyImmediate(triggerCollider);
            }
        }
    }

    private void ConfigureVisual()
    {
        MeshRenderer visualRenderer = GetOrCreateVisual();
        if (visualRenderer == null)
        {
            return;
        }

        Transform visualTransform = visualRenderer.transform;
        visualTransform.localPosition = localCenter;
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = localSize;
        visualRenderer.enabled = magnetEnabled && showArea;

        Material material = visualRenderer.sharedMaterial;
        if (material == null || material.name != "Magnet Area Visual Material")
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Magnet Area Visual Material";
            visualRenderer.sharedMaterial = material;
        }

        material.color = areaColor;
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private MeshRenderer GetOrCreateVisual()
    {
        Transform visualTransform = transform.Find(VisualName);
        if (visualTransform == null)
        {
            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualObject.name = VisualName;
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
        }

        Collider[] visualColliders = visualTransform.GetComponents<Collider>();
        foreach (Collider visualCollider in visualColliders)
        {
            RemoveCollider(visualCollider);
        }

        return visualTransform.GetComponent<MeshRenderer>();
    }

    private void RemoveCollider(Collider target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private Vector3 EstimateDefaultSize()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            return new Vector3(
                Mathf.Max(0.01f, meshSize.x),
                0.1f,
                Mathf.Max(0.01f, meshSize.z));
        }

        return new Vector3(10f, 0.1f, 10f);
    }

    private static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private Vector3[] GetWorldAreaCorners()
    {
        Vector3 halfSize = localSize * 0.5f;

        return new[]
        {
            transform.TransformPoint(localCenter + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(halfSize.x, -halfSize.y, -halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(-halfSize.x, halfSize.y, -halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(halfSize.x, halfSize.y, -halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(-halfSize.x, -halfSize.y, halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(halfSize.x, -halfSize.y, halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(-halfSize.x, halfSize.y, halfSize.z)),
            transform.TransformPoint(localCenter + new Vector3(halfSize.x, halfSize.y, halfSize.z)),
        };
    }
}
