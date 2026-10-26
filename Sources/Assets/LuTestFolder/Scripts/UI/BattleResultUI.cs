using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class BattleRewardRow
{
    internal int Amount; // Runtime data, independent of formatted TMP text.
    public GameObject rowObject;          // 줄 전체 끄고 켜기 위함 (0원이면 안 보이게)
    public TextMeshProUGUI labelText;     // 항목 이름 (보스 이름, 이자 등)
    public Transform coinGroupAnchor;     // 동전이 가로 정렬될 부모 (예: RateCoinGroup)
    public TextMeshProUGUI moneyText;     // "X전" 텍스트 (예: RateRewardMoney)
}

[System.Serializable]
public class BattleMonsterGraveData
{
    public string enemyID;
    public BossType bossType;
    public string displayName;
    public GameObject gravePrefab;

    [Header("표시 보정")]
    public bool overrideGravePresentation;
    public Vector3 graveLocalPosition = new Vector3(0.008f, -0.03f, 0f);
    public Vector3 graveRotation = new Vector3(0f, 180f, 0f);
    public float graveScale = 0.1f;
}

public class BattleResultUI : MonoBehaviour
{
    public static BattleResultUI Instance { get; private set; }

    [Header("상단 텍스트")]

    public TextMeshProUGUI targetScoreText;      // 요구 점수 텍스트

    [Header("★ 3D 비석 연출 설정")]
    public Transform graveAnchor;           // 하이어라키의 'Grave' 앵커 연결
    public float graveScale = 100f;
    public Vector3 graveRotation = new Vector3(0, 180f, 0);
    public float graveZOffset = -50f;       // UI 앞으로 튀어나오게 할 Z값
    public Vector3 graveLocalPosition = new Vector3(0.008f, -0.03f, 0f);
    
    [Header("★ 몬스터 비석 데이터 설정")]
    public GameObject defaultGravePrefab;
    public List<BattleMonsterGraveData> monsterGraves = new List<BattleMonsterGraveData>();
    private GameObject currentSpawnedGrave;

    [Header("보상 항목(Row) 연결")]
    [Tooltip("우선순위 순서대로 각 줄의 오브젝트들을 연결해주세요.")]
    public BattleRewardRow baseRewardRow;  // 1. 기본 보상 (Top)
    public BattleRewardRow interestRow;    // 2. 이자 (RateText)
    public BattleRewardRow artifactRow;    // 3. 기물 (ArtifactText)
    public BattleRewardRow synergyRow;     // 4. 시너지 (SynergyText)
    public BattleRewardRow goStopRow;      // 5. 제령/고스톱 (GoStopText)

    [Header("프리팹")]
    public GameObject coinPrefab;          // 1단계에서 만든 동전 프리팹
    [SerializeField] private float coinVisualScale = 0.68f;
    [SerializeField] private float coinSpacing = -0.024f;
    [SerializeField] private float maxCoinStackWidth = 0.12f;
    [SerializeField] private float coinRightPadding = 0.008f;
    [SerializeField] private Vector2 coinStartPosition = Vector2.zero;

    [Header("패널 View")]
    [SerializeField] private BattleResultPanelView panelView;

    [Header("등장/퇴장 연출")]
    [SerializeField] private float panelRiseDistance = 0.05f;
    [SerializeField] private float panelShowDuration = 0.22f;
    [SerializeField] private float panelHideDuration = 0.16f;
    [SerializeField] private float rowRevealGap = 0.055f;
    [SerializeField] private float rowRevealDuration = 0.12f;
    [SerializeField] private float coinAppearDuration = 0.11f;
    [SerializeField] private float coinMaxStaggerDuration = 0.30f;
    [SerializeField] private float coinMaxDelay = 0.035f;
    [SerializeField] private float coinStartScale = 0.68f;
    [SerializeField] private float coinOvershootScale = 1.08f;

    private BattleRewardRow totalRewardRow;
    private BattleRewardRow entryFeeRow;
    private Coroutine presentationRoutine;
    private int presentedInterest;
    private Vector2 panelBasePosition;
    private bool hasPanelBasePosition;
    private BattleReceiptPresentation receiptPresentation;
    internal bool AdvancePresentationCompleted { get; private set; }

    private void Update() => receiptPresentation?.Tick(Time.unscaledDeltaTime);

