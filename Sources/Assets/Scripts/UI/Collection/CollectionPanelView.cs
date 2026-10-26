using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>저장된 도감의 화면 전환·내용 바인딩과 타이틀 입력 잠금을 담당합니다.</summary>
public sealed class CollectionPanelView : MonoBehaviour
{
    // 기존 직렬화 번호를 보존하기 위해 새 분류는 마지막에 추가합니다.
    public enum Category { Artifact, Joker, Consumable, Planet, Enhancement, Edition, Voucher, Bag }

    [Serializable]
    private sealed class CategoryPage
    {
        public Category category = default;
        public Button button = null;
        public string title = null;
        [TextArea(3, 8)] public string introduction = null;
    }

    [Serializable]
    private sealed class BagEntry
    {
        public CardpackSO data = null;
        public CollectionEntryView view = null;
    }

    [Header("프리팹에 배치한 화면")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject categoryPage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private CategoryPage[] categories;

    [Header("공통 목록과 상세 창")]
    [SerializeField] private TMP_Text categoryTitle;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailStatus;
    [SerializeField] private Image detailIcon;
    [SerializeField] private ScrollRect itemScroll;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private CollectionEntryView entryPrefab;
    [SerializeField] private CollectionCatalogView catalog;
    [SerializeField] private ScrollRect detailScroll;

    [Header("주머니 목록 · 상점 원본을 읽기만 합니다")]
    [SerializeField] private BagEntry[] bagEntries;

    private CanvasGroup titleInput;
    private float previousTitleAlpha;
    private bool previousInteractable;
    private bool previousBlocksRaycasts;
    private GameObject previousSelection;
    private UnityAction[] categoryListeners;
    private bool categoryVisible;
    private CardpackSO selectedBag;
    public bool IsOpen { get; private set; }
    public Category CurrentCategory { get; private set; }
    public CollectionEntryView EntryPrefab => entryPrefab;
    public RectTransform ItemContent => itemScroll.content;

    // 버튼 콜백은 한 번만 연결합니다. 프리팹의 위치·이미지·글꼴은 변경하지 않습니다.
    private void Awake()
    {
        ConnectButtons();
    }

    private void OnEnable()
    {
        GameText.Changed -= RefreshLocale;
        GameText.Changed += RefreshLocale;
        RefreshLocale();
    }

    private void RefreshLocale()
    {
        if (!IsOpen || !categoryVisible) return;
        CategoryPage page = Array.Find(categories, entry => entry.category == CurrentCategory);
        if (page == null) return;
        Vector2 itemPosition = itemScroll.normalizedPosition;
        Vector2 itemVelocity = itemScroll.velocity;
        Vector2 detailPosition = detailScroll != null ? detailScroll.normalizedPosition : Vector2.one;
        Vector2 detailVelocity = detailScroll != null ? detailScroll.velocity : Vector2.zero;
        categoryTitle.text = CategoryText(page, "title", page.title);
        bool hasEntries;
        if (CurrentCategory == Category.Bag)
        {
            CardpackSO previousBag = selectedBag;
            UserData profile = SaveManager.instance != null ? SaveManager.instance.userData : null;
            hasEntries = BindBagEntries(true, profile);
            if (previousBag != null) ShowBag(previousBag, profile);
        }
        else hasEntries = catalog != null && catalog.RefreshLocalizedEntries();
        if (!hasEntries)
        {
            detailTitle.text = categoryTitle.text;
            detailDescription.text = CategoryText(page, "introduction", page.introduction);
            detailStatus.text = GameText.Get("game.CollectionPanelView.ShowCategory.8c97c0d6bb", "목록에 커서를 올려 설명을 확인하세요");
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemScroll.content);
        itemScroll.normalizedPosition = itemPosition;
        itemScroll.velocity = itemVelocity;
        if (detailScroll != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailScroll.content);
            detailScroll.normalizedPosition = detailPosition;
            detailScroll.velocity = detailVelocity;
        }
    }

    private static string CategoryText(CategoryPage page, string property, string source)
        => GameText.Get("collection.category." + page.category + "." + property,
            (source ?? string.Empty).Replace('\u00b7', '/'));

    private void ConnectButtons()
    {
        if (categoryListeners != null) return;
        if (catalog != null) catalog.ProfileChanged += RefreshBagProfile;
        closeButton.onClick.AddListener(Close);
        backButton.onClick.AddListener(ShowMainPage);
        categoryListeners = new UnityAction[categories.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            CategoryPage page = categories[i];
            categoryListeners[i] = () => ShowCategory(page);
            page.button.onClick.AddListener(categoryListeners[i]);
        }
    }

    /// <summary>타이틀 Canvas와 별개의 도감 Canvas를 열어 뒤쪽 입력을 차단합니다.</summary>
    public void Open(CanvasGroup owner)
    {
        if (IsOpen) return;
        ConnectButtons();
        titleInput = owner;
        if (titleInput != null)
        {
            previousTitleAlpha = titleInput.alpha;
            previousInteractable = titleInput.interactable;
            previousBlocksRaycasts = titleInput.blocksRaycasts;
            // 도감은 별도 Canvas이므로 타이틀 UI만 숨깁니다. 3D 타이틀 배경은 그대로 유지됩니다.
            titleInput.alpha = 0f;
            titleInput.interactable = false;
            titleInput.blocksRaycasts = false;
        }
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        IsOpen = true;
        gameObject.SetActive(true);
        ShowMainPage();
    }

    // ESC는 상세/목록에서 분류 화면으로, 분류 화면에서 타이틀로 한 단계씩 돌아갑니다.
    private void Update()
    {
        if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (categoryVisible) ShowMainPage();
        else Close();
    }

