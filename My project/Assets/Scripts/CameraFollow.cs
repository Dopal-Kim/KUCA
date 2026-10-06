using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Player를 비스듬히 위에서 내려다보며 따라가는 카메라.
/// 폰: 한 손가락 드래그 = Player 주위 회전, 두 손가락 핀치 = 줌, 두 번 탭 = 북쪽 위로 되돌리기
/// 에디터: 오른쪽 드래그 = 회전, 휠 = 줌, R = 북쪽 위로 되돌리기
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("타겟으로부터의 거리 (m)")]
    public float distance = 250f;
    public float minDistance = 60f;
    public float maxDistance = 700f;
    [Tooltip("내려다보는 각도 (90 = 바로 위)")]
    [Range(10f, 90f)]
    public float pitch = 55f;
    [Tooltip("카메라가 바라보는 방위 (0 = 북쪽이 화면 위)")]
    public float yaw = 0f;
    [Tooltip("따라가는 속도. 0이면 즉시 따라감")]
    public float followSpeed = 6f;

    [Header("Controls")]
    [Tooltip("화면 너비만큼 드래그했을 때 회전 각도")]
    public float rotateDegreesPerScreen = 270f;
    public float mouseZoomStep = 0.1f;
    [Tooltip("두 번 탭으로 인정하는 간격 (초)")]
    public float doubleTapTime = 0.3f;

    float lastTapTime = -1f;

    float resetVelocity;
    bool resettingYaw;

#if ENABLE_INPUT_SYSTEM
    void OnEnable() => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();
#endif

    void LateUpdate()
    {
        if (target == null)
            return;

        HandleInput();

        if (resettingYaw)
        {
            yaw = Mathf.SmoothDampAngle(yaw, 0f, ref resetVelocity, 0.25f);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 0.1f)
            {
                yaw = 0f;
                resettingYaw = false;
            }
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = target.position - rot * Vector3.forward * distance;

        transform.position = followSpeed > 0f
            ? Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime))
            : desired;
        transform.rotation = rot;
    }

    /// <summary>카메라를 북쪽이 위로 오게 부드럽게 되돌린다.</summary>
    public void ResetNorthUp()
    {
        resettingYaw = true;
        resetVelocity = 0f;
    }

    void Rotate(float screenDeltaX)
    {
        resettingYaw = false;
        yaw += screenDeltaX / Mathf.Max(1, Screen.width) * rotateDegreesPerScreen;
    }

    void Zoom(float factor)
    {
        distance = Mathf.Clamp(distance * factor, minDistance, maxDistance);
    }

    void HandleInput()
    {
        // 모달 화면(꾸미기, 메모판)이 열려 있으면 지도 카메라를 움직이지 않는다 (목록 스크롤이 줌이 되지 않게).
        if (UIInputBlocker.IsModalOpen)
            return;
#if ENABLE_INPUT_SYSTEM
        var touches = Touch.activeTouches;
        if (touches.Count == 1)
        {
            Touch t = touches[0];
            // UI(패널, 버튼) 위에서 시작한 터치나 모달 화면이 열려 있을 때는 카메라 조작에 쓰지 않는다.
            if (!UIInputBlocker.AllowsGameInput(t.startScreenPosition))
                return;
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                Rotate(t.delta.x);
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                if (Time.unscaledTime - lastTapTime < doubleTapTime)
                {
                    ResetNorthUp();
                    lastTapTime = -1f;
                }
                else
                    lastTapTime = Time.unscaledTime;
            }
        }
        else if (touches.Count >= 2)
        {
            Touch a = touches[0], b = touches[1];
            float now = Vector2.Distance(a.screenPosition, b.screenPosition);
            float before = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
            if (now > 1f && before > 1f)
                Zoom(before / now);
            lastTapTime = -1f;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && touches.Count == 0)
        {
            if (mouse.rightButton.isPressed)
                Rotate(mouse.delta.ReadValue().x);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                Zoom(scroll > 0f ? 1f - mouseZoomStep : 1f + mouseZoomStep);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            ResetNorthUp();
#endif
    }
}
