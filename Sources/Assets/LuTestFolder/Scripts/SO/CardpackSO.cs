using UnityEngine;

public enum PackType
{
    Artifact,   // 기물 팩
    Planet,     // (구) 행성카드 팩
    Amulet,       // 부적 팩
    Normal,
    Removal // 카드 제거
}

// 팩 사이즈
public enum PackSize
{
    Normal,     // 일반
    Jumbo,      // 중급
    Mega        // 상급
}

[CreateAssetMenu(fileName = "New Card Pack", menuName = "Card/Card Pack SO")]
public class CardpackSO : ScriptableObject
{
    [Header("팩 기본 정보")]
    public string packName;        // 예: "중급 패 주머니"
    [TextArea]
    public string description;     // 예: "5개의 타로카드 중 최대 1개 선택 후 즉시 사용"
    public Sprite packIcon;        // 상점에 보여질 팩 이미지

    [Header("상점 모델링")]
    public GameObject shopModelPrefab;
    [Tooltip("상점 슬롯 위에 미리 보여줄 때 사용하는 모델 배율입니다.")]
    [Min(0.01f)] public float shopDisplayScale = 1f;

    [Header("상점 Bounds 보정")]
    [Tooltip("켜면 상점 슬롯에서 사용하는 클릭/간격 bounds만 개별 보정합니다. 모델 크기는 바꾸지 않습니다.")]
    public bool useShopBoundsOverride = false;
    [Tooltip("상점 슬롯에 모델을 배치한 뒤 추가로 더할 로컬 위치 오프셋입니다. 모델 크기는 바꾸지 않습니다.")]
    public Vector3 shopPlacementOffset = Vector3.zero;
    [Tooltip("상점 슬롯에서 계산된 bounds 크기에 축별로 곱할 값입니다. 모델 스케일에는 영향을 주지 않습니다.")]
    public Vector3 shopBoundsScale = Vector3.one;
    [Tooltip("상점 슬롯 간격 계산에 사용할 최대 폭입니다. 0이면 제한하지 않습니다.")]
    [Min(0f)] public float shopMaxLayoutWidth = 0f;
    [Tooltip("상점 클릭/호버 콜라이더에 사용할 최대 폭입니다. 0이면 제한하지 않습니다.")]
    [Min(0f)] public float shopMaxColliderWidth = 0f;

    [Header("팩 설정")]
    public PackType packType;
    public PackSize packSize;
    public int price;              // 가격 (4, 6, 8)

    [Header("개봉 룰 (표 기준)")]
    public int generateCount;      // 까서 보여줄 카드 개수 (예: 3, 4, 5)
    public int pickCount;          // 플레이어가 고를 수 있는 최대 개수 (예: 1, 2)
}
