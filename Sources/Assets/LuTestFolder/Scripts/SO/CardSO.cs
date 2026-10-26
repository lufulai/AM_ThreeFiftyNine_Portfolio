using UnityEngine;

public enum _CardMonth { January, February, March, April, May, June, July, August, September, October }

public enum DanEnhancement { None, HongDan, ChoDan, CheongDan } // 단 강화
public enum YulGgeutEnhancement { None, Warbler, Cuckoo, Butterfly, Boar, Goose, Deer } // 열 끗 강화 노션 순서대로

public enum GwangEnhancement { None, Gwang } // 광 강화

[CreateAssetMenu (fileName = "CardSO", menuName = "Card/CardSO")]
public class CardSO : BaseCardSO
{
    public override CardType cardType => CardType.Card;

    [Header("일반 카드 세팅")]
    public _CardMonth cardMonth;
    public int ggeutValue; 
    public bool isGwang;
    public int cardScore;

    [TextArea(3,5)]
    public string cardDescription;
    
    [Header("강화 및 에디션 상태")]
    public DanEnhancement currentDan = DanEnhancement.None;
    public YulGgeutEnhancement currentYulGgeut = YulGgeutEnhancement.None;
    public GwangEnhancement currentGwang = GwangEnhancement.None;
    public EditionType currentEdition = EditionType.None;

    [Header("패 강화 상태")]
    [Tooltip("성공한 패 강화 횟수입니다. 최대 10회입니다.")]
    [Range(0, 10)] public int cardUpgradeLevel;
    [Tooltip("파괴된 패는 기본 점수가 0이며 더 이상 강화할 수 없습니다.")]
    public bool isDestroyed;

    [Header("기본 스프라이트 세팅")]
    public Sprite monthSprite; // 월 표시
    public Sprite backSprite; // 카드 뒷면

    [Header("카드 메인 일러스트 (1장 통합)")]
    public Sprite normalSprite; // 일반 카드일 때 보여줄 이미지
    public Sprite gwangSprite;  // 광 카드일 때 보여줄 이미지
}
