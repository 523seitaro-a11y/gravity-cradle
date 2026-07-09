using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StageSurfaceRotator : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;
    [SerializeField] private Transform stageRoot;
    [SerializeField] private Transform[] stagePieces;
    [Header("Attached Stage Objects")]
    [SerializeField] private Transform[] attachedStageObjects;
    [SerializeField] private Transform[] dynamicObjects;
    [SerializeField] private Vector3 pivot = Vector3.zero;
    [SerializeField] private float rotationDuration = 0.35f;
    [SerializeField] private float gravityAcceleration = 9.81f;
    [Header("Push Box Magnet Outline")]
    [SerializeField] private Color gravitySuppressedOutlineColor = Color.blue;
    [Min(0.001f)]
    [SerializeField] private float gravitySuppressedOutlineWidth = 0.2f;

    private Coroutine rotationCoroutine;
    private Vector3 gravityDirection = Vector3.down;
    private readonly List<Rigidbody> dynamicBodies = new();
    private readonly Dictionary<Rigidbody, Quaternion> pausedBodyRotations = new();
    private readonly List<GravityPlayerController> players = new();
    private readonly List<PushOnlyBox> pushOnlyBoxes = new();
    private readonly List<Transform> autoAttachedStageObjects = new();

    private void FixedUpdate()
    {
        foreach (Rigidbody body in dynamicBodies)
        {
            if (body == null || body.isKinematic)
            {
                continue;
            }

            PushOnlyBox pushOnlyBox = body.GetComponent<PushOnlyBox>();
            if (pushOnlyBox != null && pushOnlyBox.IsGravitySuppressed)
            {
                continue;
            }

            body.AddForce(gravityDirection * gravityAcceleration, ForceMode.Acceleration);
        }
    }

    private void Awake()
    {
        if (rayCamera == null)
        {
            rayCamera = Camera.main;
        }

        PrepareAttachedStageObjects();
        PrepareDynamicBodies();
    }

    private void Update()
    {
        if (rotationCoroutine != null || Mouse.current == null || rayCamera == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = rayCamera.ScreenPointToRay(mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit) || !IsStageSurface(hit.transform))
        {
            return;
        }

        RotateSurfaceToFloor(hit.normal);
    }

    private bool IsStageSurface(Transform hitTransform)
    {
        foreach (Transform dynamicObject in dynamicObjects)
        {
            if (dynamicObject != null && hitTransform.IsChildOf(dynamicObject))
            {
                return false;
            }
        }

        if (stageRoot != null && hitTransform.IsChildOf(stageRoot))
        {
            return true;
        }

        foreach (Transform piece in stagePieces)
        {
            if (piece != null && hitTransform == piece)
            {
                return true;
            }
        }

        return false;
    }

    private void RotateSurfaceToFloor(Vector3 surfaceNormal)
    {
        Quaternion delta = Quaternion.FromToRotation(surfaceNormal.normalized, Vector3.up);

        if (Quaternion.Angle(Quaternion.identity, delta) <= 0.1f)
        {
            return;
        }

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }

        Vector3 nextGravityDirection = -(delta * surfaceNormal.normalized);
        rotationCoroutine = StartCoroutine(RotateStage(delta, nextGravityDirection));
    }

    private IEnumerator RotateStage(Quaternion delta, Vector3 nextGravityDirection)
    {
        SetDynamicBodiesPaused(true);
        AttachDynamicObjectsToStage();

        Transform[] targets = GetRotationTargets();
        Vector3[] startPositions = new Vector3[targets.Length];
        Quaternion[] startRotations = new Quaternion[targets.Length];
        Vector3[] endPositions = new Vector3[targets.Length];
        Quaternion[] endRotations = new Quaternion[targets.Length];

        for (int i = 0; i < targets.Length; i++)
        {
            Transform target = targets[i];
            startPositions[i] = target.position;
            startRotations[i] = target.rotation;
            endPositions[i] = pivot + delta * (target.position - pivot);
            endRotations[i] = delta * target.rotation;
        }

        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].position = Vector3.Lerp(startPositions[i], endPositions[i], t);
                targets[i].rotation = Quaternion.Slerp(startRotations[i], endRotations[i], t);
            }

            yield return null;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            targets[i].position = endPositions[i];
            targets[i].rotation = endRotations[i];
        }

        gravityDirection = nextGravityDirection.normalized;
        DetachDynamicObjectsFromStage();
        ResumePlayers(gravityDirection);
        SetDynamicBodiesPaused(false);
        rotationCoroutine = null;
    }

    private void PrepareDynamicBodies()
    {
        foreach (Transform dynamicObject in dynamicObjects)
        {
            if (dynamicObject == null)
            {
                continue;
            }

            dynamicObject.SetParent(null, true);

            GravityPlayerController player = dynamicObject.GetComponent<GravityPlayerController>();
            if (player != null)
            {
                player.SetGravity(gravityDirection, gravityAcceleration);
                players.Add(player);
                continue;
            }

            Rigidbody body = dynamicObject.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = dynamicObject.gameObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            PushOnlyBox pushOnlyBox = dynamicObject.GetComponent<PushOnlyBox>();
            if (pushOnlyBox == null)
            {
                pushOnlyBox = dynamicObject.gameObject.AddComponent<PushOnlyBox>();
            }

            pushOnlyBox.SetOutlineSettings(gravitySuppressedOutlineColor, gravitySuppressedOutlineWidth);
            pushOnlyBox.SetGravityDirection(gravityDirection);
            pushOnlyBoxes.Add(pushOnlyBox);
            dynamicBodies.Add(body);
        }
    }

    private void PrepareAttachedStageObjects()
    {
        autoAttachedStageObjects.Clear();
        AddAutoAttachedStageObject("Button");
        AddAutoAttachedStageObject("Goal");
    }

    private void AddAutoAttachedStageObject(string objectName)
    {
        Transform found = FindSceneTransformByName(objectName);
        if (found != null)
        {
            AddUniqueTransform(autoAttachedStageObjects, found);
        }
    }

    private void AttachDynamicObjectsToStage()
    {
        foreach (Transform dynamicObject in dynamicObjects)
        {
            if (dynamicObject == null)
            {
                continue;
            }

            GravityPlayerController player = dynamicObject.GetComponent<GravityPlayerController>();
            if (player != null)
            {
                player.PauseForStageRotation(stageRoot);
                continue;
            }

            if (stageRoot != null)
            {
                Rigidbody body = dynamicObject.GetComponent<Rigidbody>();
                if (body != null)
                {
                    pausedBodyRotations[body] = dynamicObject.rotation;
                }

                dynamicObject.SetParent(stageRoot, true);
                if (body != null)
                {
                    dynamicObject.rotation = pausedBodyRotations[body];
                }
            }
        }
    }

    private void DetachDynamicObjectsFromStage()
    {
        foreach (Transform dynamicObject in dynamicObjects)
        {
            if (dynamicObject != null)
            {
                Rigidbody body = dynamicObject.GetComponent<Rigidbody>();
                dynamicObject.SetParent(null, true);
                if (body != null && pausedBodyRotations.TryGetValue(body, out Quaternion rotation))
                {
                    dynamicObject.rotation = rotation;
                }
            }
        }
    }

    private void SetDynamicBodiesPaused(bool isPaused)
    {
        foreach (Rigidbody body in dynamicBodies)
        {
            if (body == null)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.isKinematic = isPaused;
            body.useGravity = false;

            if (!isPaused)
            {
                body.WakeUp();
            }
        }
    }

    private void ResumePlayers(Vector3 nextGravityDirection)
    {
        foreach (GravityPlayerController player in players)
        {
            if (player != null)
            {
                player.ResumeAfterStageRotation(nextGravityDirection, gravityAcceleration);
            }
        }

        foreach (PushOnlyBox box in pushOnlyBoxes)
        {
            if (box != null)
            {
                box.SetGravityDirection(nextGravityDirection);
            }
        }
    }

    private Transform[] GetRotationTargets()
    {
        List<Transform> targets = new List<Transform>();

        if (stageRoot != null)
        {
            AddUniqueTransform(targets, stageRoot);
        }
        else
        {
            foreach (Transform piece in stagePieces)
            {
                AddUniqueTransform(targets, piece);
            }
        }

        if (attachedStageObjects != null)
        {
            foreach (Transform attachedObject in attachedStageObjects)
            {
                AddUniqueTransform(targets, attachedObject);
            }
        }

        foreach (Transform attachedObject in autoAttachedStageObjects)
        {
            AddUniqueTransform(targets, attachedObject);
        }

        return targets.ToArray();
    }

    private void AddUniqueTransform(List<Transform> targets, Transform candidate)
    {
        if (candidate == null)
        {
            return;
        }

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Transform existing = targets[i];
            if (existing == null)
            {
                targets.RemoveAt(i);
                continue;
            }

            if (candidate == existing || candidate.IsChildOf(existing))
            {
                return;
            }

            if (existing.IsChildOf(candidate))
            {
                targets.RemoveAt(i);
            }
        }

        targets.Add(candidate);
    }

    private Transform FindSceneTransformByName(string objectName)
    {
        foreach (GameObject sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (sceneObject.scene.IsValid() && sceneObject.name == objectName)
            {
                return sceneObject.transform;
            }
        }

        return null;
    }
}