    private void OnDisable()
    {
        AdvancePresentationCompleted = false;
        receiptPresentation?.Hide();
        StopAllCoroutines();
        presentationRoutine = null;
    }

    private void OnDestroy()
    {
        receiptPresentation?.Dispose();
        receiptPresentation = null;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        // 시작 시 화면에서 숨김
        gameObject.SetActive(false);
    }

    public void ShowResult(long targetScore, int baseReward, int interest, int artifact, int synergy, int goStop)
    {
        ShowResult(targetScore, baseReward, interest, artifact, synergy, goStop, 0);
    }

    public void ShowResult(long targetScore, int baseReward, int interest, int artifact, int synergy, int goStop, int entryFeesPaid)
    {
        AdvancePresentationCompleted = false;
        presentedInterest = interest;
        gameObject.SetActive(true);
        EnsurePanelView();
        if (panelView != null)
            entryFeeRow = panelView.EnsureEntryFeeRow(GetRewardRows());
        ApplyReceiptStyle();
        if (receiptPresentation == null) receiptPresentation = BattleReceiptPresentation.TryCreate(this, panelView);

        if (targetScoreText != null) targetScoreText.text = targetScore.ToString();

        SetupBaseReward(baseReward);

        // 2. 각 보상 줄 세팅 (0원이면 줄 전체를 숨깁니다)
        SetupRewardRow(interestRow, interest, GameText.Get("game.BattleResultUI.ShowResult.bb354c6075", "이자 <size=70%>(5전 당)</size>"));
        SetupRewardRow(artifactRow, artifact, GameText.Get("game.BattleResultUI.ShowResult.7bada8867d", "기물"));
        SetupRewardRow(synergyRow, synergy, GameText.Get("game.BattleResultUI.ShowResult.1495654724", "시너지"));
        SetupRewardRow(goStopRow, goStop, GameText.Get("game.BattleResultUI.ShowResult.f3c7b0ec82", "제령"));
        int feeLoss = -Mathf.Max(0, entryFeesPaid);
        SetupRewardRow(entryFeeRow, feeLoss, GameText.Get("game.BattleResultUI.ShowResult.13cd431a14", "제령 비용"));

        int totalReward = baseReward + interest + artifact + synergy + goStop + feeLoss;
        SetupTotalRewardRow(totalReward);

        if (panelView != null)
            panelView.RefreshRewardLayout(GetRewardRows(), totalRewardRow);

        if (receiptPresentation != null)
        {
            int pendingPayout = CombatManager.instance != null
                ? CombatManager.instance.PendingBattleResultPayout
                : Mathf.Max(0, baseReward + interest + artifact + synergy + goStop);
            receiptPresentation.Show(GetRewardRows(), totalReward, pendingPayout);
        }

        PlayShowPresentation();
    }

    public void ShowGoFailureResult(long targetScore, int entryFeesPaid)
    {
        ShowResult(targetScore, 0, 0, 0, 0, 0, entryFeesPaid);
        if (totalRewardRow != null && totalRewardRow.labelText != null)
            totalRewardRow.labelText.text = GameText.Get("game.BattleResultUI.ShowGoFailureResult.bab1c06b2b", "손실");
        receiptPresentation?.MarkFailure();
    }

    public IEnumerator PlayHideRoutine(float durationScale = 1f)
    {
        AdvancePresentationCompleted = false;
        EnsurePanelView();
        if (panelView == null)
            yield break;

        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        yield return PlayHidePresentationRoutine(Mathf.Max(0.01f, durationScale));
    }

    private void SetupBaseReward(int rewardAmount)
    {
        if (currentSpawnedGrave != null) Destroy(currentSpawnedGrave);
        currentSpawnedGrave = null;

        if (graveAnchor != null)
            graveAnchor.gameObject.SetActive(false);

        SetupRewardRow(baseRewardRow, rewardAmount, string.Empty);
    }

