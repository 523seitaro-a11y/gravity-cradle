using UnityEngine;

public class PushOnlyBox : MonoBehaviour
{
    [Header("Magnet")]
    [SerializeField] private bool affectedByMagnetAreas;
    [SerializeField] private float pushVelocityThreshold = 0.05f;

    [Header("Gravity Suppressed Outline")]
    [SerializeField] private Color gravitySuppressedOutlineColor = Color.red;
    [Min(0.001f)]
    [SerializeField] private float gravitySuppressedOutlineWidth = 0.04f;

    private Rigidbody body;
    private Collider ownCollider;
    private LineRenderer[] outlineEdges;
    private Material outlineMaterial;
    private Vector3 gravityDirection = Vector3.down;
    private bool isTouchingMagnetArea;
    private bool isBeingPushed;
    private bool wasMagnetPositionLocked;

    private const string OutlineRootName = "Gravity Suppressed Outline";

    public bool IsGravitySuppressed => affectedByMagnetAreas && isTouchingMagnetArea;

    private void OnValidate()
    {
        gravitySuppressedOutlineWidth = Mathf.Max(0.001f, gravitySuppressedOutlineWidth);

        if (outlineEdges != null)
        {
            ApplyOutlineSettings();
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        ownCollider = GetComponent<Collider>();
        body.useGravity = false;
        body.freezeRotation = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        ConfigureOutline();
        UpdateOutlineVisibility();
    }

    public void SetGravityDirection(Vector3 direction)
    {
        gravityDirection = direction.normalized;
    }

    public void SetOutlineSettings(Color color, float width)
    {
        gravitySuppressedOutlineColor = color;
        gravitySuppressedOutlineWidth = Mathf.Max(0.001f, width);

        if (outlineEdges != null)
        {
            ApplyOutlineSettings();
        }
    }

    private void FixedUpdate()
    {
        isTouchingMagnetArea = affectedByMagnetAreas && CheckMagnetAreaContact();
        ApplyMagnetPositionLock();
        UpdateOutlineVisibility();

        if (!isBeingPushed)
        {
            StopUnwantedMotion();
        }

        isBeingPushed = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        GravityPlayerController player = collision.collider.GetComponentInParent<GravityPlayerController>();
        if (player == null || player.Body == null)
        {
            return;
        }

        Vector3 pushDirection = transform.position - player.transform.position;
        if (pushDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float pushSpeed = Vector3.Dot(player.Body.linearVelocity, pushDirection.normalized);
        isBeingPushed = pushSpeed > pushVelocityThreshold;
    }

    private bool CheckMagnetAreaContact()
    {
        if (ownCollider == null)
        {
            return false;
        }

        Bounds boxBounds = ownCollider.bounds;
        foreach (MagnetArea area in MagnetArea.Areas)
        {
            if (area != null && area.IntersectsWorldBounds(boxBounds))
            {
                return true;
            }
        }

        return false;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponentInParent<GravityPlayerController>() != null)
        {
            isBeingPushed = false;
            StopUnwantedMotion();
        }
    }

    private void StopUnwantedMotion()
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.linearVelocity = IsGravitySuppressed
            ? Vector3.zero
            : Vector3.Project(body.linearVelocity, gravityDirection);
        body.angularVelocity = Vector3.zero;
    }

    private void ApplyMagnetPositionLock()
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        bool shouldLockPosition = IsGravitySuppressed && !isBeingPushed;
        RigidbodyConstraints nextConstraints = shouldLockPosition
            ? RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation
            : RigidbodyConstraints.FreezeRotation;

        if (body.constraints != nextConstraints)
        {
            body.constraints = nextConstraints;
        }

        if (shouldLockPosition)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else if (wasMagnetPositionLocked)
        {
            body.WakeUp();
        }

        wasMagnetPositionLocked = shouldLockPosition;
    }

    private void ConfigureOutline()
    {
        Transform outlineRoot = transform.Find(OutlineRootName);
        if (outlineRoot == null)
        {
            GameObject outlineObject = new GameObject(OutlineRootName);
            outlineRoot = outlineObject.transform;
            outlineRoot.SetParent(transform, false);
        }

        outlineEdges = outlineRoot.GetComponentsInChildren<LineRenderer>(true);
        if (outlineEdges.Length != 12)
        {
            foreach (LineRenderer edge in outlineEdges)
            {
                if (Application.isPlaying)
                {
                    Destroy(edge.gameObject);
                }
                else
                {
                    DestroyImmediate(edge.gameObject);
                }
            }

            outlineEdges = new LineRenderer[12];
            for (int i = 0; i < outlineEdges.Length; i++)
            {
                GameObject edgeObject = new GameObject($"Edge {i + 1}");
                edgeObject.transform.SetParent(outlineRoot, false);
                outlineEdges[i] = edgeObject.AddComponent<LineRenderer>();
            }
        }

        ApplyOutlineSettings();

        UpdateOutlineShape();
    }

    private void ApplyOutlineSettings()
    {
        if (outlineEdges == null)
        {
            return;
        }

        for (int i = 0; i < outlineEdges.Length; i++)
        {
            LineRenderer edge = outlineEdges[i];
            if (edge == null)
            {
                continue;
            }

            edge.useWorldSpace = false;
            edge.positionCount = 2;
            edge.startWidth = gravitySuppressedOutlineWidth;
            edge.endWidth = gravitySuppressedOutlineWidth;
            edge.startColor = gravitySuppressedOutlineColor;
            edge.endColor = gravitySuppressedOutlineColor;
            edge.material = GetOrCreateOutlineMaterial();
        }
    }

    private void UpdateOutlineShape()
    {
        if (ownCollider == null || outlineEdges == null || outlineEdges.Length != 12)
        {
            return;
        }

        Bounds bounds = ownCollider.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            transform.InverseTransformPoint(new Vector3(min.x, min.y, min.z)),
            transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)),
            transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)),
            transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)),
            transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)),
            transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)),
            transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z)),
            transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)),
        };

        SetOutlineEdge(0, corners[0], corners[1]);
        SetOutlineEdge(1, corners[1], corners[2]);
        SetOutlineEdge(2, corners[2], corners[3]);
        SetOutlineEdge(3, corners[3], corners[0]);
        SetOutlineEdge(4, corners[4], corners[5]);
        SetOutlineEdge(5, corners[5], corners[6]);
        SetOutlineEdge(6, corners[6], corners[7]);
        SetOutlineEdge(7, corners[7], corners[4]);
        SetOutlineEdge(8, corners[0], corners[4]);
        SetOutlineEdge(9, corners[1], corners[5]);
        SetOutlineEdge(10, corners[2], corners[6]);
        SetOutlineEdge(11, corners[3], corners[7]);
    }

    private void SetOutlineEdge(int index, Vector3 start, Vector3 end)
    {
        outlineEdges[index].SetPosition(0, start);
        outlineEdges[index].SetPosition(1, end);
    }

    private void UpdateOutlineVisibility()
    {
        if (outlineEdges == null)
        {
            return;
        }

        bool showOutline = IsGravitySuppressed;
        foreach (LineRenderer edge in outlineEdges)
        {
            edge.enabled = showOutline;
        }
    }

    private Material GetOrCreateOutlineMaterial()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.color = gravitySuppressedOutlineColor;
            return outlineMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        outlineMaterial = new Material(shader);
        outlineMaterial.name = "Gravity Suppressed Outline Material";
        outlineMaterial.color = gravitySuppressedOutlineColor;
        return outlineMaterial;
    }
}