    private void ShowMainPage()
    {
        if (catalog != null) catalog.HideEntries();
        categoryVisible = false;
        mainPage.SetActive(true);
        categoryPage.SetActive(false);
        ClearUiSelection();
    }

    // 저장된 목록에 내용만 바인딩합니다. 발견·해금 판정은 도감 바인더가 담당합니다.
    private void ShowCategory(CategoryPage page)
    {
        CurrentCategory = page.category;
        selectedBag = null;
        categoryVisible = true;
        categoryTitle.text = CategoryText(page, "title", page.title);
        detailTitle.text = categoryTitle.text;
        detailDescription.text = CategoryText(page, "introduction", page.introduction);
        detailStatus.text = GameText.Get("game.CollectionPanelView.ShowCategory.8c97c0d6bb", "목록에 커서를 올려 설명을 확인하세요");
        detailIcon.gameObject.SetActive(false);
        mainPage.SetActive(false);
        categoryPage.SetActive(true);
        bool hasBags = BindBagEntries(page.category == Category.Bag,
            SaveManager.instance != null ? SaveManager.instance.userData : null);
        bool hasItems = catalog != null && catalog.BindCategory(page.category, ShowEntry);
        emptyState.SetActive(!hasBags && !hasItems);
        itemScroll.StopMovement();
        // 실제 높이는 프리팹의 Grid/ContentSizeFitter가 결정합니다. 코드에서 배치를 덮어쓰지 않습니다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemScroll.content);
        itemScroll.verticalNormalizedPosition = 1f;
        ClearUiSelection();
    }

    // 목록 카드는 프리팹에 미리 배치합니다. 런타임에는 생성하지 않고 내용·표시 여부만 바꿉니다.
    public bool BindBagEntries(bool visible, UserData profile)
    {
        bool hasEntries = false;
        if (bagEntries == null) return false;
        foreach (BagEntry entry in bagEntries)
        {
            if (entry.view == null) continue;
            bool show = visible && CollectionBagInfo.IsShopBag(entry.data);
            entry.view.gameObject.SetActive(show);
            if (!show) continue;
            CardpackSO data = entry.data;
            bool discovered = CollectionDiscovery.Contains(profile, CollectionDiscovery.BagKey(data));
            entry.view.Bind(discovered ? CollectionBagInfo.ListTitle(data) : "?",
                discovered ? CollectionBagInfo.Summary(data) : GameText.Get("game.CollectionPanelView.BindBagEntries.9a705a638b", "미발견"),
                !discovered && catalog != null ? catalog.HiddenSprite : null,
                () => ShowBag(data, profile), previewOnHover: true);
            // 빈 상세 창 대신 첫 주머니를 즉시 보여 줍니다.
            if (!hasEntries) ShowBag(data, profile);
            hasEntries = true;
        }
        return hasEntries;
    }

    private void ShowBag(CardpackSO data, UserData profile)
    {
        selectedBag = data;
        bool discovered = CollectionDiscovery.Contains(profile, CollectionDiscovery.BagKey(data));
        ShowEntry(discovered ? CollectionBagInfo.Title(data) : "?",
            discovered ? CollectionBagInfo.Description(data) : GameText.Get("game.CollectionPanelView.ShowBag.6075ebf0a0", "능력: ?\n\n해금 조건\n해금 조건 없음"),
            discovered ? GameText.Get("game.CollectionPanelView.ShowBag.48950ed213", "상점 주머니 안내 / 구매/개봉은 상점에서") : GameText.Get("game.CollectionPanelView.ShowBag.9a705a638b", "미발견"),
            !discovered && catalog != null ? catalog.HiddenSprite : null);
    }

    // 프로필 전환/초기화는 주머니의 발견 상태에도 동일하게 반영합니다.
    private void RefreshBagProfile()
    {
        if (categoryVisible && CurrentCategory == Category.Bag)
            BindBagEntries(true, SaveManager.instance != null ? SaveManager.instance.userData : null);
    }

    /// <summary>추후 목록의 항목을 선택했을 때 기존 상세 프리팹에 내용만 채웁니다.</summary>
    public void ShowEntry(string title, string description, string status, Sprite icon)
    {
        detailTitle.text = title;
        detailDescription.text = description;
        detailStatus.text = status;
        detailIcon.sprite = icon;
        detailIcon.gameObject.SetActive(icon != null);
        // 긴 효과/이야기도 잘리지 않도록 작성된 상세 스크롤을 맨 위로 돌립니다.
        if (detailScroll != null)
        {
            detailScroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailScroll.content);
            detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        RestoreTitleInput();
        gameObject.SetActive(false);
    }

    // 비활성화·씬 종료 등 닫힘 경로가 달라도 열기 전의 입력 상태를 복원합니다.
    private void OnDisable()
    {
        GameText.Changed -= RefreshLocale;
        RestoreTitleInput();
    }

    private void RestoreTitleInput()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (titleInput != null)
        {
            titleInput.alpha = previousTitleAlpha;
            titleInput.interactable = previousInteractable;
            titleInput.blocksRaycasts = previousBlocksRaycasts;
        }
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                ? previousSelection : null);
        titleInput = null;
    }

    // 페이지 진입 시 자동 포커스가 호버 색으로 남지 않도록 선택만 해제합니다.
    // 버튼의 Navigation/호버 스타일은 유지하므로 사용자가 직접 탐색하는 동작은 바꾸지 않습니다.
    private static void ClearUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        GameText.Changed -= RefreshLocale;
        if (catalog != null) catalog.ProfileChanged -= RefreshBagProfile;
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (backButton != null) backButton.onClick.RemoveListener(ShowMainPage);
        if (categoryListeners == null) return;
        for (int i = 0; i < categories.Length; i++)
            if (categories[i].button != null) categories[i].button.onClick.RemoveListener(categoryListeners[i]);
    }
}
