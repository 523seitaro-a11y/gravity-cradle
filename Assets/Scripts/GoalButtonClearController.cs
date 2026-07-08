using System.Collections.Generic;
using UnityEngine;

public class GoalButtonClearController : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] private Transform buttonRoot;
    [SerializeField] private Transform buttonMain;
    [SerializeField] private Transform stageCenter;
    [SerializeField] private float buttonPressDistance = 0.15f;
    [SerializeField] private float buttonMoveSpeed = 10f;
    [SerializeField] private float buttonContactPadding = 0.05f;
    [SerializeField] private bool disableButtonColliders = true;

    [Header("Goal")]
    [SerializeField] private GameObject goalLock;
    [SerializeField] private Transform goalHole;
    [SerializeField] private float goalContactPadding = 0.05f;

    [Header("Clear Display")]
    [SerializeField] private string clearMessage = "クリア";
    [SerializeField] private int clearFontSize = 64;
    [SerializeField] private Color clearTextColor = Color.white;

    private Vector3 buttonMainStartLocalPosition;
    private Vector3 buttonMainStartWorldPosition;
    private GUIStyle clearStyle;
    private bool isButtonPressed;
    private bool isCleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneController()
    {
        if (FindObjectsByType<GoalButtonClearController>(FindObjectsSortMode.None).Length > 0)
        {
            return;
        }

        GameObject controllerObject = new GameObject("Goal Button Clear Controller");
        controllerObject.AddComponent<GoalButtonClearController>();
    }

    private void Awake()
    {
        AutoAssignReferences();
        DisableButtonCollidersIfNeeded();

        if (buttonMain != null)
        {
            buttonMainStartLocalPosition = buttonMain.localPosition;
            buttonMainStartWorldPosition = buttonMain.position;
        }
    }

    private void OnValidate()
    {
        buttonPressDistance = Mathf.Max(0f, buttonPressDistance);
        buttonMoveSpeed = Mathf.Max(0.01f, buttonMoveSpeed);
        buttonContactPadding = Mathf.Max(0f, buttonContactPadding);
        goalContactPadding = Mathf.Max(0f, goalContactPadding);
        clearFontSize = Mathf.Max(1, clearFontSize);
    }

    private void Update()
    {
        isButtonPressed = IsAnyInteractorTouching(buttonMain, buttonContactPadding);

        MoveButtonMain(isButtonPressed);
        UpdateGoalLock();

        if (!isCleared && isButtonPressed && IsPlayerTouchingGoalHole())
        {
            isCleared = true;
        }
    }

    private void OnGUI()
    {
        if (!isCleared)
        {
            return;
        }

        if (clearStyle == null)
        {
            clearStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = clearFontSize,
                fontStyle = FontStyle.Bold,
            };
        }

        clearStyle.normal.textColor = clearTextColor;
        clearStyle.fontSize = clearFontSize;

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), clearMessage, clearStyle);
    }

    private void AutoAssignReferences()
    {
        if (buttonRoot == null) buttonRoot = FindTransformByName("Button");
        if (buttonMain == null) buttonMain = FindTransformByName("ButtonMain");
        if (goalLock == null) goalLock = FindGameObjectByName("GoalLock");
        if (goalHole == null) goalHole = FindTransformByName("GoalHole");
        if (stageCenter == null) stageCenter = FindTransformByName("Stage");
    }

    private void DisableButtonCollidersIfNeeded()
    {
        if (!disableButtonColliders)
        {
            return;
        }

        DisableColliders(buttonRoot);

        if (buttonMain != null && (buttonRoot == null || !buttonMain.IsChildOf(buttonRoot)))
        {
            DisableColliders(buttonMain);
        }
    }

    private void DisableColliders(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    private void MoveButtonMain(bool pressed)
    {
        if (buttonMain == null)
        {
            return;
        }

        Vector3 basePosition = buttonMain.parent != null
            ? buttonMain.parent.TransformPoint(buttonMainStartLocalPosition)
            : buttonMainStartWorldPosition;
        Vector3 pressDirection = GetButtonPressDirection(basePosition);
        Vector3 targetPosition = pressed
            ? basePosition + pressDirection * buttonPressDistance
            : basePosition;

        buttonMain.position = Vector3.Lerp(
            buttonMain.position,
            targetPosition,
            1f - Mathf.Exp(-buttonMoveSpeed * Time.deltaTime));
    }

    private Vector3 GetButtonPressDirection(Vector3 buttonBasePosition)
    {
        Vector3 center = stageCenter != null ? stageCenter.position : Vector3.zero;
        Vector3 direction = buttonBasePosition - center;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = buttonMain != null ? buttonMain.up : Vector3.up;
        }

        return direction.normalized;
    }

    private void UpdateGoalLock()
    {
        if (goalLock != null && goalLock.activeSelf == isButtonPressed)
        {
            goalLock.SetActive(!isButtonPressed);
        }
    }

    private bool IsAnyInteractorTouching(Transform target, float padding)
    {
        if (!TryGetTargetBounds(target, padding, out Bounds targetBounds))
        {
            return false;
        }

        foreach (Bounds interactorBounds in GetInteractorBounds())
        {
            if (targetBounds.Intersects(interactorBounds))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerTouchingGoalHole()
    {
        if (!TryGetTargetBounds(goalHole, goalContactPadding, out Bounds goalBounds))
        {
            return false;
        }

        foreach (GravityPlayerController player in FindObjectsByType<GravityPlayerController>(FindObjectsSortMode.None))
        {
            if (player != null && TryGetObjectBounds(player.transform, 0f, out Bounds playerBounds) && goalBounds.Intersects(playerBounds))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<Bounds> GetInteractorBounds()
    {
        foreach (GravityPlayerController player in FindObjectsByType<GravityPlayerController>(FindObjectsSortMode.None))
        {
            if (player != null && TryGetObjectBounds(player.transform, 0f, out Bounds bounds))
            {
                yield return bounds;
            }
        }

        foreach (PushOnlyBox box in FindObjectsByType<PushOnlyBox>(FindObjectsSortMode.None))
        {
            if (box != null && TryGetObjectBounds(box.transform, 0f, out Bounds bounds))
            {
                yield return bounds;
            }
        }
    }

    private bool TryGetTargetBounds(Transform target, float padding, out Bounds bounds)
    {
        if (target == null)
        {
            bounds = default(Bounds);
            return false;
        }

        return TryGetObjectBounds(target, padding, out bounds);
    }

    private bool TryGetObjectBounds(Transform target, float padding, out Bounds bounds)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        bool hasBounds = false;
        bounds = new Bounds(target.position, Vector3.one * Mathf.Max(0.05f, padding * 2f));

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        bounds.Expand(padding * 2f);
        return true;
    }

    private Transform FindTransformByName(string objectName)
    {
        GameObject found = FindGameObjectByName(objectName);
        return found != null ? found.transform : null;
    }

    private GameObject FindGameObjectByName(string objectName)
    {
        foreach (GameObject sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (sceneObject.scene.IsValid() && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
