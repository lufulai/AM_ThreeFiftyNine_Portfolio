using System.Collections.Generic;
using UnityEngine;

/// <summary>실제 게임의 노출/획득만 기록합니다. 도감 조회에는 저장 부작용이 없습니다.</summary>
public static class CollectionDiscovery
{
    public static string Key(CollectionPanelView.Category category, string id)
        => string.IsNullOrWhiteSpace(id) ? string.Empty : category + ":" + id;

    // 원본 이름이나 표시 문구가 아닌 고정 카드 ID를 사용합니다.
    public static string Key(BaseCardSO card)
    {
        if (card is JokerSO joker) return Key(CollectionPanelView.Category.Joker, joker.GetPersistentID());
        if (card is PlanetSO) return Key(CollectionPanelView.Category.Planet, card.cardID);
        if (card is AmuletSO amulet)
            return amulet.amuletType == AmuletType.PlanetReplay ? Key(amulet.replayPlanet)
                : Key(CollectionPanelView.Category.Consumable, card.cardID);
        return string.Empty;
    }

    public static bool Contains(UserData profile, string key)
        => !string.IsNullOrEmpty(key) && profile?.discoveredCollectionKeys != null
            && profile.discoveredCollectionKeys.Contains(key);

    /// <summary>중복을 추가하지 않는 순수 데이터 처리입니다. 테스트/실제 저장이 같은 규칙을 씁니다.</summary>
    public static bool Add(UserData profile, IEnumerable<string> keys)
    {
        if (profile == null || keys == null) return false;
        profile.discoveredCollectionKeys ??= new List<string>();
        bool changed = false;
        foreach (string key in keys)
            if (!string.IsNullOrWhiteSpace(key) && !profile.discoveredCollectionKeys.Contains(key))
            {
                profile.discoveredCollectionKeys.Add(key);
                changed = true;
            }
        return changed;
    }

    // EditMode 미리보기와 튜토리얼은 프로필을 변경하지 않습니다. 한 노출의 여러 효과는 한 번 저장합니다.
    private static void Record(IEnumerable<string> keys)
    {
        if (SaveManager.instance == null || !CanRecord(Application.isPlaying,
            TutorialSessionContext.IsTutorialSession, SaveManager.instance.userData)) return;
        if (Add(SaveManager.instance.userData, keys)) SaveManager.instance.SaveUserData();
    }

    /// <summary>실제 기록 경로와 검사에서 공유하는 허용 조건입니다. 프로필을 생성하거나 변경하지 않습니다.</summary>
    public static bool CanRecord(bool isPlaying, bool isTutorial, UserData profile)
        => isPlaying && !isTutorial && profile != null;

    public static void RecordCard(BaseCardSO card)
    {
        if (card == null) return;
        var keys = new List<string> { Key(card) };
        if (card is CardSO normal)
        {
            AddEffect(keys, EnhancementCategory.Dan, (int)normal.currentDan);
            AddEffect(keys, EnhancementCategory.YulGgeut, (int)normal.currentYulGgeut);
            AddEffect(keys, EnhancementCategory.Gwang, (int)normal.currentGwang);
            AddEdition(keys, normal.currentEdition);
        }
        else if (card is JokerSO joker)
        {
            AddEffect(keys, EnhancementCategory.Gwang, (int)joker.currentGwang);
            AddEdition(keys, joker.currentEdition);
        }
        Record(keys);
    }

    public static string EffectKey(EnhancementCategory family, int variant)
        => Key(CollectionPanelView.Category.Enhancement, family + ":" + variant);

    private static void AddEffect(List<string> keys, EnhancementCategory family, int variant)
    {
        if (variant != 0) keys.Add(EffectKey(family, variant));
    }

    private static void AddEdition(List<string> keys, EditionType edition)
    {
        if (edition != EditionType.None) keys.Add(Key(CollectionPanelView.Category.Edition, edition.ToString()));
    }

    public static void RecordEdition(EditionType edition)
    {
        var keys = new List<string>();
        AddEdition(keys, edition);
        Record(keys);
    }

    public static void RecordVoucher(VoucherData voucher)
    {
        if (voucher != null) Record(new[] { Key(CollectionPanelView.Category.Voucher, voucher.voucherID) });
    }

    // 주머니의 오래된 영어 이름 대신 종류+크기의 조합을 고정 키로 사용합니다.
    public static string BagKey(CardpackSO bag)
        => CollectionBagInfo.IsShopBag(bag) ? Key(CollectionPanelView.Category.Bag, bag.packType + ":" + bag.packSize) : string.Empty;

    public static void RecordBag(CardpackSO bag) => Record(new[] { BagKey(bag) });
}
