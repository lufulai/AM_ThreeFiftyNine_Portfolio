using UnityEngine;

public class PlanetSO : BaseCardSO
{
    public override CardType cardType => CardType.Planet;

    [Header("일반 스프라이트 세팅")]
    [SerializeField] public Sprite planetSprite; // 동물 스프라이트

    [TextArea(3,5)]
    public string amuletDescription;

    [Header("대비되는 족보 및 칩, 배수 세팅")]
    [SerializeField] public JokBoType jokboType;
    [SerializeField] public int plusChip;
    [SerializeField] public int plusMulti;

}
