using UnityEngine;

/// <summary>
/// Player를 비스듬히 위에서 내려다보며 따라가는 카메라.
/// 지도 기준 방향(북쪽 = 화면 위)을 유지한다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("타겟으로부터의 거리 (m)")]
    public float distance = 250f;
    [Tooltip("내려다보는 각도 (90 = 바로 위)")]
    [Range(10f, 90f)]
    public float pitch = 55f;
    [Tooltip("따라가는 속도. 0이면 즉시 따라감")]
    public float followSpeed = 6f;

    void LateUpdate()
    {
        if (target == null)
            return;

        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 desired = target.position - rot * Vector3.forward * distance;

        transform.position = followSpeed > 0f
            ? Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime))
            : desired;
        transform.rotation = rot;
    }
}
