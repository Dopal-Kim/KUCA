using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

/// <summary>
/// 경희몬 잡기 화면에서만 잠깐 켜는 AR Foundation 장치 (ARKit / ARCore).
/// XR 은 앱 시작 때 켜지 않고(지도 화면은 AR 이 필요 없음) 여기서 켰다가 닫을 때 끈다.
/// 바닥(수평면)을 찾으면 그 위에 경희몬을 세운다.
/// </summary>
public class ArCatchRig
{
    public Camera Camera { get; private set; }
    public bool Ready { get; private set; }
    public string FailReason { get; private set; }

    GameObject sessionGo;
    GameObject originGo;
    GameObject planeTemplateHolder;
    ARPlaneManager planes;
    ARRaycastManager raycaster;
    Material planeMaterial;
    readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    bool startedXr;

    /// <summary>XR 을 켜고 AR 세션과 카메라를 만든다. 끝나면 Ready 또는 FailReason 이 정해진다.</summary>
    public IEnumerator Start(Vector3 origin, int layer)
    {
        Ready = false;
        FailReason = null;

        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null)
        {
            FailReason = "XR 설정이 없어요";
            yield break;
        }
        if (manager.activeLoader == null)
        {
            yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                FailReason = "AR 을 시작하지 못했어요";
                yield break;
            }
            manager.StartSubsystems();
            startedXr = true;
        }

        yield return ARSession.CheckAvailability();
        if (ARSession.state == ARSessionState.Unsupported)
        {
            FailReason = "이 기기는 AR 을 지원하지 않아요";
            yield break;
        }

        // XR Origin > Camera Offset > AR Camera (비활성 상태로 다 붙인 뒤 켠다)
        originGo = new GameObject("XR Origin (Catch)");
        originGo.SetActive(false);
        originGo.transform.position = origin;
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originGo.transform, false);
        var camGo = new GameObject("AR Camera", typeof(Camera));
        camGo.transform.SetParent(offset.transform, false);
        Camera = camGo.GetComponent<Camera>();
        Camera.clearFlags = CameraClearFlags.SolidColor;
        Camera.backgroundColor = Color.black;
        Camera.nearClipPlane = 0.05f;
        Camera.farClipPlane = 30f;
        Camera.cullingMask = 1 << layer;
        Camera.depth = 10f;
        camGo.AddComponent<ARCameraManager>();
        camGo.AddComponent<ARCameraBackground>();
        var pose = camGo.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        var pos = new InputAction("AR Position", binding: "<XRHMD>/centerEyePosition");
        pos.AddBinding("<HandheldARInputDevice>/devicePosition");
        var rot = new InputAction("AR Rotation", binding: "<XRHMD>/centerEyeRotation");
        rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
        pose.positionInput = new InputActionProperty(pos);
        pose.rotationInput = new InputActionProperty(rot);

        var xrOrigin = originGo.AddComponent<XROrigin>();
        xrOrigin.Camera = Camera;
        xrOrigin.CameraFloorOffsetObject = offset;

        planes = originGo.AddComponent<ARPlaneManager>();
        planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        planes.planePrefab = MakePlaneTemplate(layer);
        raycaster = originGo.AddComponent<ARRaycastManager>();

        originGo.SetActive(true);
        // 세션은 XR Origin 이 장면에 생긴 뒤에 만들어야 Origin 을 찾는다.
        sessionGo = new GameObject("AR Session", typeof(ARSession));
        Ready = true;
    }

    /// <summary>찾은 바닥을 반투명하게 보여 주는 판 (인스턴스 원본)</summary>
    GameObject MakePlaneTemplate(int layer)
    {
        // 원본은 꺼진 부모 아래에 두어 장면에 나타나지 않게 한다. 복제본은 켜진 채로 만들어진다.
        planeTemplateHolder = new GameObject("PlaneTemplateHolder");
        planeTemplateHolder.SetActive(false);
        var go = new GameObject("AR Plane", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(planeTemplateHolder.transform, false);
        go.layer = layer;
        go.AddComponent<ARPlane>();
        go.AddComponent<ARPlaneMeshVisualizer>();
        Shader s = Shader.Find("Sprites/Default");
        planeMaterial = new Material(s != null ? s : Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 1f, 1f, 0.18f) };
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = planeMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public bool HasPlanes => planes != null && planes.trackables.count > 0;

    /// <summary>화면 좌표에서 찾은 바닥으로 광선을 쏴 맞은 자리를 돌려준다.</summary>
    public bool Raycast(Vector2 screenPoint, out Pose hitPose)
    {
        hitPose = default;
        if (raycaster == null || !raycaster.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
            return false;
        hitPose = hits[0].pose;
        return true;
    }

    /// <summary>경희몬을 세운 뒤에는 바닥 판을 숨겨 사진에 안 나오게 한다.</summary>
    public void SetPlanesVisible(bool visible)
    {
        if (planes == null)
            return;
        foreach (ARPlane p in planes.trackables)
        {
            var r = p.GetComponent<MeshRenderer>();
            if (r != null && r.enabled != visible)
                r.enabled = visible;
        }
    }

    public void Stop()
    {
        if (originGo != null) Object.Destroy(originGo);
        if (sessionGo != null) Object.Destroy(sessionGo);
        if (planeTemplateHolder != null) Object.Destroy(planeTemplateHolder);
        if (planeMaterial != null) Object.Destroy(planeMaterial);
        originGo = sessionGo = planeTemplateHolder = null;
        planes = null;
        raycaster = null;
        Camera = null;
        Ready = false;

        if (startedXr)
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager != null && manager.activeLoader != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            startedXr = false;
        }
    }
}
