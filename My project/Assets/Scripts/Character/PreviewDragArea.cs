using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>UI 영역을 좌우로 드래그한 양(픽셀)을 알려 준다. 캐릭터 미리보기 회전에 쓴다.</summary>
public class PreviewDragArea : MonoBehaviour, IDragHandler
{
    public System.Action<float> onDragX;

    public void OnDrag(PointerEventData eventData) => onDragX?.Invoke(eventData.delta.x);
}
