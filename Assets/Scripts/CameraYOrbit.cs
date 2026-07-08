using UnityEngine;
using UnityEngine.InputSystem;

public class CameraYOrbit : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private float fixedY = 22f;
    [SerializeField, Range(1f, 89f)] private float downwardAngle = 40f;
    [SerializeField] private float degreesPerScrollUnit = 10f;

    private void Start()
    {
        ApplyOrbitPose();
    }

    private void Update()
    {
        Vector3 focus = GetFocusPoint();

        if (Mouse.current == null)
        {
            ApplyOrbitPose(focus);
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            ApplyOrbitPose(focus);
            return;
        }

        Quaternion rotation = Quaternion.AngleAxis(scrollY * degreesPerScrollUnit, Vector3.up);
        Vector3 offset = transform.position - focus;

        transform.position = focus + rotation * offset;
        ApplyOrbitPose(focus);
    }

    private void ApplyOrbitPose()
    {
        ApplyOrbitPose(GetFocusPoint());
    }

    private void ApplyOrbitPose(Vector3 focus)
    {
        Vector3 flatOffset = transform.position - focus;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude <= Mathf.Epsilon)
        {
            flatOffset = Vector3.back;
        }

        float height = Mathf.Abs(fixedY - focus.y);
        float distance = height / Mathf.Tan(downwardAngle * Mathf.Deg2Rad);
        Vector3 position = focus + flatOffset.normalized * distance;

        position.y = fixedY;
        transform.position = position;
        LookAt(focus);
    }

    private Vector3 GetFocusPoint()
    {
        return target != null ? target.position : center;
    }

    private void LookAt(Vector3 focus)
    {
        transform.LookAt(focus, Vector3.up);
    }
}