    private void SetupRewardRow(BattleRewardRow row, int amount, string defaultLabel)
    {
        if (row == null) return;
        row.Amount = amount;
        if (row.rowObject == null) return;

        // 보상이 0원이면 해당 줄은 Layout 그룹에서 보이지 않게 꺼버립니다.
        if (amount == 0)
        {
            row.rowObject.SetActive(false);
            return;
        }

        row.rowObject.SetActive(true);
        EnsureCoinGroupAnchor(row);

        if (row.labelText != null)
        {
            row.labelText.text = defaultLabel;
            row.labelText.richText = true;
            row.labelText.color = BattleResultPanelView.TextColor;
        }

        if (row.moneyText != null)
        {
            row.moneyText.text = GameText.Get("game.BattleResultUI.SetupRewardRow.27855a9c7b", "{value0}전", new { value0 = amount });
            row.moneyText.color = amount < 0 ? BattleResultPanelView.NegativeTextColor : BattleResultPanelView.TextColor;
        }

        if (panelView != null)
            panelView.ApplyRewardRow(row, amount);

        if (amount > 0)
        {
            // 양수 금액일 때만 동전을 촤르륵 소환
            SpawnCoins(row.coinGroupAnchor, amount);
        }
        else
        {
            // 음수 금액일 때는 동전을 소환하지 않고 빈 컨테이너로 둡니다.
            SpawnCoins(row.coinGroupAnchor, 0);
        }
    }

    private static void EnsureCoinGroupAnchor(BattleRewardRow row)
    {
        if (row == null || row.rowObject == null || row.coinGroupAnchor != null)
            return;

        Transform existing = row.rowObject.transform.Find("CoinGroup");
        if (existing == null)
            existing = row.rowObject.transform.Find("RuntimeCoinGroup");
        if (existing != null)
        {
            row.coinGroupAnchor = existing;
            return;
        }

        GameObject coinGroupObject = new GameObject("RuntimeCoinGroup", typeof(RectTransform));
        coinGroupObject.layer = row.rowObject.layer;
        coinGroupObject.transform.SetParent(row.rowObject.transform, false);

        RectTransform coinGroupRect = coinGroupObject.GetComponent<RectTransform>();
        coinGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
        coinGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
        coinGroupRect.pivot = new Vector2(0.5f, 0.5f);
        row.coinGroupAnchor = coinGroupRect;
    }

    private void SetupTotalRewardRow(int totalAmount)
    {
        EnsurePanelView();
        if (panelView == null)
            return;

        totalRewardRow = panelView.EnsureTotalRewardRow(GetRewardRows());
        if (totalRewardRow == null || totalRewardRow.rowObject == null)
            return;

        totalRewardRow.rowObject.SetActive(true);

        if (totalRewardRow.labelText != null)
            totalRewardRow.labelText.text = GameText.Get("game.BattleResultUI.SetupTotalRewardRow.61433b7da3", "총합");

        totalRewardRow.Amount = totalAmount;
        if (totalRewardRow.moneyText != null)
        {
            totalRewardRow.moneyText.text = GameText.Get("game.BattleResultUI.SetupTotalRewardRow.27855a9c7b", "{value0}전", new { value0 = totalAmount });
            totalRewardRow.moneyText.color = totalAmount < 0 ? BattleResultPanelView.NegativeTextColor : BattleResultPanelView.TotalTextColor;
        }

        panelView.ApplyTotalRewardRow(totalRewardRow, totalAmount);
        SpawnCoins(totalRewardRow.coinGroupAnchor, Mathf.Max(0, totalAmount));
    }

    private void SpawnCoins(Transform container, int count)
    {
        // The new scroll displays one income motif instead of building hidden legacy coin stacks.
        if (receiptPresentation != null) return;
        if (container == null || coinPrefab == null) return;

        // 남아있던 기존 동전 삭제
        foreach (Transform child in container)
            Destroy(child.gameObject);
        
        int visibleCount = Mathf.Max(0, count);
        RectTransform containerRect = container as RectTransform;
        float spacing = CalculateCoinSpacing(visibleCount, containerRect);
        for (int i = 0; i < visibleCount; i++)
        {
            GameObject newCoin = Instantiate(coinPrefab, container);
            
            // ★ 수정: 회전값을 코드에서 주지 않습니다. 프리팹 설정을 따릅니다.
            // 대신 위치와 스케일을 초기화하여 찌그러짐을 방지합니다.
            newCoin.transform.localPosition = Vector3.zero;
            newCoin.transform.localRotation = Quaternion.identity; // 회전 초기화 (프리팹 기준)
            newCoin.transform.localScale = Vector3.one * Mathf.Max(0.01f, coinVisualScale); // 스케일 초기화 (찌그러짐 방지)

            RectTransform rect = newCoin.GetComponent<RectTransform>();
            if (rect != null)
            {
                PrepareCoinRect(rect);
                float rightX = ResolveCoinRightX(containerRect, rect);
                rect.anchoredPosition = coinStartPosition + new Vector2(rightX - (i * spacing), 0f);
                SetCoinPresentationState(rect, 0f, coinStartScale);
            }
        }
    }

