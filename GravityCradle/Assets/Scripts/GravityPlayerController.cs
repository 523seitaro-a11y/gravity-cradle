using UnityEngine;
using UnityEngine.InputSystem;

public class GravityPlayerController : MonoBehaviour
{
    private enum LocalAxis
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ,
    }

    [SerializeField] private Camera movementCamera;
    [SerializeField] private Transform[] eyes;
    [SerializeField] private LocalAxis frontAxis = LocalAxis.PositiveX;
    [SerializeField] private LocalAxis upAxis = LocalAxis.PositiveY;
    [SerializeField] private float eyeForwardDistance = 0.5f;
    [SerializeField] private float eyeVerticalOffset = 0.1f;
    [SerializeField] private float eyeHorizontalSpacing = 0.25f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravityAcceleration = 9.81f;
    [SerializeField] private float groundCheckDistance = 1.15f;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private float groundNormalMinDot = 0.5f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody body;
    private Collider ownCollider;
    private float[] eyeForwardOffsets;
    private float[] eyeVerticalOffsets;
    private float[] eyeHorizontalOffsets;
    private Vector3 gravityDirection = Vector3.down;
    private Vector3 lastMoveDirection;
    private Quaternion pausedWorldRotation;
    private bool hasMoveInput;
    private bool isPausedByStage;
    private bool wasGrounded;

    public Rigidbody Body => body;

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

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        InitializeEyeLayout();
    }

    private void FixedUpdate()
    {
        if (isPausedByStage)
        {
            return;
        }

        bool isGrounded = IsGrounded();
        if (isGrounded && !wasGrounded)
        {
            AlignEyesToGravity();
        }

        wasGrounded = isGrounded;

        body.AddForce(gravityDirection * gravityAcceleration, ForceMode.Acceleration);

        if (isGrounded)
        {
            ApplyGroundMovement();
            AlignEyesToMoveDirection();
        }
    }

    public void SetGravity(Vector3 direction, float acceleration)
    {
        gravityDirection = direction.normalized;
        gravityAcceleration = acceleration;
    }

    public void PauseForStageRotation(Transform stageRoot)
    {
        isPausedByStage = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        pausedWorldRotation = transform.rotation;

        if (stageRoot != null)
        {
            transform.SetParent(stageRoot, true);
            transform.rotation = pausedWorldRotation;
        }
    }

    public void ResumeAfterStageRotation(Vector3 nextGravityDirection, float nextGravityAcceleration)
    {
        transform.SetParent(null, true);
        transform.rotation = pausedWorldRotation;
        SetGravity(nextGravityDirection, nextGravityAcceleration);

        wasGrounded = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.isKinematic = false;
        isPausedByStage = false;
        body.WakeUp();
    }

    private bool IsGrounded()
    {
        float extent = GetExtentAlongGravity();
        Vector3 origin = transform.position - gravityDirection * (extent + 0.05f);
        float distance = extent + groundCheckDistance;

        RaycastHit[] hits = Physics.RaycastAll(origin, gravityDirection, distance, groundMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (IsGroundHit(hit))
            {
                return true;
            }
        }

        float radius = GetGroundCheckRadius();
        RaycastHit[] sphereHits = Physics.SphereCastAll(origin, radius, gravityDirection, distance, groundMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in sphereHits)
        {
            if (IsGroundHit(hit))
            {
                return true;
            }
        }

        foreach (Vector3 offset in GetGroundCheckOffsets(radius))
        {
            RaycastHit[] offsetHits = Physics.RaycastAll(origin + offset, gravityDirection, distance, groundMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in offsetHits)
            {
                if (IsGroundHit(hit))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float GetExtentAlongGravity()
    {
        if (ownCollider == null)
        {
            return 0.5f;
        }

        Vector3 extents = ownCollider.bounds.extents;
        Vector3 direction = gravityDirection;
        return Mathf.Abs(direction.x) * extents.x
            + Mathf.Abs(direction.y) * extents.y
            + Mathf.Abs(direction.z) * extents.z;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        return hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    private bool IsGroundHit(RaycastHit hit)
    {
        if (IsOwnCollider(hit.collider))
        {
            return false;
        }

        return Vector3.Dot(hit.normal, -gravityDirection) >= groundNormalMinDot;
    }

    private float GetGroundCheckRadius()
    {
        if (ownCollider == null)
        {
            return groundCheckRadius;
        }

        Vector3 extents = ownCollider.bounds.extents;
        Vector3 gravity = new Vector3(Mathf.Abs(gravityDirection.x), Mathf.Abs(gravityDirection.y), Mathf.Abs(gravityDirection.z));
        Vector3 perpendicularExtents = new Vector3(
            gravity.x > 0.5f ? 0f : extents.x,
            gravity.y > 0.5f ? 0f : extents.y,
            gravity.z > 0.5f ? 0f : extents.z);
        float maxRadius = float.PositiveInfinity;
        if (perpendicularExtents.x > 0.001f) maxRadius = Mathf.Min(maxRadius, perpendicularExtents.x);
        if (perpendicularExtents.y > 0.001f) maxRadius = Mathf.Min(maxRadius, perpendicularExtents.y);
        if (perpendicularExtents.z > 0.001f) maxRadius = Mathf.Min(maxRadius, perpendicularExtents.z);

        if (float.IsPositiveInfinity(maxRadius))
        {
            maxRadius = groundCheckRadius;
        }
        else
        {
            maxRadius *= 0.9f;
        }

        return Mathf.Clamp(groundCheckRadius, 0.01f, maxRadius);
    }

    private Vector3[] GetGroundCheckOffsets(float radius)
    {
        Vector3 up = -gravityDirection;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        forward = forward.normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        return new[]
        {
            forward * radius,
            -forward * radius,
            right * radius,
            -right * radius,
        };
    }

    private void ApplyGroundMovement()
    {
        Vector2 input = ReadMoveInput();
        Vector3 desiredVelocity = Vector3.zero;

        if (input.sqrMagnitude > 0.001f)
        {
            Vector3 forward = ProjectOnMovePlane(GetCameraForward());
            Vector3 right = ProjectOnMovePlane(GetCameraRight());
            Vector3 rawMoveDirection = forward * input.y + right * input.x;
            Vector3 snappedMoveDirection = SnapToCardinalDirection(rawMoveDirection);
            desiredVelocity = snappedMoveDirection * moveSpeed;
            lastMoveDirection = snappedMoveDirection;
            hasMoveInput = true;
        }
        else
        {
            hasMoveInput = false;
        }

        Vector3 currentGravityVelocity = Vector3.Project(body.linearVelocity, gravityDirection);
        body.linearVelocity = currentGravityVelocity + desiredVelocity;
    }

    private Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private Vector3 GetCameraForward()
    {
        return movementCamera != null ? movementCamera.transform.forward : transform.forward;
    }

    private Vector3 GetCameraRight()
    {
        return movementCamera != null ? movementCamera.transform.right : transform.right;
    }

    private Vector3 ProjectOnMovePlane(Vector3 direction)
    {
        Vector3 projected = Vector3.ProjectOnPlane(direction, gravityDirection);
        return projected.sqrMagnitude > 0.001f ? projected.normalized : Vector3.zero;
    }

    private Vector3 SnapToCardinalDirection(Vector3 direction)
    {
        Vector3 projectedDirection = ProjectOnMovePlane(direction);
        if (projectedDirection.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        Vector3 up = -gravityDirection;
        Vector3 referenceForward = ProjectOnMovePlane(Vector3.forward);
        if (referenceForward.sqrMagnitude <= 0.001f)
        {
            referenceForward = ProjectOnMovePlane(Vector3.right);
        }

        Vector3 referenceRight = Vector3.Cross(up, referenceForward).normalized;
        Vector3[] cardinalDirections =
        {
            referenceForward,
            referenceRight,
            -referenceForward,
            -referenceRight,
        };

        Vector3 bestDirection = cardinalDirections[0];
        float bestDot = Vector3.Dot(projectedDirection, bestDirection);

        for (int i = 1; i < cardinalDirections.Length; i++)
        {
            float dot = Vector3.Dot(projectedDirection, cardinalDirections[i]);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestDirection = cardinalDirections[i];
            }
        }

        return bestDirection;
    }

    private void AlignEyesToGravity()
    {
        Vector3 up = -gravityDirection;
        Vector3 fallbackForward = Vector3.ProjectOnPlane(transform.forward, up);
        if (fallbackForward.sqrMagnitude <= 0.001f)
        {
            fallbackForward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        PositionEyes(fallbackForward.normalized, up);
    }

    private void AlignEyesToMoveDirection()
    {
        if (!hasMoveInput || lastMoveDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 up = -gravityDirection;
        Vector3 forward = Vector3.ProjectOnPlane(lastMoveDirection, up);
        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        PositionEyes(forward.normalized, up);
    }

    private void InitializeEyeLayout()
    {
        if (eyes == null)
        {
            eyes = new Transform[0];
        }

        eyeForwardOffsets = new float[eyes.Length];
        eyeVerticalOffsets = new float[eyes.Length];
        eyeHorizontalOffsets = new float[eyes.Length];

        for (int i = 0; i < eyes.Length; i++)
        {
            if (eyes[i] == null)
            {
                continue;
            }

            Vector3 localPosition = eyes[i].localPosition;
            Vector3 scale = transform.lossyScale;
            float initialForwardOffset = Mathf.Abs(Vector3.Dot(localPosition, GetLocalAxisVector(frontAxis)) * GetAxisScale(frontAxis, scale));
            eyeForwardOffsets[i] = initialForwardOffset > 0.001f ? initialForwardOffset : eyeForwardDistance;
            eyeVerticalOffsets[i] = eyeVerticalOffset;
            eyeHorizontalOffsets[i] = eyes.Length > 1 ? (i - (eyes.Length - 1) * 0.5f) * eyeHorizontalSpacing : 0f;
        }
    }

    private void PositionEyes(Vector3 forward, Vector3 up)
    {
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward.normalized, up);

        for (int i = 0; i < eyes.Length; i++)
        {
            Transform eye = eyes[i];
            if (eye == null)
            {
                continue;
            }

            eye.position = transform.position
                + forward * eyeForwardOffsets[i]
                + up * eyeVerticalOffsets[i]
                + right * eyeHorizontalOffsets[i];
            eye.rotation = rotation;
        }
    }

    private Vector3 GetLocalRightAxis()
    {
        Vector3 localUp = GetLocalAxisVector(upAxis);
        Vector3 localFront = GetLocalAxisVector(frontAxis);
        Vector3 right = Vector3.Cross(localUp, localFront);
        if (right.sqrMagnitude <= 0.001f)
        {
            right = Vector3.Cross(Vector3.up, localFront);
        }

        if (right.sqrMagnitude <= 0.001f)
        {
            right = Vector3.forward;
        }

        return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.forward;
    }

    private Vector3 GetLocalAxisVector(LocalAxis axis)
    {
        return axis switch
        {
            LocalAxis.PositiveX => Vector3.right,
            LocalAxis.NegativeX => Vector3.left,
            LocalAxis.PositiveY => Vector3.up,
            LocalAxis.NegativeY => Vector3.down,
            LocalAxis.PositiveZ => Vector3.forward,
            LocalAxis.NegativeZ => Vector3.back,
            _ => Vector3.right,
        };
    }

    private float GetAxisScale(LocalAxis axis, Vector3 scale)
    {
        Vector3 axisVector = GetLocalAxisVector(axis);
        return GetAxisScale(axisVector, scale);
    }

    private float GetAxisScale(Vector3 axisVector, Vector3 scale)
    {
        axisVector = new Vector3(Mathf.Abs(axisVector.x), Mathf.Abs(axisVector.y), Mathf.Abs(axisVector.z));
        return axisVector.x * scale.x + axisVector.y * scale.y + axisVector.z * scale.z;
    }
}
