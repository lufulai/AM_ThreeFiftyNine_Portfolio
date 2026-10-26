/// <summary>상점 주머니 원본을 변경하지 않고 도감용 표시 문구만 만듭니다.</summary>
public static class CollectionBagInfo
{
    // 패 제거 서비스는 상점 주머니 목록에 포함하지 않습니다.
    public static bool IsShopBag(CardpackSO data) => data != null &&
        (data.packType == PackType.Normal || data.packType == PackType.Artifact ||
         data.packType == PackType.Planet || data.packType == PackType.Amulet);

    public static string Kind(PackType type)
    {
        switch (type)
        {
            case PackType.Normal: return GameText.Get("game.CollectionBagInfo.Kind.67f8bde348", "패");
            case PackType.Artifact: return GameText.Get("game.CollectionBagInfo.Kind.7bada8867d", "기물");
            case PackType.Planet: return GameText.Get("game.CollectionBagInfo.Kind.8651744d91", "신장패");
            case PackType.Amulet: return GameText.Get("game.CollectionBagInfo.Kind.659f6fdb0a", "부적");
            default: return GameText.Get("game.CollectionBagInfo.Kind.5739db1617", "주머니");
        }
    }

    public static string Grade(PackSize size)
    {
        switch (size)
        {
            case PackSize.Normal: return GameText.Get("game.CollectionBagInfo.Grade.735234fcb2", "하급");
            case PackSize.Jumbo: return GameText.Get("game.CollectionBagInfo.Grade.e108f53258", "중급");
            case PackSize.Mega: return GameText.Get("game.CollectionBagInfo.Grade.ec5328d0bc", "상급");
            default: return GameText.Get("game.CollectionBagInfo.Grade.06a904d2fd", "등급 미등록");
        }
    }

    // 영어 packName이나 오래된 설명 대신 현재 타입·개봉 수치를 기준으로 표시합니다.
    public static string Title(CardpackSO data) => GameText.Get("game.CollectionBagInfo.Title.7862441c88", "{value0} {value1} 주머니", new { value0 = Grade(data.packSize), value1 = Kind(data.packType) });
    // 좁은 목록 카드에서는 단어가 중간에 잘리지 않도록 종류와 등급을 두 줄로 분리합니다.
    public static string ListTitle(CardpackSO data) => GameText.Get("game.CollectionBagInfo.ListTitle.4b625bc91e", "{value0} 주머니\n{value1}", new { value0 = Kind(data.packType), value1 = Grade(data.packSize) });
    /// <summary>좁은 목록 카드에서도 ‘선택’이 글자 단위로 갈라지지 않도록 행동 문구를 다음 줄에 표시합니다.</summary>
    public static string Summary(CardpackSO data) => GameText.Get("game.CollectionBagInfo.Summary.b8e7fb85c1", "기본 {value0}전 / {value1}개 중 {value2}개\n선택", new { value0 = data.price, value1 = data.generateCount, value2 = data.pickCount });

    public static string Description(CardpackSO data)
    {
        return GameText.Get("game.CollectionBagInfo.Description.d779335807", "내용물: {value0}\n등급: {value1}\n기본 가격: {value2}전\n\n개봉하면 후보 {value3}개가 제시되며,\n최대 {value4}개를 선택할 수 있습니다.\n\n표시 가격은 원본의 기본값입니다. 실제 상점 가격은 적용된 효과에 따라 달라질 수 있습니다.", new { value0 = Kind(data.packType), value1 = Grade(data.packSize), value2 = data.price, value3 = data.generateCount, value4 = data.pickCount });
    }
}
