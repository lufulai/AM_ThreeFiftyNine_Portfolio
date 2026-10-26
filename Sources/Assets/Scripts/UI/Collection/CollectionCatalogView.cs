using System;
using UnityEngine;

/// <summary>작성된 도감 카드에 프로필별 공개/잠금 내용을 바인딩합니다. 배치·획득은 담당하지 않습니다.</summary>
public sealed class CollectionCatalogView : MonoBehaviour
{
    [SerializeField] private CollectionCatalogEntry[] entries;
    [Header("2차 잠금 이미지 · 비어 있으면 ? 텍스트만 표시")]
    [SerializeField] private Sprite hiddenSprite;
    private SaveManager observedSave;
    private CollectionPanelView.Category boundCategory;
    private Action<string, string, string, Sprite> showDetail;
    private bool isBound;
    private CollectionCatalogEntry selectedEntry;
    private UserData boundProfile;
    public Sprite HiddenSprite => hiddenSprite;
    public event Action ProfileChanged;

    public bool BindCategory(CollectionPanelView.Category category, Action<string, string, string, Sprite> display)
    {
        ObserveProfile();
        return BindCategory(category, display, SaveManager.instance != null ? SaveManager.instance.userData : null);
    }

    // 프로필을 인자로 받는 경로는 저장 파일을 건드리지 않는 격리 검사에도 사용합니다.
    public bool BindCategory(CollectionPanelView.Category category,
        Action<string, string, string, Sprite> display, UserData profile)
    {
        boundCategory = category;
        showDetail = display;
        boundProfile = profile;
        isBound = true;
        bool found = false;
        if (entries == null) return false;
        foreach (CollectionCatalogEntry entry in entries)
        {
            if (entry?.view == null) continue;
            bool show = entry.category == category;
            entry.view.gameObject.SetActive(show);
            if (!show) continue;
            bool revealed = CollectionItemInfo.IsVisible(entry, profile);
            entry.view.Bind(revealed ? CollectionItemInfo.Name(entry) : "?",
                CollectionItemInfo.Status(entry, profile), revealed ? null : hiddenSprite,
                () => Show(entry, profile), previewOnHover: true);
            if (!found) Show(entry, profile);
            found = true;
        }
        return found;
    }

    private void Show(CollectionCatalogEntry entry, UserData profile)
    {
        selectedEntry = entry;
        bool revealed = CollectionItemInfo.IsVisible(entry, profile);
        showDetail?.Invoke(revealed ? CollectionItemInfo.Name(entry) : "?",
            CollectionItemInfo.Description(entry, profile), CollectionItemInfo.Status(entry, profile),
            revealed ? null : hiddenSprite);
    }

    internal bool RefreshLocalizedEntries()
    {
        if (!isBound) return false;
        CollectionCatalogEntry selected = selectedEntry;
        bool found = BindCategory(boundCategory, showDetail, boundProfile);
        if (selected != null && selected.category == boundCategory) Show(selected, boundProfile);
        return found;
    }

    public void HideEntries()
    {
        isBound = false;
        showDetail = null;
        selectedEntry = null;
        boundProfile = null;
        if (entries == null) return;
        foreach (CollectionCatalogEntry entry in entries)
            if (entry?.view != null) entry.view.gameObject.SetActive(false);
    }

    // 전환/초기화 이후 같은 인스턴스를 재사용해도 이전 프로필의 공개 상태가 남지 않습니다.
    private void ObserveProfile()
    {
        if (observedSave == SaveManager.instance) return;
        StopObserving();
        observedSave = SaveManager.instance;
        if (observedSave == null) return;
        observedSave.ActiveProfileChanged += OnProfileChanged;
        observedSave.ProfileDataChanged += Refresh;
    }

    private void OnProfileChanged(int _) => Refresh();
    private void Refresh()
    {
        if (!isBound || observedSave == null) return;
        BindCategory(boundCategory, showDetail, observedSave.userData);
        ProfileChanged?.Invoke();
    }

    private void OnDisable() => StopObserving();
    private void OnDestroy() => StopObserving();
    private void StopObserving()
    {
        if (observedSave != null)
        {
            observedSave.ActiveProfileChanged -= OnProfileChanged;
            observedSave.ProfileDataChanged -= Refresh;
        }
        observedSave = null;
    }
}
