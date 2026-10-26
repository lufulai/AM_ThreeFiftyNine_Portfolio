using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class BattleResultPanelView : MonoBehaviour
{
    public static readonly Color PanelColor = new Color(0.22f, 0.07f, 0.018f, 0.96f);
    public static readonly Color GoldColor = new Color(0.78f, 0.50f, 0.22f, 1f);
    public static readonly Color TitlePlateColor = new Color(0.46f, 0.14f, 0.035f, 0.94f);
    public static readonly Color TitleBorderColor = new Color(0.86f, 0.42f, 0.12f, 1f);
    public static readonly Color TextColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    public static readonly Color MutedTextColor = new Color(0.92f, 0.84f, 0.68f, 1f);
    public static readonly Color TargetScoreColor = new Color(1f, 0.12f, 0.08f, 1f);
    public static readonly Color DashTextColor = new Color(1f, 0.92f, 0.76f, 0.95f);
    public static readonly Color NegativeTextColor = new Color(1f, 0.26f, 0.2f, 1f);
    public static readonly Color TotalTextColor = new Color(1f, 0.78f, 0.28f, 1f);

    private const string RuntimeTitlePlateName = "RuntimeGoStopResultTitlePlate";
    private const string RuntimeBaseDividerName = "RuntimeGoStopResultBaseDivider";
    private const string RuntimeTotalRowName = "RuntimeGoStopResultTotalRow";
    private const string RuntimeTotalDividerName = "RuntimeGoStopResultTotalDivider";
    private const string RuntimeTotalLabelName = "RuntimeGoStopResultTotalLabel";
    private const string RuntimeTotalCoinGroupName = "RuntimeGoStopResultTotalCoinGroup";
    private const string RuntimeTotalMoneyName = "RuntimeGoStopResultTotalMoney";
    private const string RuntimeAmountGuideRailName = "RuntimeGoStopAmountGuideRail";
    private const string RuntimeAmountGuideFillName = "RuntimeGoStopAmountGuideFill";
    private const string RuntimeAmountGuideDotName = "RuntimeGoStopAmountGuideDot";
    private const string RuntimeHeaderDividerName = "RuntimeGoStopResultHeaderDivider";

    [Header("Reward Layout")]
    [SerializeField] private Vector2 baseRowPosition = new Vector2(-0.012f, -0.03f);
    [SerializeField] private Vector2 detailRowStartPosition = new Vector2(-0.375f, 0.055f);
    [SerializeField] private float detailRowSpacing = -0.064f;
    [SerializeField] private Vector2 totalRowPosition = new Vector2(0f, -0.238f);
    [SerializeField] private Vector2 totalRowSize = new Vector2(0.76f, 0.058f);
    [SerializeField] private Vector2 rowSize = new Vector2(0.3f, 0.056f);
    [SerializeField] private Vector2 coinGroupSize = new Vector2(0.18f, 0.054f);
    [SerializeField] private Vector2 coinGroupPosition = new Vector2(0.612f, 0f);
    [SerializeField] private Vector2 moneyTextPosition = new Vector2(0.812f, 0f);
    [SerializeField] private Vector2 totalLabelPosition = new Vector2(-0.31f, 0f);
    [SerializeField] private Vector2 totalLabelSize = new Vector2(0.13f, 0.052f);
    [SerializeField] private Vector2 totalCoinGroupPosition = new Vector2(0.17f, 0f);
    [SerializeField] private Vector2 totalMoneyTextPosition = new Vector2(0.355f, 0f);
    [SerializeField] private Vector2 totalMoneyTextSize = new Vector2(0.12f, 0.052f);
    [SerializeField] private float labelFontSize = 0.029f;
    [SerializeField] private float moneyFontSize = 0.035f;
    [SerializeField] private float totalFontSize = 0.033f;
    [SerializeField] private float guideCoinSpacing = 0.024f;
    [SerializeField] private float guideMaxCoinStackWidth = 0.12f;
    [SerializeField] private float guideDotSpacing = 0.017f;
    [SerializeField] private float guideCoinRightPadding = 0.008f;
    [SerializeField] private float guideCoinVisualWidth = 0.054f;
    [SerializeField] private float guideCoinGap = 0.028f;
    [SerializeField] private Vector2 normalGuideRange = new Vector2(0.16f, 0.612f);
    [SerializeField] private Vector2 topGuideRange = new Vector2(-0.13f, 0.265f);
    [SerializeField] private Vector2 totalGuideRange = new Vector2(-0.13f, 0.17f);

    [Header("Editable Scene References")]
    [SerializeField] private RectTransform layoutRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI targetLabelText;
    [SerializeField] private TextMeshProUGUI targetScoreText;
    [SerializeField] private RectTransform baseDivider;

    private bool initialized;
    private BattleRewardRow totalRewardRow;
    private BattleRewardRow entryFeeRow;

#if UNITY_EDITOR
    private bool editorPreviewApplyQueued;

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        QueueEditorPreviewLayout();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || !isActiveAndEnabled)
            return;

        QueueEditorPreviewLayout();
    }

    private void QueueEditorPreviewLayout()
    {
        if (editorPreviewApplyQueued)
            return;

        editorPreviewApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyQueuedEditorPreviewLayout;
    }

    private void ApplyQueuedEditorPreviewLayout()
    {
        editorPreviewApplyQueued = false;

        if (this == null || Application.isPlaying || !isActiveAndEnabled)
            return;

        ApplyEditorPreviewLayout();
    }

    [ContextMenu("Apply Result Panel Preview Layout")]
    private void ApplyEditorPreviewLayout()
    {
        BattleResultUI resultUI = GetComponentInParent<BattleResultUI>();
        if (resultUI == null)
            resultUI = FindFirstObjectByType<BattleResultUI>(FindObjectsInactive.Include);

        if (resultUI == null)
            return;

        BattleRewardRow[] rewardRows =
        {
            resultUI.baseRewardRow,
            resultUI.interestRow,
            resultUI.artifactRow,
            resultUI.synergyRow,
            resultUI.goStopRow
        };

        initialized = false;
        Apply(rewardRows, resultUI.targetScoreText);
        BattleRewardRow editorTotalRow = EnsureTotalRewardRow(rewardRows);
        RefreshRewardLayout(rewardRows, editorTotalRow);
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    public void Apply(BattleRewardRow[] rewardRows, TextMeshProUGUI fallbackTargetScoreText)
    {
        if (!initialized)
        {
            BindFallbackReferences(fallbackTargetScoreText);
            StylePanel();
            StyleDecorativeImages();
            StyleTexts();
            StyleTitle();
            EnsureBaseDivider(rewardRows);
            EnsureHeaderDivider(rewardRows);
            initialized = true;
        }

        StyleRewardRows(rewardRows);
        StyleTargetScore(fallbackTargetScoreText);
        EnsureHeaderDivider(rewardRows);
    }

    public void ApplyRewardRow(BattleRewardRow row, int amount)
    {
        if (row == null)
            return;
        row.Amount = amount;

        if (row.labelText != null)
        {
            row.labelText.color = TextColor;
            row.labelText.alignment = TextAlignmentOptions.Left;
            row.labelText.fontSize = labelFontSize;
            row.labelText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (row.moneyText != null)
        {
            row.moneyText.color = amount < 0 ? NegativeTextColor : TextColor;
            row.moneyText.alignment = TextAlignmentOptions.Right;
            row.moneyText.fontSize = moneyFontSize;
            row.moneyText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        UpdateAmountGuide(row, amount, false, normalGuideRange.x, normalGuideRange.y, 0f);
    }

    public BattleRewardRow EnsureEntryFeeRow(BattleRewardRow[] rewardRows)
    {
        if (entryFeeRow != null && entryFeeRow.rowObject != null)
            return entryFeeRow;
        Transform parent = ResolveRewardRowsParent(rewardRows);
        if (parent == null)
            return null;
        const string rowName = "RuntimeGoStopResultEntryFeeRow";
        RectTransform row = FindDirectChild(parent, rowName);
        if (row == null)
        {
            var rowObject = new GameObject(rowName, typeof(RectTransform));
            rowObject.layer = parent.gameObject.layer;
            rowObject.transform.SetParent(parent, false);
            row = rowObject.GetComponent<RectTransform>();
        }
        TextMeshProUGUI label = EnsureText(row, "EntryFeeLabel", GameText.Get("game.BattleResultPanelView.EnsureEntryFeeRow.13cd431a14", "제령 비용"), ResolvePreferredFont(rewardRows));
        TextMeshProUGUI money = EnsureText(row, "EntryFeeMoney", GameText.Get("game.BattleResultPanelView.EnsureEntryFeeRow.d94b53e54b", "0전"), label.font);
        RectTransform coinGroup = FindDirectChild(row, "CoinGroup");
        if (coinGroup == null)
        {
            var coins = new GameObject("CoinGroup", typeof(RectTransform));
            coins.layer = row.gameObject.layer;
            coins.transform.SetParent(row, false);
            coinGroup = coins.GetComponent<RectTransform>();
        }
        entryFeeRow = new BattleRewardRow
        {
            rowObject = row.gameObject,
            labelText = label,
            moneyText = money,
            coinGroupAnchor = coinGroup
        };
        return entryFeeRow;
    }

    public BattleRewardRow EnsureTotalRewardRow(BattleRewardRow[] rewardRows)
    {
        if (totalRewardRow != null && totalRewardRow.rowObject != null)
            return totalRewardRow;

        Transform parent = ResolveRewardRowsParent(rewardRows);
        if (parent == null)
            return null;

        RectTransform rowRoot = FindDirectChild(parent, RuntimeTotalRowName);
        if (rowRoot == null)
        {
            GameObject rowObject = new GameObject(RuntimeTotalRowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            rowObject.layer = parent.gameObject.layer;
            rowObject.transform.SetParent(parent, false);
            rowRoot = rowObject.GetComponent<RectTransform>();
        }

        Image background = rowRoot.GetComponent<Image>();
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.color = new Color(0.095f, 0.035f, 0.018f, 0.72f);
        background.raycastTarget = false;

        Outline outline = rowRoot.GetComponent<Outline>();
        outline.effectColor = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.72f);
        outline.effectDistance = new Vector2(0.0025f, -0.0025f);

        TextMeshProUGUI label = EnsureText(rowRoot, RuntimeTotalLabelName, GameText.Get("game.BattleResultPanelView.EnsureTotalRewardRow.61433b7da3", "총합"), ResolvePreferredFont(rewardRows));
        TextMeshProUGUI money = EnsureText(rowRoot, RuntimeTotalMoneyName, GameText.Get("game.BattleResultPanelView.EnsureTotalRewardRow.d94b53e54b", "0전"), label.font);
        RectTransform coinGroup = FindDirectChild(rowRoot, RuntimeTotalCoinGroupName);
        if (coinGroup == null)
        {
            GameObject coinGroupObject = new GameObject(RuntimeTotalCoinGroupName, typeof(RectTransform));
            coinGroupObject.layer = rowRoot.gameObject.layer;
            coinGroupObject.transform.SetParent(rowRoot, false);
            coinGroup = coinGroupObject.GetComponent<RectTransform>();
        }

        RectTransform divider = FindDirectChild(rowRoot, RuntimeTotalDividerName);
        if (divider == null)
        {
            GameObject dividerObject = new GameObject(RuntimeTotalDividerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerObject.layer = rowRoot.gameObject.layer;
            dividerObject.transform.SetParent(rowRoot, false);
            divider = dividerObject.GetComponent<RectTransform>();
        }

        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.sprite = null;
        dividerImage.color = GoldColor;
        dividerImage.raycastTarget = false;

        totalRewardRow = new BattleRewardRow
        {
            rowObject = rowRoot.gameObject,
            labelText = label,
            coinGroupAnchor = coinGroup,
            moneyText = money
        };

        ApplyTotalRewardRow(totalRewardRow, 0);
        LayoutTotalRow(totalRewardRow);
        LayoutTotalDivider(divider);
        return totalRewardRow;
    }

    public void ApplyTotalRewardRow(BattleRewardRow row, int amount)
    {
        if (row == null)
            return;
        row.Amount = amount;

        Color amountColor = amount < 0 ? NegativeTextColor : TotalTextColor;

        if (row.labelText != null)
        {
            row.labelText.color = TotalTextColor;
            row.labelText.fontSize = totalFontSize;
            row.labelText.alignment = TextAlignmentOptions.Left;
            row.labelText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (row.moneyText != null)
        {
            row.moneyText.color = amountColor;
            row.moneyText.fontSize = totalFontSize;
            row.moneyText.alignment = TextAlignmentOptions.Right;
            row.moneyText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        UpdateAmountGuide(row, amount, true, totalGuideRange.x, totalGuideRange.y, 0f);
    }

    public void RefreshRewardLayout(BattleRewardRow[] rewardRows, BattleRewardRow totalRow)
    {
        if (rewardRows == null || rewardRows.Length == 0)
            return;

        LayoutBaseRow(rewardRows[0]);

        int detailCount = 0;
        for (int i = 1; i < rewardRows.Length; i++)
            if (rewardRows[i] != null && rewardRows[i].rowObject != null && rewardRows[i].rowObject.activeSelf)
                detailCount++;
        // Include the fee without pushing the existing total/button out of the receipt.
        float spacing = detailRowSpacing * (detailCount > 4 ? 3f / (detailCount - 1) : 1f);
        int visibleDetailIndex = 0;
        for (int i = 1; i < rewardRows.Length; i++)
        {
            BattleRewardRow row = rewardRows[i];
            if (row == null || row.rowObject == null || !row.rowObject.activeSelf)
                continue;

            Vector2 position = detailRowStartPosition + new Vector2(0f, spacing * visibleDetailIndex);
            LayoutDetailRow(row, position);
            visibleDetailIndex++;
        }

        if (totalRow != null && totalRow.rowObject != null)
        {
            LayoutTotalRow(totalRow);
            RectTransform divider = FindDirectChild(totalRow.rowObject.transform, RuntimeTotalDividerName);
            if (divider != null)
                LayoutTotalDivider(divider);

            HideDashTexts(totalRow.rowObject.transform);
            UpdateAmountGuide(totalRow, GetLayoutAmount(totalRow), true, totalGuideRange.x, totalGuideRange.y, 0f);
        }
    }

    private void BindFallbackReferences(TextMeshProUGUI fallbackTargetScoreText)
    {
        if (layoutRoot == null)
            layoutRoot = transform as RectTransform;

        if (targetScoreText == null)
            targetScoreText = fallbackTargetScoreText;

        Button continueButton = FieldUIManager.Instance != null ? FieldUIManager.Instance.ResultNextButton : null;
        if (continueButton == null || !continueButton.transform.IsChildOf(transform))
            continueButton = System.Array.Find(GetComponentsInChildren<Button>(true), button => button.name == "NextButton");
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null)
                continue;

            if (titleText == null && continueButton != null && text.transform.IsChildOf(continueButton.transform))
                titleText = text;

            if (targetLabelText == null && text.name == "TargetScore")
                targetLabelText = text;
        }
    }

    private void StylePanel()
    {
        Image image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = PanelColor;
        image.raycastTarget = true;

        Outline outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();

        outline.effectColor = GoldColor;
        outline.effectDistance = new Vector2(0.005f, -0.005f);
    }

    private void StyleDecorativeImages()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image == null || image.gameObject == gameObject)
                continue;

            if (image.transform.GetComponentInParent<Button>() != null)
            {
                StyleButtonImage(image);
                continue;
            }

            if (IsCoinOrGraveImage(image.transform))
                continue;

            image.color = Color.clear;
            image.raycastTarget = false;
        }
    }

    private void StyleButtonImage(Image image)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = TitlePlateColor;
        image.raycastTarget = true;

        Outline outline = image.GetComponent<Outline>();
        if (outline == null)
            outline = image.gameObject.AddComponent<Outline>();

        outline.effectColor = TitleBorderColor;
        outline.effectDistance = new Vector2(0.004f, -0.004f);
    }

    private void StyleTexts()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null)
                continue;

            text.color = IsDashText(text.text) ? DashTextColor : TextColor;
            text.characterSpacing = 0f;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (targetLabelText != null)
        {
            targetLabelText.color = MutedTextColor;
            targetLabelText.fontSize = Mathf.Max(targetLabelText.fontSize, 0.023f);
        }
    }

    private void StyleTitle()
    {
        if (titleText == null)
            return;

        titleText.color = TextColor;
        titleText.fontSize = Mathf.Max(titleText.fontSize, 0.043f);
        titleText.alignment = TextAlignmentOptions.Center;

        RectTransform titleRect = titleText.rectTransform;
        if (titleRect == null || titleRect.parent == null)
            return;

        if (titleRect.parent.GetComponent<Button>() != null)
        {
            Image parentImage = titleRect.parent.GetComponent<Image>();
            if (parentImage != null)
                StyleButtonImage(parentImage);

            titleRect.SetAsLastSibling();
            return;
        }

        RectTransform plateRect = FindDirectChild(titleRect.parent, RuntimeTitlePlateName);
        if (plateRect == null)
        {
            GameObject plate = new GameObject(RuntimeTitlePlateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            plate.layer = titleRect.gameObject.layer;
            plate.transform.SetParent(titleRect.parent, false);
            plate.transform.SetSiblingIndex(titleRect.GetSiblingIndex());
            plateRect = plate.GetComponent<RectTransform>();
        }

        plateRect.anchorMin = titleRect.anchorMin;
        plateRect.anchorMax = titleRect.anchorMax;
        plateRect.pivot = titleRect.pivot;
        plateRect.anchoredPosition = titleRect.anchoredPosition;
        plateRect.sizeDelta = titleRect.sizeDelta + new Vector2(0.09f, 0.04f);

        Image plateImage = plateRect.GetComponent<Image>();
        plateImage.sprite = null;
        plateImage.type = Image.Type.Simple;
        plateImage.color = TitlePlateColor;
        plateImage.raycastTarget = false;

        Outline outline = plateRect.GetComponent<Outline>();
        outline.effectColor = TitleBorderColor;
        outline.effectDistance = new Vector2(0.004f, -0.004f);

        titleRect.SetAsLastSibling();
    }

    private void EnsureBaseDivider(BattleRewardRow[] rewardRows)
    {
        if (baseDivider != null)
            return;

        BattleRewardRow baseRow = rewardRows != null && rewardRows.Length > 0 ? rewardRows[0] : null;
        if (baseRow == null || baseRow.rowObject == null)
            return;

        Transform parent = baseRow.rowObject.transform.parent;
        if (parent == null)
            return;

        baseDivider = FindDirectChild(parent, RuntimeBaseDividerName);
        if (baseDivider == null)
        {
            GameObject divider = new GameObject(RuntimeBaseDividerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.layer = baseRow.rowObject.layer;
            divider.transform.SetParent(parent, false);
            divider.transform.SetSiblingIndex(baseRow.rowObject.transform.GetSiblingIndex() + 1);
            baseDivider = divider.GetComponent<RectTransform>();
        }

        RectTransform baseRect = baseRow.rowObject.transform as RectTransform;
        LayoutBaseDivider(baseRect);

        Image dividerImage = baseDivider.GetComponent<Image>();
        dividerImage.color = GoldColor;
        dividerImage.raycastTarget = false;
    }

    private void EnsureHeaderDivider(BattleRewardRow[] rewardRows)
    {
        Transform parent = ResolveRewardRowsParent(rewardRows);
        if (parent == null)
            return;

        RectTransform divider = FindDirectChild(parent, RuntimeHeaderDividerName);
        if (divider == null)
        {
            GameObject dividerObject = new GameObject(RuntimeHeaderDividerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerObject.layer = parent.gameObject.layer;
            dividerObject.transform.SetParent(parent, false);
            divider = dividerObject.GetComponent<RectTransform>();
        }

        divider.anchorMin = new Vector2(0.5f, 0.5f);
        divider.anchorMax = new Vector2(0.5f, 0.5f);
        divider.pivot = new Vector2(0.5f, 0.5f);
        divider.anchoredPosition = new Vector2(0f, 0.118f);
        divider.sizeDelta = new Vector2(0.86f, 0.005f);

        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.sprite = null;
        dividerImage.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.88f);
        dividerImage.raycastTarget = false;
    }

    private void StyleRewardRows(BattleRewardRow[] rewardRows)
    {
        if (rewardRows == null)
            return;

        foreach (BattleRewardRow row in rewardRows)
            ApplyRewardRow(row, 0);
    }

    private void StyleTargetScore(TextMeshProUGUI fallbackTargetScoreText)
    {
        if (targetScoreText == null)
            targetScoreText = fallbackTargetScoreText;

        if (targetScoreText == null)
            return;

        targetScoreText.color = TargetScoreColor;
        targetScoreText.fontSize = Mathf.Max(targetScoreText.fontSize, 0.054f);
    }

    private void LayoutBaseRow(BattleRewardRow row)
    {
        if (row == null || row.rowObject == null)
            return;

        RectTransform rowRect = row.rowObject.transform as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchoredPosition = baseRowPosition;
            rowRect.sizeDelta = Vector2.one;
        }

        LayoutTargetScoreTexts();

        if (row.labelText != null)
        {
            row.labelText.fontSize = 0.03f;
            row.labelText.alignment = TextAlignmentOptions.Center;
        }

        LayoutCoinGroup(row.coinGroupAnchor, new Vector2(0.265f, 0.206f));
        LayoutMoneyText(row.moneyText, new Vector2(0.45f, 0.206f), moneyFontSize);
        HideDashTexts(row.rowObject.transform);
        UpdateAmountGuide(row, GetLayoutAmount(row), false, topGuideRange.x, topGuideRange.y, 0.206f);

        RectTransform baseRect = row.rowObject.transform as RectTransform;
        LayoutBaseDivider(baseRect);
    }

    private void LayoutDetailRow(BattleRewardRow row, Vector2 position)
    {
        if (row == null || row.rowObject == null)
            return;

        RectTransform rowRect = row.rowObject.transform as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchoredPosition = position;
            rowRect.sizeDelta = rowSize;
        }

        LayoutLabel(row.labelText, Vector2.zero, labelFontSize);
        LayoutCoinGroup(row.coinGroupAnchor, coinGroupPosition);
        LayoutMoneyText(row.moneyText, moneyTextPosition, moneyFontSize);
        HideDashTexts(row.rowObject.transform);
        UpdateAmountGuide(row, GetLayoutAmount(row), false, normalGuideRange.x, normalGuideRange.y, 0f);
    }

    private void LayoutTotalRow(BattleRewardRow row)
    {
        if (row == null || row.rowObject == null)
            return;

        RectTransform rowRect = row.rowObject.transform as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchoredPosition = totalRowPosition;
            rowRect.sizeDelta = totalRowSize;
        }

        LayoutText(row.labelText, totalLabelPosition, totalLabelSize, totalFontSize, new Vector2(0f, 0.5f), TextAlignmentOptions.Left);
        LayoutCoinGroup(row.coinGroupAnchor, totalCoinGroupPosition);
        LayoutText(row.moneyText, totalMoneyTextPosition, totalMoneyTextSize, totalFontSize, new Vector2(1f, 0.5f), TextAlignmentOptions.Right);
        HideDashTexts(row.rowObject.transform);
        UpdateAmountGuide(row, GetLayoutAmount(row), true, totalGuideRange.x, totalGuideRange.y, 0f);
    }

    private void LayoutBaseDivider(RectTransform baseRect)
    {
        if (baseDivider == null)
            return;

        baseDivider.anchorMin = new Vector2(0.5f, 0.5f);
        baseDivider.anchorMax = new Vector2(0.5f, 0.5f);
        baseDivider.pivot = new Vector2(0.5f, 0.5f);
        baseDivider.anchoredPosition = new Vector2(0f, -0.02f);
        baseDivider.sizeDelta = new Vector2(0.90f, 0.006f);
    }

    private static void LayoutTotalDivider(RectTransform divider)
    {
        if (divider == null)
            return;

        divider.anchorMin = new Vector2(0.5f, 0.5f);
        divider.anchorMax = new Vector2(0.5f, 0.5f);
        divider.pivot = new Vector2(0.5f, 0.5f);
        divider.anchoredPosition = new Vector2(0f, 0.047f);
        divider.sizeDelta = new Vector2(0.84f, 0.0045f);
    }

    private static void LayoutLabel(TextMeshProUGUI text, Vector2 position, float fontSize)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(0.32f, 0.054f);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void LayoutCoinGroup(Transform target, Vector2 position)
    {
        if (target == null)
            return;

        RectTransform rect = target as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = coinGroupSize;
    }

    private static void LayoutMoneyText(TextMeshProUGUI text, Vector2 position, float fontSize)
    {
        if (text == null)
            return;

        LayoutText(text, position, new Vector2(0.15f, 0.056f), fontSize, new Vector2(1f, 0.5f), TextAlignmentOptions.Right);
    }

    private static void LayoutText(TextMeshProUGUI text, Vector2 position, Vector2 size, float fontSize, Vector2 pivot, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void LayoutTargetScoreTexts()
    {
        if (targetLabelText != null)
        {
            RectTransform labelRect = targetLabelText.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-0.31f, 0.265f);
            labelRect.sizeDelta = new Vector2(0.2f, 0.08f);
            targetLabelText.color = MutedTextColor;
            targetLabelText.fontSize = 0.027f;
            targetLabelText.alignment = TextAlignmentOptions.Center;
        }

        if (targetScoreText != null)
        {
            RectTransform scoreRect = targetScoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = new Vector2(-0.309f, 0.208f);
            scoreRect.sizeDelta = new Vector2(0.28f, 0.08f);
            targetScoreText.color = TargetScoreColor;
            targetScoreText.fontSize = 0.054f;
            targetScoreText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void UpdateAmountGuide(BattleRewardRow row, int amount, bool isTotal, float startX, float coinGroupCenterX, float y)
    {
        if (row == null || row.rowObject == null)
            return;

        RectTransform rowRoot = row.rowObject.transform as RectTransform;
        if (rowRoot == null)
            return;

        RectTransform guideRoot = EnsureGuideRoot(rowRoot);
        ClearGuideDots(guideRoot);

        RectTransform oldFill = FindDirectChild(rowRoot, RuntimeAmountGuideFillName);
        if (oldFill != null)
            oldFill.gameObject.SetActive(false);

        int coinCount = Mathf.Max(0, amount);
        float stackWidth = CalculateCoinStackWidth(coinCount);
        float coinHalfWidth = Mathf.Max(0f, guideCoinVisualWidth) * 0.5f;
        float coinRightCenterX = coinGroupCenterX + (coinGroupSize.x * 0.5f) - Mathf.Max(0f, guideCoinRightPadding) - coinHalfWidth;
        float endX = coinRightCenterX - stackWidth - coinHalfWidth - Mathf.Max(0f, guideCoinGap);
        float width = Mathf.Max(0f, endX - startX);
        if (width <= 0.001f)
            return;

        float dotSpacing = Mathf.Max(0.006f, guideDotSpacing);
        int dotCount = Mathf.Max(2, Mathf.FloorToInt(width / dotSpacing) + 1);
        for (int i = 0; i < dotCount; i++)
        {
            float t = dotCount <= 1 ? 1f : i / (float)(dotCount - 1);
            float x = Mathf.Lerp(startX, endX, t);
            Color dotColor = Color.Lerp(
                new Color(DashTextColor.r, DashTextColor.g, DashTextColor.b, isTotal ? 0.34f : 0.28f),
                new Color(DashTextColor.r, DashTextColor.g, DashTextColor.b, isTotal ? 0.96f : 0.88f),
                t);

            RectTransform dot = CreateGuideDot(guideRoot, i, dotColor);
            dot.anchoredPosition = new Vector2(x, y);
            dot.sizeDelta = new Vector2(isTotal ? 0.0085f : 0.0075f, isTotal ? 0.005f : 0.0045f);
        }
    }

    private RectTransform EnsureGuideRoot(RectTransform parent)
    {
        RectTransform rect = FindDirectChild(parent, RuntimeAmountGuideRailName);
        Image image = rect != null ? rect.GetComponent<Image>() : null;
        if (rect == null)
        {
            GameObject guideObject = new GameObject(RuntimeAmountGuideRailName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            guideObject.layer = parent.gameObject.layer;
            guideObject.transform.SetParent(parent, false);
            rect = guideObject.GetComponent<RectTransform>();
            image = guideObject.GetComponent<Image>();
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        if (image != null)
        {
            image.sprite = null;
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        rect.SetSiblingIndex(0);
        return rect;
    }

    private static RectTransform CreateGuideDot(RectTransform parent, int index, Color color)
    {
        GameObject dotObject = new GameObject($"{RuntimeAmountGuideDotName}_{index:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dotObject.layer = parent.gameObject.layer;
        dotObject.transform.SetParent(parent, false);

        RectTransform rect = dotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = dotObject.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void ClearGuideDots(RectTransform guideRoot)
    {
        if (guideRoot == null)
            return;

        for (int i = guideRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = guideRoot.GetChild(i);
            if (child != null && child.name.StartsWith(RuntimeAmountGuideDotName))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    private float CalculateCoinStackWidth(int coinCount)
    {
        if (coinCount <= 1)
            return 0f;

        float availableWidth = Mathf.Max(0f, coinGroupSize.x - (Mathf.Max(0f, guideCoinRightPadding) * 2f) - Mathf.Max(0f, guideCoinVisualWidth));
        float maxWidth = Mathf.Min(Mathf.Max(0f, guideMaxCoinStackWidth), availableWidth);
        float spacing = Mathf.Min(Mathf.Max(0f, guideCoinSpacing), maxWidth / (coinCount - 1));
        return spacing * (coinCount - 1);
    }

    private static void HideDashTexts(Transform root)
    {
        if (root == null)
            return;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null || !IsDashText(text.text))
                continue;

            text.gameObject.SetActive(false);
        }
    }

    private static int GetLayoutAmount(BattleRewardRow row)
    {
        if (row == null) return 0;
#if UNITY_EDITOR
        // Authored edit-time previews have no runtime reward model yet.
        if (!Application.isPlaying) return TryParseMoneyAmount(row.moneyText);
#endif
        return row.Amount;
    }

#if UNITY_EDITOR
    private static int TryParseMoneyAmount(TextMeshProUGUI text)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return 0;

        string value = text.text.Replace("전", string.Empty).Trim();
        return int.TryParse(value, out int amount) ? amount : 0;
    }
#endif

    private static Transform ResolveRewardRowsParent(BattleRewardRow[] rewardRows)
    {
        if (rewardRows == null)
            return null;

        foreach (BattleRewardRow row in rewardRows)
        {
            if (row != null && row.rowObject != null && row.rowObject.transform.parent != null)
                return row.rowObject.transform.parent;
        }

        return null;
    }

    private TMP_FontAsset ResolvePreferredFont(BattleRewardRow[] rewardRows)
    {
        if (titleText != null && titleText.font != null)
            return titleText.font;

        if (rewardRows != null)
        {
            foreach (BattleRewardRow row in rewardRows)
            {
                if (row != null && row.labelText != null && row.labelText.font != null)
                    return row.labelText.font;
            }
        }

        return null;
    }

    private static TextMeshProUGUI EnsureText(RectTransform parent, string name, string fallbackText, TMP_FontAsset font)
    {
        RectTransform rect = FindDirectChild(parent, name);
        TextMeshProUGUI text = rect != null ? rect.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        if (font != null)
            text.font = font;

        text.text = fallbackText;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        GameText.Style(text);
        return text;
    }

    private static RectTransform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child as RectTransform;
        }

        return null;
    }

    private static bool IsDashText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string value = text.Trim();
        if (value.Length < 4)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '-' && value[i] != '—' && value[i] != '─')
                return false;
        }

        return true;
    }

    private static bool IsCoinOrGraveImage(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            string name = current.name;
            if (name.Contains("Coin") || name.Contains("coin") || name.Contains("Grave") || name.Contains("grave"))
                return true;

            current = current.parent;
        }

        return false;
    }
}
