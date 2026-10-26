using UnityEngine;

public enum EditionType { None, Silver, Gold, Radiant } // 에디션
public abstract class BaseCardSO : ScriptableObject
{
    [Header("[ 공통 카드 정보 ]")]
    public string cardID;      // 시스템 식별용 ID
    public string cardName;    // 실제 출력될 이름
    public enum CardType { Card, Planet, Joker, Amulet }
    public abstract CardType cardType { get; }
}
