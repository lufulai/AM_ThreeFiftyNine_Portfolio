using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 각각의 족보의 현제 레벨 및 추가 칩, 배수
public class PlanetStats
{
    public int level = 1;
    public int plusChip = 0;
    public int plusMulti = 0;
}

public class PlanetCardLevelManager : MonoBehaviour
{
    [Header("테스트용 행성 카드")]
    [SerializeField] private List<PlanetSO> planetList;

    [Header("행성 강화 연출")]
    [SerializeField] private bool usePlanetUpgradePresentation = true;
    [SerializeField] private Vector2 presentationCardSize = new Vector2(220f, 300f);
    [SerializeField] private Vector2 presentationFrameSize = new Vector2(206f, 270f);
    [SerializeField] private Vector2 presentationFrameOffset = new Vector2(0f, -24f);
    [SerializeField] private Vector2 presentationCardSpacing = new Vector2(280f, 0f);
    [SerializeField, Min(0.01f)] private float presentationEnterDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float presentationHoldDuration = 0.86f;
    [SerializeField, Min(0.01f)] private float presentationExitDuration = 0.18f;
    [SerializeField, Min(0)] private int presentationParticleCount = 26;
    [SerializeField] private Color presentationFrameColor = new Color(1f, 0.77f, 0.25f, 0.9f);
    [SerializeField] private Color presentationBackplateColor = new Color(0.035f, 0.03f, 0.04f, 0.88f);
    [SerializeField] private Color presentationParticleColor = new Color(1f, 0.78f, 0.25f, 1f);
    [SerializeField] private TMP_FontAsset presentationTextFont;

    public static PlanetCardLevelManager Instance { get; private set; }

    public Dictionary<JokBoType, PlanetStats> jokboLevel = new Dictionary<JokBoType, PlanetStats>();

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        CancelUpgradePresentation();
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable() => CancelUpgradePresentation();

    private Coroutine planetUpgradePresentationRoutine;
    private RectTransform planetUpgradeCanvasRoot;
    private readonly Queue<List<PlanetUpgradePresentationData>> upgradePresentationQueue = new Queue<List<PlanetUpgradePresentationData>>();
    private GameObject activeUpgradePresentationRoot;
    private System.IDisposable upgradeInputBlock;
    public bool IsUpgradePresentationPlaying => planetUpgradePresentationRoutine != null;

    public void UpgradeJokboLevelWithPresentation(PlanetSO planet)
        => UpgradeJokboLevelWithPresentation(planet, null);

    public void UpgradeJokboLevelWithPresentation(PlanetSO planet, Vector3? purchaseOrigin)
    {
        if (planet == null) return;
        UnlockManager.RecordItemAcquired(planet);
        UpgradeJokboLevel(planet);
        PlayPlanetUpgradePresentation(new List<PlanetUpgradePresentationData>
        {
            new PlanetUpgradePresentationData(planet, GetJokboLevel(planet.jokboType), 1, purchaseOrigin)
        });
    }

    // The pack has already committed the upgrade; this method only presents that result.
    internal IEnumerator PlayAppliedUpgradeAtPositionRoutine(PlanetSO planet, Vector3 position,
        float cardHeight, System.Action<GameObject> registerRoot)
    {
        if (!usePlanetUpgradePresentation || planet == null) yield break;
        yield return ObservatoryConstellationPresentation.PlayUpgradeResults(new[]
        {
            new PlanetUpgradePresentationData(planet, GetJokboLevel(planet.jokboType), 1,
                position, true, cardHeight)
        }, registerRoot);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (presentationTextFont == null)
            presentationTextFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/UI/Typography/EBS_jusigyeong_M SDF.asset");
    }
#endif

    public void RandomUpgrade()
    {
        if (planetList == null || planetList.Count == 0)
            return;

        int randomIndex = Random.Range(0, planetList.Count);
        PlanetSO randomPlanet = planetList[randomIndex];

        UpgradeJokboLevel(randomPlanet);
        UnlockManager.RecordItemAcquired(randomPlanet);


    }