    private void PlayShowPresentation()
    {
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);

        presentationRoutine = StartCoroutine(PlayShowPresentationRoutine());
    }

    private IEnumerator PlayShowPresentationRoutine()
    {
        if (receiptPresentation != null)
        {
            while (receiptPresentation.IsVisible && !receiptPresentation.IsReady) yield return null;
            if (!receiptPresentation.IsVisible) { presentationRoutine = null; yield break; }
            TutorialSessionContext.NotifyBattleRewardPresented(presentedInterest);
            presentationRoutine = null;
            yield break;
        }
        EnsurePanelView();
        RectTransform panelRect = GetPanelRect();
        CanvasGroup panelGroup = GetOrAddCanvasGroup(panelView != null ? panelView.gameObject : null);
        BattleRewardRow[] rows = GetPresentationRows();

        CapturePanelBasePosition(panelRect);
        PrepareRowsForPresentation(rows);

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelBasePosition + new Vector2(0f, -panelRiseDistance);
            panelRect.localScale = Vector3.one * 0.96f;
        }

        float elapsed = 0f;
        while (elapsed < panelShowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, panelShowDuration));
            float eased = EaseOutCubic(t);

            if (panelGroup != null)
                panelGroup.alpha = eased;

            if (panelRect != null)
            {
                panelRect.anchoredPosition = Vector2.LerpUnclamped(panelBasePosition + new Vector2(0f, -panelRiseDistance), panelBasePosition, eased);
                float scale = Mathf.LerpUnclamped(0.96f, 1.02f, EaseOutCubic(Mathf.Clamp01(t * 1.35f)));
                if (t > 0.72f)
                    scale = Mathf.LerpUnclamped(1.02f, 1f, EaseOutCubic((t - 0.72f) / 0.28f));
                panelRect.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelBasePosition;
            panelRect.localScale = Vector3.one;
        }

        foreach (BattleRewardRow row in rows)
        {
            if (row == null || row.rowObject == null || !row.rowObject.activeSelf)
                continue;

            StartCoroutine(FadeCanvasGroup(GetOrAddCanvasGroup(row.rowObject), 0f, 1f, rowRevealDuration));
            StartCoroutine(PlayCoinStackRoutine(row.coinGroupAnchor));
            yield return new WaitForSecondsRealtime(rowRevealGap);
        }

        yield return new WaitForSecondsRealtime(coinMaxStaggerDuration + coinAppearDuration);
        FinalizeRowsPresentation(rows);

        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
        }

        // 수령 내역이 모두 나타난 뒤에 안내해 연출 중 설명창이 먼저 뜨지 않도록 합니다.
        TutorialSessionContext.NotifyBattleRewardPresented(presentedInterest);
        presentationRoutine = null;
    }

    private IEnumerator PlayHidePresentationRoutine(float durationScale)
    {
        if (receiptPresentation != null)
        {
            BattleReceiptPresentation receipt = receiptPresentation;
            int version = receipt.SequenceVersion;
            yield return receipt.PlayPayoutRoutine();
            if (receiptPresentation != receipt || !receipt.IsCurrent(version)) yield break;
            // The winding sheet needs its full readable duration even when the legacy panel requests a speed-up.
            float duration = receiptPresentation.ExitDuration * Mathf.Max(1f, durationScale);
            float time = 0f;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            receiptPresentation.SetExit(0f);
            while (time < duration && receiptPresentation == receipt && receipt.IsCurrent(version))
            {
                time = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
                receiptPresentation.SetExit(Mathf.Clamp01(time / duration));
                yield return null;
            }
            if (time >= duration && receiptPresentation == receipt && receipt.IsCurrent(version))
            {
                receipt.Hide(); AdvancePresentationCompleted = true;
            }
            yield break;
        }
        RectTransform panelRect = GetPanelRect();
        CanvasGroup panelGroup = GetOrAddCanvasGroup(panelView != null ? panelView.gameObject : null);
        BattleRewardRow[] rows = GetPresentationRows();

        CapturePanelBasePosition(panelRect);

        if (panelGroup != null)
        {
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
        }

        for (int i = rows.Length - 1; i >= 0; i--)
        {
            BattleRewardRow row = rows[i];
            if (row == null || row.rowObject == null || !row.rowObject.activeSelf)
                continue;

            StartCoroutine(FadeCanvasGroup(
                GetOrAddCanvasGroup(row.rowObject),
                1f,
                0f,
                0.08f * durationScale));
            yield return new WaitForSecondsRealtime(0.025f * durationScale);
        }

        float scaledPanelHideDuration = panelHideDuration * durationScale;
        float elapsed = 0f;
        while (elapsed < scaledPanelHideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, scaledPanelHideDuration));
            float eased = EaseInCubic(t);

            if (panelGroup != null)
                panelGroup.alpha = 1f - eased;

            if (panelRect != null)
            {
                panelRect.anchoredPosition = Vector2.LerpUnclamped(panelBasePosition, panelBasePosition + new Vector2(0f, panelRiseDistance * 0.55f), eased);
                panelRect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.985f, eased);
            }

            yield return null;
        }

        if (panelGroup != null)
            panelGroup.alpha = 0f;

        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelBasePosition;
            panelRect.localScale = Vector3.one;
        }
        AdvancePresentationCompleted = true;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t));
            yield return null;
        }

        group.alpha = to;
    }

    private IEnumerator PlayCoinStackRoutine(Transform container)
    {
        if (container == null)
            yield break;

        int coinCount = container.childCount;
        if (coinCount == 0)
            yield break;

        float delay = coinCount <= 1 ? 0f : Mathf.Min(coinMaxDelay, coinMaxStaggerDuration / Mathf.Max(1, coinCount - 1));
        for (int i = coinCount - 1; i >= 0; i--)
        {
            RectTransform coin = container.GetChild(i) as RectTransform;
            if (coin != null)
                StartCoroutine(PlayCoinAppearRoutine(coin));

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator PlayCoinAppearRoutine(RectTransform coin)
    {
        if (coin == null)
            yield break;

        Vector3 finalScale = Vector3.one * Mathf.Max(0.01f, coinVisualScale);
        Vector3 startScale = finalScale * Mathf.Max(0.01f, coinStartScale);
        Vector3 overshootScale = finalScale * Mathf.Max(1f, coinOvershootScale);
        Vector2 finalPosition = coin.anchoredPosition;
        Vector2 startPosition = finalPosition + new Vector2(0.014f, 0f);

        float elapsed = 0f;
        while (elapsed < coinAppearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, coinAppearDuration));
            float eased = EaseOutCubic(t);

            float scaleT = t < 0.62f ? EaseOutCubic(t / 0.62f) : EaseOutCubic((t - 0.62f) / 0.38f);
            coin.localScale = t < 0.62f
                ? Vector3.LerpUnclamped(startScale, overshootScale, scaleT)
                : Vector3.LerpUnclamped(overshootScale, finalScale, scaleT);

            coin.anchoredPosition = Vector2.LerpUnclamped(startPosition, finalPosition, eased);
            SetGraphicAlpha(coin, eased);
            yield return null;
        }

        coin.localScale = finalScale;
        coin.anchoredPosition = finalPosition;
        SetGraphicAlpha(coin, 1f);
    }

    private void PrepareRowsForPresentation(BattleRewardRow[] rows)
    {
        foreach (BattleRewardRow row in rows)
        {
            if (row == null || row.rowObject == null || !row.rowObject.activeSelf)
                continue;

            CanvasGroup group = GetOrAddCanvasGroup(row.rowObject);
            if (group != null)
                group.alpha = 0f;

            PrepareCoinsForPresentation(row.coinGroupAnchor);
        }
    }

    private void FinalizeRowsPresentation(BattleRewardRow[] rows)
    {
        foreach (BattleRewardRow row in rows)
        {
            if (row == null || row.rowObject == null || !row.rowObject.activeSelf)
                continue;

            CanvasGroup group = GetOrAddCanvasGroup(row.rowObject);
            if (group != null)
                group.alpha = 1f;

            SetCoinsVisible(row.coinGroupAnchor);
        }
    }

    private void PrepareCoinsForPresentation(Transform container)
    {
        if (container == null)
            return;

        foreach (Transform child in container)
        {
            RectTransform rect = child as RectTransform;
            if (rect == null)
                continue;

            SetCoinPresentationState(rect, 0f, coinStartScale);
        }
    }

    private void SetCoinsVisible(Transform container)
    {
        if (container == null)
            return;

        foreach (Transform child in container)
        {
            RectTransform rect = child as RectTransform;
            if (rect == null)
                continue;

            SetCoinPresentationState(rect, 1f, 1f);
        }
    }

    private void SetCoinPresentationState(RectTransform rect, float alpha, float scaleMultiplier)
    {
        if (rect == null)
            return;

        rect.localScale = Vector3.one * Mathf.Max(0.01f, coinVisualScale) * Mathf.Max(0.01f, scaleMultiplier);
        SetGraphicAlpha(rect, alpha);
    }

    private static void SetGraphicAlpha(Transform root, float alpha)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
                continue;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    private BattleRewardRow[] GetPresentationRows()
    {
        return new[]
        {
            baseRewardRow,
            interestRow,
            artifactRow,
            synergyRow,
            goStopRow,
            entryFeeRow,
            totalRewardRow
        };
    }

    private RectTransform GetPanelRect()
    {
        EnsurePanelView();
        return panelView != null ? panelView.transform as RectTransform : null;
    }

    private void CapturePanelBasePosition(RectTransform panelRect)
    {
        if (hasPanelBasePosition || panelRect == null)
            return;

        panelBasePosition = panelRect.anchoredPosition;
        hasPanelBasePosition = true;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private float CalculateCoinSpacing(int count, RectTransform containerRect)
    {
        if (count <= 1)
            return 0f;

        float preferredSpacing = Mathf.Abs(coinSpacing);
        float maxWidth = ResolveCoinStackMaxWidth(containerRect);
        float compressedSpacing = maxWidth / (count - 1);
        return Mathf.Min(preferredSpacing, compressedSpacing);
    }

    private void PrepareCoinRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private float ResolveCoinRightX(RectTransform containerRect, RectTransform coinRect)
    {
        if (containerRect == null)
            return 0f;

        float coinHalfWidth = 0f;
        if (coinRect != null)
            coinHalfWidth = coinRect.rect.width * Mathf.Max(0.01f, coinVisualScale) * 0.5f;

        return (containerRect.rect.width * 0.5f) - Mathf.Max(0f, coinRightPadding) - coinHalfWidth;
    }

    private float ResolveCoinStackMaxWidth(RectTransform containerRect)
    {
        float configuredWidth = Mathf.Max(0f, maxCoinStackWidth);
        if (containerRect == null)
            return configuredWidth;

        float availableWidth = Mathf.Max(0f, containerRect.rect.width - (Mathf.Max(0f, coinRightPadding) * 2f));
        return Mathf.Min(configuredWidth, availableWidth);
    }

    private Vector3 ResolveGraveLocalPosition(BattleMonsterGraveData data)
    {
        if (data != null && data.overrideGravePresentation)
            return data.graveLocalPosition;

        Vector3 position = graveLocalPosition;
        position.z = graveZOffset;
        return position;
    }

    private Vector3 ResolveGraveRotation(BattleMonsterGraveData data)
    {
        return data != null && data.overrideGravePresentation ? data.graveRotation : graveRotation;
    }

    private float ResolveGraveScale(BattleMonsterGraveData data)
    {
        if (data != null && data.overrideGravePresentation)
            return Mathf.Max(0.001f, data.graveScale);

        return Mathf.Max(0.001f, graveScale);
    }

    private void ApplyReceiptStyle()
    {
        EnsurePanelView();
        if (panelView != null)
            panelView.Apply(GetRewardRows(), targetScoreText);
    }

    private void EnsurePanelView()
    {
        if (panelView != null)
            return;

        panelView = FindFirstObjectByType<BattleResultPanelView>(FindObjectsInactive.Include);
    }

    private BattleRewardRow[] GetRewardRows()
    {
        return new[]
        {
            baseRewardRow,
            interestRow,
            artifactRow,
            synergyRow,
            goStopRow,
            entryFeeRow
        };
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
