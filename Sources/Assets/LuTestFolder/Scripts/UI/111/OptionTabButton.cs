using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class OptionTabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("변경할 UI 요소")]
    [SerializeField] private Image buttonBackgroundImage; // 버튼 배경 이미지
    [SerializeField] private TextMeshProUGUI buttonText;   // 버튼 글자

    [Header("평소 색상 (검정 반투명)")]
    [SerializeField] private Color normalBgColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color normalTextColor = Color.white;

    [Header("호버 및 선택 상태 색상 (주황색)")]
    [SerializeField] private Color activeBgColor = new Color(1f, 0.45f, 0.1f, 1f); // 이쁜 주황색
    [SerializeField] private Color activeTextColor = Color.white;

    private bool isSelected = false;
    private Image bookSelectionMark;
    private Image bookUnderline;
    private Color bookUnderlineTint = new Color(0.46f, 0.12f, 0.08f, 1f);

    // Only the settings book opts into this skin; other authored buttons keep their palette.
    internal void ConfigureBookStyle(Image selectionMark, Image underline)
        => ConfigureBookStyle(selectionMark, underline, null);

    internal void ConfigureBookStyle(Image selectionMark, Image underline, Color? underlineTint)
    {
        if (buttonBackgroundImage == null) buttonBackgroundImage = GetComponent<Image>();
        if (buttonText == null) buttonText = GetComponentInChildren<TextMeshProUGUI>(true);
        bookSelectionMark = selectionMark;
        bookUnderline = underline;
        bookUnderlineTint = underlineTint ?? new Color(0.46f, 0.12f, 0.08f, 1f);
        normalBgColor = activeBgColor = Color.clear;
        normalTextColor = new Color(0.13f, 0.12f, 0.11f);
        activeTextColor = new Color(0.46f, 0.12f, 0.08f);
        SetSelectState(isSelected);
    }

    private void OnEnable()
    {
        // 창이 처음 켜질 때 선택되지 않은 평소 상태로 초기화
        if (!isSelected)
        {
            SetColor(normalBgColor, normalTextColor);
        }
    }

    // 외부(OptionWindowManager)에서 선택 상태를 강제 제어할 함수
    public void SetSelectState(bool select)
    {
        isSelected = select;
        if (isSelected)
        {
            SetColor(activeBgColor, activeTextColor);
        }
        else
        {
            SetColor(normalBgColor, normalTextColor);
        }
    }

    // 마우스가 들어올 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return; // 이미 선택된 버튼이면 호버 연출 스킵
        SetColor(activeBgColor, activeTextColor);
    }

    // 마우스가 나갈 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return; // 선택된 버튼은 주황색 유지
        SetColor(normalBgColor, normalTextColor);
    }

    private void SetColor(Color bgColor, Color textColor)
    {
        if (buttonBackgroundImage != null) buttonBackgroundImage.color = bgColor;
        if (buttonText != null) buttonText.color = textColor;
        if (bookSelectionMark != null)
            bookSelectionMark.enabled = isSelected;
        if (bookUnderline != null)
        {
            bookUnderline.enabled = isSelected || textColor == activeTextColor;
            bookUnderline.color = new Color(bookUnderlineTint.r, bookUnderlineTint.g, bookUnderlineTint.b,
                bookUnderlineTint.a * (isSelected ? 0.85f : 0.4f));
        }
    }
}