    public List<PlanetUpgradePresentationData> RandomUpgradeDistinct(int drawCount, bool playPresentation = false)
    {
        List<PlanetUpgradePresentationData> upgradedPlanets = new List<PlanetUpgradePresentationData>();
        if (planetList == null || planetList.Count == 0)
            return upgradedPlanets;

        drawCount = Mathf.Min(drawCount, planetList.Count);

        List<PlanetSO> tempPool = new List<PlanetSO>(planetList);

        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            PlanetSO selectedPlanet = tempPool[randomIndex];

            UpgradeJokboLevel(selectedPlanet);
            UnlockManager.RecordItemAcquired(selectedPlanet);
            upgradedPlanets.Add(new PlanetUpgradePresentationData(selectedPlanet, GetJokboLevel(selectedPlanet.jokboType)));

            tempPool.RemoveAt(randomIndex);
        }

        if (playPresentation && upgradedPlanets.Count > 0)
            PlayPlanetUpgradePresentation(upgradedPlanets);

        return upgradedPlanets;
    }

    public void UpgradeJokboLevel(PlanetSO planet)
    {
        if (planet == null) return;
        ConsumableManager.Instance?.RememberUsedPlanet(planet);
        // 실제 강화에 사용된 신장패를 프로필 도감에 기록합니다.
        CollectionDiscovery.RecordCard(planet);

        JokBoType type = planet.jokboType;

        if(!jokboLevel.ContainsKey(type))
        {
            jokboLevel[type] = new PlanetStats();
        }

        jokboLevel[type].level++;
        jokboLevel[type].plusChip += planet.plusChip;
        jokboLevel[type].plusMulti += planet.plusMulti;

        GameLog.Info($"{type} 족보 업그레이드 완료 Level {jokboLevel[type].level}, " +
            $"현재 추가 칩 {jokboLevel[type].plusChip} 현재 추가 배수 {jokboLevel[type].plusMulti}");
        ArtifactManager.Instance?.ProcessArtifactTrigger(ArtifactTriggerType.OnPlanetUse);
    }

    /// <summary>
    /// 천문대처럼 여러 족보를 동시에 강화할 때 기존 신장패 강화 규칙을 지정 횟수만큼 반복 적용합니다.
    /// 각 레벨 상승은 일반 신장패 사용과 동일하게 기물 트리거도 한 번씩 발생합니다.
    /// </summary>
    public bool TryUpgradeJokbos(IReadOnlyList<JokBoType> targets, int upgradeAmount, bool playPresentation)
    {
        if (targets == null || targets.Count == 0 || upgradeAmount <= 0)
            return false;

        List<PlanetUpgradePresentationData> presentationData = new List<PlanetUpgradePresentationData>();
        HashSet<JokBoType> appliedTargets = new HashSet<JokBoType>();

        for (int i = 0; i < targets.Count; i++)
        {
            JokBoType target = targets[i];
            if (target == JokBoType.None || !appliedTargets.Add(target) || !TryGetPlanet(target, out PlanetSO planet))
                continue;

            for (int level = 0; level < upgradeAmount; level++)
                UpgradeJokboLevel(planet);

            presentationData.Add(new PlanetUpgradePresentationData(planet, GetJokboLevel(target), upgradeAmount));
        }

        if (playPresentation && presentationData.Count > 0)
            PlayPlanetUpgradePresentation(presentationData);

        return presentationData.Count > 0;
    }

    /// <summary>등록된 일반 신장패에서 지정 족보에 대응하는 데이터를 찾습니다.</summary>
    public bool TryGetPlanet(JokBoType type, out PlanetSO planet)
    {
        planet = null;
        if (type == JokBoType.None || planetList == null)
            return false;

        for (int i = 0; i < planetList.Count; i++)
        {
            PlanetSO candidate = planetList[i];
            if (candidate == null || candidate.jokboType != type)
                continue;

            planet = candidate;
            return true;
        }

        return false;
    }

    public int GetJokboLevel(JokBoType type)
    {
        return jokboLevel.TryGetValue(type, out PlanetStats stats) && stats != null
            ? Mathf.Max(1, stats.level)
            : 1;
    }

    /// <summary>족보 레벨 Dictionary를 JsonUtility가 저장할 수 있는 목록으로 변환합니다.</summary>
    public List<RunJokboLevelEntry> CaptureCheckpoint()
    {
        List<RunJokboLevelEntry> entries = new List<RunJokboLevelEntry>();
        foreach (KeyValuePair<JokBoType, PlanetStats> pair in jokboLevel)
        {
            if (pair.Key == JokBoType.None || pair.Value == null)
                continue;

            entries.Add(new RunJokboLevelEntry
            {
                jokboType = pair.Key,
                level = Mathf.Max(1, pair.Value.level),
                plusChip = pair.Value.plusChip,
                plusMulti = pair.Value.plusMulti
            });
        }

        return entries;
    }

    /// <summary>체크포인트의 족보 강화 수치만 복원하며 강화 연출과 기물 효과는 다시 발동하지 않습니다.</summary>
    public void RestoreCheckpoint(List<RunJokboLevelEntry> entries)
    {
        jokboLevel.Clear();
        if (entries == null)
            return;

        // 복원은 결과값을 직접 대입해 이미 지급된 강화 효과의 중복 발동을 막습니다.
        for (int i = 0; i < entries.Count; i++)
        {
            RunJokboLevelEntry entry = entries[i];
            if (entry == null || entry.jokboType == JokBoType.None)
                continue;

            jokboLevel[entry.jokboType] = new PlanetStats
            {
                level = Mathf.Max(1, entry.level),
                plusChip = entry.plusChip,
                plusMulti = entry.plusMulti
            };
        }
    }

    /// <summary>새 런에서 이전 런의 족보 강화 수치를 제거합니다.</summary>
    public void ResetForNewRun()
    {
        CancelUpgradePresentation();
        jokboLevel.Clear();
        ClearPlanetUpgradePresentation();
    }

    public bool TryGetNextLevelBonus(JokBoType type, out int plusChip, out int plusMulti)
    {
        plusChip = 0;
        plusMulti = 0;
        if (type == JokBoType.None || planetList == null)
            return false;

        for (int i = 0; i < planetList.Count; i++)
        {
            PlanetSO planet = planetList[i];
            if (planet == null || planet.jokboType != type)
                continue;

            plusChip = planet.plusChip;
            plusMulti = planet.plusMulti;
            return plusChip != 0 || plusMulti != 0;
        }

        return false;
    }

    private void PlayPlanetUpgradePresentation(List<PlanetUpgradePresentationData> upgradedPlanets)
    {
        if (!usePlanetUpgradePresentation || upgradedPlanets == null || upgradedPlanets.Count == 0)
            return;

        // Keep one use/purchase together; unrelated later requests remain queued.
        upgradePresentationQueue.Enqueue(new List<PlanetUpgradePresentationData>(upgradedPlanets));
        if (planetUpgradePresentationRoutine == null)
            planetUpgradePresentationRoutine = StartCoroutine(PlayQueuedUpgradeResults());
    }

    private IEnumerator PlayQueuedUpgradeResults()
    {
        upgradeInputBlock = PresentationInputBlocker.Acquire();
        try
        {
            // Let the purchase/consumable owner finish its synchronous UI cleanup.
            yield return null;
            while (upgradePresentationQueue.Count > 0)
            {
                var result = upgradePresentationQueue.Dequeue();
                yield return ObservatoryConstellationPresentation.PlayUpgradeResults(result,
                    root => activeUpgradePresentationRoot = root);
            }
        }
        finally
        {
            upgradePresentationQueue.Clear();
            planetUpgradePresentationRoutine = null;
            if (activeUpgradePresentationRoot != null)
            {
                activeUpgradePresentationRoot.SetActive(false);
                Destroy(activeUpgradePresentationRoot);
            }
            activeUpgradePresentationRoot = null;
            upgradeInputBlock?.Dispose();
            upgradeInputBlock = null;
        }
    }

    private void CancelUpgradePresentation()
    {
        if (planetUpgradePresentationRoutine != null)
            StopCoroutine(planetUpgradePresentationRoutine);
        planetUpgradePresentationRoutine = null;
        upgradePresentationQueue.Clear();
        if (activeUpgradePresentationRoot != null)
        {
            activeUpgradePresentationRoot.SetActive(false);
            Destroy(activeUpgradePresentationRoot);
        }
        activeUpgradePresentationRoot = null;
        upgradeInputBlock?.Dispose();
        upgradeInputBlock = null;
    }

    private IEnumerator PlayPlanetUpgradePresentationRoutine(List<PlanetUpgradePresentationData> upgradedPlanets)
    {
        using (PresentationInputBlocker.Acquire())
        {
            RectTransform canvasRect = CreatePresentationCanvas(out CanvasGroup canvasGroup);
            planetUpgradeCanvasRoot = canvasRect;
            List<RectTransform> cardRects = new List<RectTransform>();

            for (int i = 0; i < upgradedPlanets.Count; i++)
            {
                Vector2 anchoredPosition = ResolveCardPosition(i, upgradedPlanets.Count);
                RectTransform cardRect = CreatePlanetCardVisual(canvasRect, upgradedPlanets[i], anchoredPosition);
                cardRect.localScale = Vector3.one * 0.72f;
                cardRects.Add(cardRect);
                PlayPlanetParticles(canvasRect, anchoredPosition + presentationFrameOffset);
            }

            float scaledEnterDuration = ScalePresentationDuration(presentationEnterDuration);
            float scaledHoldDuration = PresentationSpeedManager.ScaleDelay(Mathf.Max(0f, presentationHoldDuration));
            float scaledExitDuration = ScalePresentationDuration(presentationExitDuration);
            Sequence enterSequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < cardRects.Count; i++)
            {
                RectTransform cardRect = cardRects[i];
                CanvasGroup group = cardRect.GetComponent<CanvasGroup>();
                if (group != null)
                    enterSequence.Join(group.DOFade(1f, scaledEnterDuration).SetEase(Ease.OutQuad));

                enterSequence.Join(cardRect.DOScale(Vector3.one, scaledEnterDuration).SetEase(Ease.OutBack));
                enterSequence.Join(cardRect.DOAnchorPos(cardRect.anchoredPosition + Vector2.up * 20f, scaledEnterDuration).SetEase(Ease.OutCubic));
            }

            yield return enterSequence.WaitForCompletion();
            yield return new WaitForSecondsRealtime(scaledHoldDuration);

            Sequence exitSequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < cardRects.Count; i++)
            {
                RectTransform cardRect = cardRects[i];
                CanvasGroup group = cardRect.GetComponent<CanvasGroup>();
                if (group != null)
                    exitSequence.Join(group.DOFade(0f, scaledExitDuration).SetEase(Ease.InQuad));

                exitSequence.Join(cardRect.DOScale(Vector3.one * 0.92f, scaledExitDuration).SetEase(Ease.InCubic));
            }

            if (canvasGroup != null)
                exitSequence.Join(canvasGroup.DOFade(0f, scaledExitDuration).SetEase(Ease.InQuad));

            yield return exitSequence.WaitForCompletion();
            ClearPlanetUpgradePresentation();
        }

        planetUpgradePresentationRoutine = null;
    }

    private void ClearPlanetUpgradePresentation()
    {
        if (planetUpgradeCanvasRoot == null)
            return;

        Destroy(planetUpgradeCanvasRoot.gameObject);
        planetUpgradeCanvasRoot = null;
    }

    private Vector2 ResolveCardPosition(int index, int count)
    {
        if (count <= 1)
            return Vector2.zero;

        float startX = -presentationCardSpacing.x * (count - 1) * 0.5f;
        return new Vector2(startX + presentationCardSpacing.x * index, presentationCardSpacing.y);
    }

    private RectTransform CreatePresentationCanvas(out CanvasGroup canvasGroup)
    {
        GameObject canvasObject = new GameObject("PlanetUpgradePresentationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 6200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform rect = canvasObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private RectTransform CreatePlanetCardVisual(RectTransform parent, PlanetUpgradePresentationData data, Vector2 anchoredPosition)
    {
        RectTransform root = CreateRect("PlanetUpgradeCard", parent, anchoredPosition - Vector2.up * 20f, presentationCardSize);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Vector2 frameSize = ResolvePresentationFrameSize();
        RectTransform frameRect = CreateRect("PlanetUpgradeFrame", root, presentationFrameOffset, frameSize);
        Image backplate = frameRect.gameObject.AddComponent<Image>();
        backplate.color = presentationBackplateColor;
        backplate.raycastTarget = false;
        Outline outline = frameRect.gameObject.AddComponent<Outline>();
        outline.effectColor = presentationFrameColor;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform labelRect = CreateRect("LevelLabel", root, new Vector2(0f, presentationCardSize.y * 0.58f), new Vector2(320f, 88f));
        TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = $"Level Up\nLv{data.Level}. {ResolveJokboName(data.Planet)}";
        ApplyPresentationFont(label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 31f;
        label.color = new Color(1f, 0.88f, 0.45f, 1f);
        label.raycastTarget = false;
        label.outlineColor = new Color(0.05f, 0.025f, 0.015f, 0.95f);
        label.outlineWidth = 0.18f;

        RectTransform imageRect = CreateRect("PlanetImage", root, presentationFrameOffset, frameSize * 0.74f);
        Image image = imageRect.gameObject.AddComponent<Image>();
        image.sprite = data.Planet != null ? data.Planet.planetSprite : null;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform glowRect = CreateRect("PlanetGlow", root, presentationFrameOffset, frameSize * 1.02f);
        glowRect.SetAsFirstSibling();
        Image glow = glowRect.gameObject.AddComponent<Image>();
        glow.color = new Color(presentationParticleColor.r, presentationParticleColor.g, presentationParticleColor.b, 0.13f);
        glow.raycastTarget = false;
        glow.DOFade(0f, ScalePresentationDuration(presentationEnterDuration + presentationHoldDuration)).SetEase(Ease.InQuad).SetUpdate(true);
        labelRect.SetAsLastSibling();

        return root;
    }

    private Vector2 ResolvePresentationFrameSize()
    {
        float width = presentationFrameSize.x > 0f ? presentationFrameSize.x : presentationCardSize.x;
        float height = presentationFrameSize.y > 0f ? presentationFrameSize.y : presentationCardSize.y;
        return new Vector2(width, height);
    }

    private string ResolveJokboName(PlanetSO planet)
    {
        if (planet == null)
            return GameText.Get("game.PlanetCardLevelManager.ResolveJokboName.5eed50c2fd", "족보");

        if (ScoreManager.Instance != null)
            return ScoreManager.Instance.GetJokboDisplayName(planet.jokboType);

        return string.IsNullOrWhiteSpace(planet.cardName) ? planet.jokboType.ToString() : GameContent.Get(planet, "cardName", planet.cardName);
    }

    private void ApplyPresentationFont(TextMeshProUGUI text)
    {
        if (text == null || presentationTextFont == null)
            return;

        text.font = presentationTextFont;
    }

    private void PlayPlanetParticles(RectTransform parent, Vector2 center)
    {
        if (parent == null || presentationParticleCount <= 0)
            return;

        for (int i = 0; i < presentationParticleCount; i++)
        {
            RectTransform particleRect = CreateRect("PlanetUpgradeSpark", parent, center, new Vector2(10f, 10f));
            Image particle = particleRect.gameObject.AddComponent<Image>();
            particle.color = presentationParticleColor;
            particle.raycastTarget = false;

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction == Vector2.zero)
                direction = Vector2.up;

            float distance = Random.Range(54f, 158f);
            Vector2 end = center + direction * distance + Vector2.up * Random.Range(16f, 90f);
            float duration = ScalePresentationDuration(Random.Range(0.46f, 0.76f));

            particleRect.localScale = Vector3.one * Random.Range(0.7f, 1.55f);
            particleRect.DOAnchorPos(end, duration).SetEase(Ease.OutCubic).SetUpdate(true);
            particleRect.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad).SetUpdate(true);
            particle.DOFade(0f, duration).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => Destroy(particleRect.gameObject));
        }
    }

    private static RectTransform CreateRect(string objectName, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = rectObject.transform as RectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private static float ScalePresentationDuration(float duration)
    {
        return PresentationSpeedManager.ScaleDuration(Mathf.Max(0.01f, duration));
    }

    public readonly struct PlanetUpgradePresentationData
    {
        public readonly PlanetSO Planet;
        public readonly int Level;
        public readonly int UpgradeAmount;
        public readonly Vector3? PurchaseOrigin;
        public readonly bool StayAtOrigin;
        public readonly float OriginCardHeight;

        public PlanetUpgradePresentationData(PlanetSO planet, int level)
            : this(planet, level, 1) { }

        public PlanetUpgradePresentationData(PlanetSO planet, int level, int upgradeAmount)
            : this(planet, level, upgradeAmount, null) { }

        public PlanetUpgradePresentationData(PlanetSO planet, int level, int upgradeAmount, Vector3? purchaseOrigin)
            : this(planet, level, upgradeAmount, purchaseOrigin, false, 0f) { }

        public PlanetUpgradePresentationData(PlanetSO planet, int level, int upgradeAmount,
            Vector3? purchaseOrigin, bool stayAtOrigin, float originCardHeight)
        {
            Planet = planet;
            Level = level;
            UpgradeAmount = Mathf.Max(1, upgradeAmount);
            PurchaseOrigin = purchaseOrigin;
            StayAtOrigin = stayAtOrigin;
            OriginCardHeight = Mathf.Max(0f, originCardHeight);
        }
    }

}
