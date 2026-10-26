using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>도감 목록 카드의 표시만 담당합니다. 항목 획득·해금 처리는 하지 않습니다.</summary>
public sealed class CollectionEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text statusLabel;
    private Action selected;
    private bool connected;
    private bool previewOnHover;

    private void Awake() => ConnectButton();

    // 미리 배치된 비활성 목록도 Bind 시 연결하며, 재진입으로 콜백이 중복되지 않게 합니다.
    private void ConnectButton()
    {
        if (connected || button == null) return;
        button.onClick.AddListener(HandleSelection);
        connected = true;
    }

    /// <summary>저장된 카드 프리팹의 배치를 유지하면서 표시 내용과 선택 콜백을 연결합니다.</summary>
    public void Bind(string title, string status, Sprite sprite, Action onSelected, bool previewOnHover = false)
    {
        ConnectButton();
        nameLabel.text = title;
        statusLabel.text = status;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.gameObject.SetActive(sprite != null);
        selected = onSelected;
        this.previewOnHover = previewOnHover;
    }

    // 주머니는 클릭 없이 호버로 미리 봅니다. 기존 일반 항목의 클릭 콜백은 그대로 유지합니다.
    private void HandleSelection()
    {
        if (!previewOnHover) selected?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData) => PreviewIfAllowed();

    // 키보드/패드로 직접 목록을 탐색할 때도 같은 상세를 확인할 수 있습니다.
    public void OnSelect(BaseEventData eventData) => PreviewIfAllowed();

    private void PreviewIfAllowed()
    {
        if (previewOnHover && isActiveAndEnabled && button != null && button.IsInteractable())
            selected?.Invoke();
    }

    // 주머니를 클릭한 경우에도 커서가 떠나면 선택 색이 고정되지 않게 합니다.
    // 마지막 상세 내용은 유지하며 다른 UI의 선택 상태는 건드리지 않습니다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (previewOnHover && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }
    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleSelection);
        selected = null;
    }
}
